using SchoolMS.Domain;
using SchoolMS.Repository;

namespace SchoolMS.Services;

public class AuthService(AuthRepo repo)
{
    /// <summary>Sign-in that was already proven another way (the fingerprint / passkey): the active user, or null.</summary>
    public User? LoginWithoutPassword(string username)
    {
        if (string.IsNullOrWhiteSpace(username)) return null;
        var u = repo.Validate(username.Trim());
        if (u != null) repo.UpdateLogin(u.UserId);
        return u;
    }

    public (bool ok, User? user, string msg) Login(string username, string password)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            return (false, null, "Username and password are required.");
        var u = repo.Validate(username.Trim());
        if (u == null) return (false, null, "Invalid username or password.");
        if (u.PasswordHash.Trim() != password.Trim()) return (false, null, "Invalid username or password.");
        repo.UpdateLogin(u.UserId);
        return (true, u, "Login successful.");
    }
}
