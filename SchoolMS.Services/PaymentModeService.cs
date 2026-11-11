using SchoolMS.Domain;
using SchoolMS.Repository;

namespace SchoolMS.Services;

public class PaymentModeService(PaymentModeRepo repo)
{
    public static readonly string[] Defaults = { "Cash", "Online Transfer", "Cheque", "Demand Draft", "Card" };

    public List<PaymentMode> GetAll() => repo.GetAll();

    /// <summary>Names offered in the forms (active ones, in master order). Falls back to the defaults when the master cannot be read.
    /// [keep] = the value already saved on a record being edited, so an old / switched-off mode is still shown.</summary>
    public List<string> ActiveNames(string? keep = null)
    {
        List<string> names;
        try { names = repo.GetAll().Where(m => m.IsActive).Select(m => m.ModeName).ToList(); }
        catch { names = new(); }
        if (names.Count == 0) names = Defaults.ToList();
        if (!string.IsNullOrWhiteSpace(keep) && !names.Contains(keep, StringComparer.OrdinalIgnoreCase)) names.Add(keep.Trim());
        return names;
    }

    public (bool ok, string? error) Save(int id, string? name, bool isActive)
    {
        name = (name ?? "").Trim();
        if (name.Length == 0) return (false, "Enter the payment mode name.");
        if (name.Length > 50) return (false, "The name is too long.");
        if (repo.GetAll().Any(m => m.ModeId != id && string.Equals(m.ModeName, name, StringComparison.OrdinalIgnoreCase)))
            return (false, "This payment mode already exists.");
        repo.Save(new PaymentMode { ModeId = id, ModeName = name, IsActive = isActive });
        return (true, null);
    }

    public void Delete(int id) => repo.Delete(id);
}
