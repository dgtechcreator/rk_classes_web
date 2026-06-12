using SchoolMS.Domain;
using SchoolMS.Repository;

namespace SchoolMS.Services;

public class ParentService(ParentRepo repo)
{
    // Login — returns (ok, parent, errorMessage)
    public (bool ok, ParentAccount? parent, string msg) Login(string phone, string password)
    {
        if (string.IsNullOrWhiteSpace(phone) || string.IsNullOrWhiteSpace(password))
            return (false, null, "Phone number and password are required.");

        phone = phone.Trim();
        var p = repo.GetByPhone(phone);
        if (p == null)
            return (false, null, "No account found. Please register first.");
        if (!p.IsActive)
            return (false, null, "Your account has been deactivated. Contact school.");
        if (p.Password != password)
            return (false, null, "Incorrect password. Please try again.");

        repo.UpdateLastLogin(p.ParentId);
        return (true, p, "Login successful.");
    }

    // Register — phone must match at least one student's FatherPhone or MotherPhone
    public (bool ok, ParentAccount? parent, string msg) Register(string phone, string password, string? fullName)
    {
        if (string.IsNullOrWhiteSpace(phone) || string.IsNullOrWhiteSpace(password))
            return (false, null, "Phone number and password are required.");

        phone = phone.Trim();

        // Already registered?
        var existing = repo.GetByPhone(phone);
        if (existing != null)
            return (false, null, "Account already exists for this number. Please login.");

        // Phone must be in student records (FatherPhone or MotherPhone)
        var children = repo.GetChildren(phone);
        if (!children.Any())
            return (false, null, "This phone number is not found in any student record. Contact school.");

        var id = repo.Save(new ParentAccount { Phone = phone, Password = password, FullName = fullName });
        if (id <= 0) return (false, null, "Registration failed. Please try again.");

        var newParent = repo.GetByPhone(phone);
        return (true, newParent, "Registration successful.");
    }

    // Get children for a parent phone
    public List<Student> GetChildren(string phone) => repo.GetChildren(phone);
}
