using System.Globalization;

namespace SchoolMS.Domain;

/// Additional charges are kept inside a payment's Remarks as "Additional Charges: ₹x" (parts separated by " | ").
/// This is the one place that reads and rewrites that text, so every page and the API agree on it.
public static class AdditionalCharges
{
    const string Marker = "Additional Charges:";

    public static string Token(decimal amount)
        => $"{Marker} ₹{amount.ToString("0.##", CultureInfo.InvariantCulture)}";

    public static bool Has(string? remarks) => !string.IsNullOrEmpty(remarks) && remarks.Contains(Marker);

    /// Sum of every "Additional Charges" part in one remarks string.
    public static decimal Parse(string? remarks)
    {
        if (!Has(remarks)) return 0;
        decimal sum = 0;
        foreach (var part in remarks!.Split('|'))
        {
            if (!part.Contains(Marker)) continue;
            var s = part.Replace(Marker, "").Replace("₹", "").Trim();
            if (decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out var c)) sum += c;
        }
        return sum;
    }

    public static decimal Total(IEnumerable<FeePayment> payments) => payments.Sum(p => Parse(p.Remarks));

    /// Remarks with the additional-charges part removed (what staff actually typed).
    public static string Strip(string? remarks)
        => string.Join(" | ", (remarks ?? "").Split('|').Select(p => p.Trim()).Where(p => p.Length > 0 && !p.Contains(Marker)));

    /// Remarks with the additional-charges part replaced by [amount] (removed when 0).
    public static string WithAmount(string? remarks, decimal amount)
    {
        var rest = Strip(remarks);
        if (amount <= 0) return rest;
        return rest.Length > 0 ? rest + " | " + Token(amount) : Token(amount);
    }

    /// The remarks rewrites that bring a student's total additional charges to [target].
    /// Lowering takes it off the newest payments first; raising puts the difference on the newest payment.
    /// [history] must be newest-first (as FeesService.GetStudentHistory returns it).
    public static List<(int PaymentId, string Remarks)> Plan(IReadOnlyList<FeePayment> history, decimal target)
    {
        var changes = new List<(int, string)>();
        decimal current = Total(history);
        if (target == current || history.Count == 0) return changes;

        if (target > current)
        {
            var p = history[0];
            changes.Add((p.PaymentId, WithAmount(p.Remarks, Parse(p.Remarks) + (target - current))));
            return changes;
        }

        decimal toRemove = current - target;
        foreach (var p in history)
        {
            if (toRemove <= 0) break;
            var c = Parse(p.Remarks);
            if (c <= 0) continue;
            var take = Math.Min(c, toRemove);
            changes.Add((p.PaymentId, WithAmount(p.Remarks, c - take)));
            toRemove -= take;
        }
        return changes;
    }
}
