using SchoolMS.Domain;

namespace SchoolMS.Services;

public class FeePositionService(FeesService feesSvc, FeeStructureService feeStructureSvc)
{
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
