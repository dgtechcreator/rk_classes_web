using System.Text.Json;
using Fido2NetLib;
using Fido2NetLib.Objects;
using Microsoft.AspNetCore.Mvc;
using SchoolMS.Repository;
using SchoolMS.Services;
using SchoolMS.Web.Auth;
using SchoolMS.Web.Filters;

namespace SchoolMS.Web.Controllers;

/// <summary>
/// Fingerprint / passkey sign-in for the website (WebAuthn) - staff, teachers, admin and students / parents.
/// A signed-in person registers the device once (the browser asks for the fingerprint, face or screen lock); after that the
/// login pages offer "Login with fingerprint". Only a public key is stored here; the account checks (active, rights) are
/// the same as for a password sign-in.
/// </summary>
public class WebAuthnController(
    WebAuthnRepo repo, AuthService auth, ParentService parents, UserMgmtService userMgmt, UserAccessCache access,
    StaffNameService names, LoginTracker tracker) : Controller
{
    const string RegKey = "wa.reg", LoginKey = "wa.login";

    // Built per request so it works on any host name (localhost, the live domain ...). The browser says which origin it used.
    IFido2 F()
    {
        var origin = Request.Headers["Origin"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(origin)) origin = $"{Request.Scheme}://{Request.Host}";
        return new Fido2(new Fido2Configuration {
            ServerDomain = Request.Host.Host, ServerName = "RK Classes", Origins = new HashSet<string> { origin },
        });
    }

    /// <summary>Who is signed in on this browser: ("staff", UserId, name) or ("parent", ParentId, name), or null.</summary>
    (string type, int id, string name)? Me()
    {
        var uid = HttpContext.Session.GetInt32("UserId");
        if (uid != null) return ("staff", uid.Value, HttpContext.Session.GetString("FullName") ?? HttpContext.Session.GetString("Username") ?? "Staff");
        var pid = HttpContext.Session.GetInt32("ParentId");
        if (pid != null) return ("parent", pid.Value, HttpContext.Session.GetString("ParentName") ?? HttpContext.Session.GetString("ParentPhone") ?? "Parent");
        return null;
    }

    static string B64(byte[] b) => Microsoft.AspNetCore.WebUtilities.Base64UrlTextEncoder.Encode(b);

    // ── register this device (must be signed in) ─────────────────────────────────────────────

    [HttpPost]
    public IActionResult RegisterOptions()
    {
        var me = Me();
        if (me == null) return Unauthorized(new { error = "Please sign in first." });
        var user = new Fido2User { Id = System.Text.Encoding.UTF8.GetBytes($"{me.Value.type}:{me.Value.id}"), Name = $"{me.Value.type}-{me.Value.id}", DisplayName = me.Value.name };
        var existing = repo.ForAccount(me.Value.type, me.Value.id).Select(c => new PublicKeyCredentialDescriptor(Microsoft.AspNetCore.WebUtilities.Base64UrlTextEncoder.Decode(c.CredentialId))).ToList();
        var options = F().RequestNewCredential(new RequestNewCredentialParams {
            User = user,
            ExcludeCredentials = existing,
            AuthenticatorSelection = new AuthenticatorSelection {
                AuthenticatorAttachment = AuthenticatorAttachment.Platform,   // the fingerprint / face / screen lock of THIS device
                ResidentKey = ResidentKeyRequirement.Required,                // lets the login page work without typing a username
                UserVerification = UserVerificationRequirement.Required,
            },
            AttestationPreference = AttestationConveyancePreference.None,
        });
        HttpContext.Session.SetString(RegKey, options.ToJson());
        return Content(options.ToJson(), "application/json");
    }

    [HttpPost]
    public async Task<IActionResult> RegisterComplete([FromBody] AuthenticatorAttestationRawResponse attestation)
    {
        var me = Me();
        if (me == null) return Unauthorized(new { error = "Please sign in first." });
        var json = HttpContext.Session.GetString(RegKey);
        if (string.IsNullOrEmpty(json)) return BadRequest(new { error = "The request expired. Please try again." });
        try
        {
            var options = CredentialCreateOptions.FromJson(json);
            var cred = await F().MakeNewCredentialAsync(new MakeNewCredentialParams {
                AttestationResponse = attestation, OriginalOptions = options,
                IsCredentialIdUniqueToUserCallback = (args, ct) => Task.FromResult(repo.Get(B64(args.CredentialId)) == null),
            });
            repo.Add(new WebAuthnCredential {
                CredentialId = B64(cred.Id), AccountType = me.Value.type, AccountId = me.Value.id,
                PublicKey = Convert.ToBase64String(cred.PublicKey), SignCount = cred.SignCount,
                DeviceName = LoginTracker.DescribeBrowser(Request.Headers.UserAgent.ToString()),
            });
            HttpContext.Session.Remove(RegKey);
            return Json(new { success = true });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = "Could not register this device: " + ex.Message });
        }
    }

    // ── sign in with the fingerprint (no password) ───────────────────────────────────────────

    [HttpPost]
    public IActionResult LoginOptions()
    {
        var options = F().GetAssertionOptions(new GetAssertionOptionsParams { UserVerification = UserVerificationRequirement.Required });
        HttpContext.Session.SetString(LoginKey, options.ToJson());
        return Content(options.ToJson(), "application/json");
    }

    [HttpPost]
    public async Task<IActionResult> LoginComplete([FromBody] AuthenticatorAssertionRawResponse assertion)
    {
        var json = HttpContext.Session.GetString(LoginKey);
        if (string.IsNullOrEmpty(json)) return BadRequest(new { error = "The request expired. Please try again." });
        var cred = repo.Get(B64(assertion.RawId));
        if (cred == null) return BadRequest(new { error = "This fingerprint is not registered. Sign in with your password and turn it on first." });
        try
        {
            var options = AssertionOptions.FromJson(json);
            var result = await F().MakeAssertionAsync(new MakeAssertionParams {
                AssertionResponse = assertion, OriginalOptions = options,
                StoredPublicKey = Convert.FromBase64String(cred.PublicKey), StoredSignatureCounter = (uint)cred.SignCount,
                IsUserHandleOwnerOfCredentialIdCallback = (args, ct) => Task.FromResult(
                    System.Text.Encoding.UTF8.GetString(args.UserHandle) == $"{cred.AccountType}:{cred.AccountId}"),
            });
            repo.Touch(cred.CredentialId, result.SignCount);
            HttpContext.Session.Remove(LoginKey);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = "Fingerprint check failed: " + ex.Message });
        }

        return cred.AccountType == "parent" ? SignInParent(cred.AccountId) : SignInStaff(cred.AccountId);
    }

    IActionResult SignInStaff(int userId)
    {
        var username = userMgmt.GetById(userId)?.Username;
        var u = username == null ? null : auth.LoginWithoutPassword(username);
        if (u == null) return BadRequest(new { error = "This account is not available." });

        access.Invalidate(u.UserId);
        var acc = access.Get(u.UserId);
        if (acc != null && !acc.CanUse)
        {
            tracker.Record(HttpContext, "staff", u.UserId, u.Username, u.FullName, u.RoleName, false);
            return BadRequest(new { error = acc.CrBlocked ? "Your Class Representative access is switched off. Please contact the admin." : "This account is disabled. Please contact the admin." });
        }
        tracker.Record(HttpContext, "staff", u.UserId, u.Username + " (fingerprint)", u.FullName, u.RoleName, true);
        HttpContext.Session.SetUser(u);
        string display = u.FullName;
        try { var n = names.DisplayName(u.UserId); if (!string.IsNullOrWhiteSpace(n)) display = n; } catch { }
        HttpContext.Session.SetString("DisplayName", display);
        var perms = acc is { Known: true } ? acc.Perms : userMgmt.GetPermissions(u.UserId);
        HttpContext.Session.SetPermissions(perms);
        var home = acc?.Cr != null ? "/Attendance/Index" : PagePermissionFilter.LandingPage(u.RoleId == 1, perms);
        return Json(new { success = true, redirect = home });
    }

    IActionResult SignInParent(int parentId)
    {
        var p = parents.LoginWithoutPassword(parentId);
        if (p == null) return BadRequest(new { error = "This account is not available." });
        tracker.Record(HttpContext, "parent", p.ParentId, p.Phone + " (fingerprint)", p.FullName, "Student / Parent", true);
        HttpContext.Session.SetParentId(p.ParentId);
        HttpContext.Session.SetParentPhone(p.Phone);
        HttpContext.Session.SetParentName(p.FullName);
        return Json(new { success = true, redirect = "/Parent/Dashboard" });
    }

    // ── the signed-in person's own fingerprint devices ───────────────────────────────────────

    [HttpGet]
    public IActionResult Mine()
    {
        var me = Me();
        if (me == null) return Unauthorized();
        return Json(repo.ForAccount(me.Value.type, me.Value.id).Select(c => new { id = c.CredentialId, device = c.DeviceName, added = c.CreatedAt, lastUsed = c.LastUsedAt }));
    }

    public record RemoveReq(string Id);

    [HttpPost]
    public IActionResult Remove([FromBody] RemoveReq req)
    {
        var me = Me();
        if (me == null) return Unauthorized();
        repo.Remove(me.Value.type, me.Value.id, req.Id);
        return Json(new { success = true });
    }
}
