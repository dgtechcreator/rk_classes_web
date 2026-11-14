using SchoolMS.Domain;

namespace SchoolMS.Services;

/// <summary>Rules for registering a NEW student (web form, mobile app and API all use this one list).</summary>
public static class StudentRules
{
    /// <summary>Names of the compulsory fields that are empty. Empty list = fine.</summary>
    public static List<string> MissingRequired(Student s)
    {
        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(s.FullName)) missing.Add("Full Name");
        if (s.DateOfBirth == null) missing.Add("Date of Birth");
        if (string.IsNullOrWhiteSpace(s.Gender)) missing.Add("Gender");
        if (string.IsNullOrWhiteSpace(s.FatherName)) missing.Add("Father's Name");
        if (string.IsNullOrWhiteSpace(s.MotherName)) missing.Add("Mother's Name");
        if (string.IsNullOrWhiteSpace(s.Phone)) missing.Add("Contact No.");
        if (string.IsNullOrWhiteSpace(s.Address)) missing.Add("Address");
        if (s.AdmissionDate == null) missing.Add("Admission Date");
        return missing;
    }
}
