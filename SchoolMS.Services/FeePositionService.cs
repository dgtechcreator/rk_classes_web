using SchoolMS.Domain;

namespace SchoolMS.Services;

public class FeePositionService(FeesService feesSvc, FeeStructureService feeStructureSvc)
{
    /// Replaces the old per-class "collected / pending" (read from StudentFees.PaidAmount, which is not kept up to
    /// date and showed Rs 0) with the Finance rule: fees from StudentFees, collected from non-deleted payments,
    /// pending = fees - collected - discount. Matches classes by name, so use it for the current academic year.
    public void EnrichSummary(List<FeeStructureSummary> summary)
    {
        var rows = feesSvc.GetFinanceStudents(null, null, null, true);
        static string N(string? v) => (v ?? "").Trim().ToLowerInvariant();
        foreach (var s in summary)
        {
            var m = rows.Where(r => N(r.ClassName) == N(s.ClassName) && N(r.SectionName) == N(s.SectionName)).ToList();
            s.TotalFees = m.Sum(r => r.TotalFees);
            s.Discount = m.Sum(r => r.Discount);
            s.CollectedAmt = m.Sum(r => r.Collected);
            s.PendingAmt = Math.Max(0, m.Sum(r => r.Balance));
        }

        // Class/medium groups that have students but no fee-structure head are not in the stored procedure's
        // list at all (e.g. 11th) — add them so the rows add up to the Finance totals.
        var known = summary.Select(s => (N(s.ClassName), N(s.SectionName))).ToHashSet();
        var yearName = summary.Select(s => s.YearName).FirstOrDefault(y => !string.IsNullOrWhiteSpace(y));
        foreach (var g in rows.GroupBy(r => (Cls: r.ClassName.Trim(), Sec: r.SectionName.Trim())))
        {
            if (known.Contains((N(g.Key.Cls), N(g.Key.Sec)))) continue;
            summary.Add(new FeeStructureSummary {
                ClassName = g.Key.Cls, SectionName = g.Key.Sec, YearName = yearName,
                StudentCount = g.Count(), FeeHeads = 0, TotalMonthlyFee = 0,
                TotalFees = g.Sum(r => r.TotalFees), Discount = g.Sum(r => r.Discount),
                CollectedAmt = g.Sum(r => r.Collected), PendingAmt = Math.Max(0, g.Sum(r => r.Balance)),
            });
        }

        // Class order 5th, 6th ... 12th (numeric), then medium.
        static int Num(string? c) { var d = new string((c ?? "").Where(char.IsDigit).ToArray()); return int.TryParse(d, out var n) ? n : 999; }
        var sorted = summary.OrderBy(s => Num(s.ClassName)).ThenBy(s => N(s.SectionName)).ToList();
        summary.Clear();
        summary.AddRange(sorted);
    }

    public FeePosition Calculate(Student student)
    {
        // Smart multi-level lookup (handles year/section id mismatches between Students and FeeStructure)
        var structures = feeStructureSvc.GetAllForStudent(student.StudentId);
        if (!structures.Any() && student.AcademicYearId.HasValue && student.ClassId.HasValue)
        {
            structures = feeStructureSvc.GetAll(student.AcademicYearId, student.ClassId, student.SectionId);
            if (!structures.Any() && student.SectionId.HasValue)
                structures = feeStructureSvc.GetAll(student.AcademicYearId, student.ClassId, null);
            if (!structures.Any())
                structures = feeStructureSvc.GetAll(null, student.ClassId, student.SectionId);
            if (!structures.Any())
                structures = feeStructureSvc.GetAll(null, student.ClassId, null);
        }

        DateTime? dueDate = null;
        var firstDue = structures.FirstOrDefault(x => x.DueDay > 0);
        if (firstDue != null)
            dueDate = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddDays(firstDue.DueDay - 1);

        var history = feesSvc.GetStudentHistory(student.StudentId);
        decimal extra = 0;
        foreach (var p in history)
        {
            if (string.IsNullOrEmpty(p.Remarks) || !p.Remarks.Contains("Additional Charges:")) continue;
            foreach (var part in p.Remarks.Split("|"))
                if (part.Contains("Additional Charges:") &&
                    decimal.TryParse(part.Replace("Additional Charges:", "").Replace("₹", "").Trim(), out var c))
                    extra += c;
        }

        return new FeePosition {
            BaseFee = structures.Sum(x => x.Amount),
            AdditionalCharges = extra,
            Discount = history.Sum(x => x.Discount),
            Paid = history.Sum(x => x.NetAmount),
            DueDate = dueDate,
            Structures = structures,
            History = history,
        };
    }
}
