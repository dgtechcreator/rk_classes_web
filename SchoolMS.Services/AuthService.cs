using SchoolMS.Domain;
using SchoolMS.Repository;

namespace SchoolMS.Services;

public class AuthService(AuthRepo repo)
{
    public (bool ok, User? user, string msg) Login(string username, string password)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            return (false, null, "Username and password are required.");
        var u = repo.Validate(username);
        if (u == null) return (false, null, "Invalid username or password.");
        if (u.PasswordHash != password) return (false, null, "Invalid username or password.");
        repo.UpdateLogin(u.UserId);
        return (true, u, "Login successful.");
    }
}
