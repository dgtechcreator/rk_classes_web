using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.IdentityModel.Tokens;
using SchoolMS.Domain;

namespace SchoolMS.Web.Auth;

// Issues JWTs for the mobile app — two token "types" (staff / parent) sharing one signing key.
public class JwtTokenService(IConfiguration config)
{
    public string GenerateStaffToken(User u, Dictionary<string, bool> perms)
    {
        var claims = new List<Claim> {
            new(ClaimTypes.NameIdentifier, u.UserId.ToString()),
            new(ClaimTypes.Name, u.FullName),
            new("username", u.Username),
            new("type", "staff"),
            new("roleId", u.RoleId.ToString()),
            new("roleName", u.RoleName),
            new("perms", JsonSerializer.Serialize(perms)),
        };
        return Build(claims);
    }

    public string GenerateParentToken(ParentAccount p)
    {
        var claims = new List<Claim> {
            new(ClaimTypes.NameIdentifier, p.ParentId.ToString()),
            new(ClaimTypes.Name, p.FullName ?? ""),
            new("type", "parent"),
            new("phone", p.Phone),
        };
        return Build(claims);
    }

    string Build(List<Claim> claims)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:Secret"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expiryDays = int.TryParse(config["Jwt:ExpiryDays"], out var d) ? d : 30;
        var token = new JwtSecurityToken(
            issuer: config["Jwt:Issuer"],
            audience: config["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddDays(expiryDays),
            signingCredentials: creds
        );
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
