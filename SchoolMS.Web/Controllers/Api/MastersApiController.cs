using Microsoft.AspNetCore.Mvc;
using SchoolMS.Domain;
using SchoolMS.Services;
using SchoolMS.Web.Auth;

namespace SchoolMS.Web.Controllers.Api;

[ApiController]
[Route("api/masters")]
[ApiRequireStaff]
public class MastersApiController(MastersService svc, StudentService studentSvc, AttendanceBatchService batchSvc) : ControllerBase
{
    public record SaveYearReq(int YearId, string YearName, bool IsCurrent, bool IsActive = true);
    public record SaveClassReq(int ClassId, string ClassName, int OrderNo, bool IsActive = true);
    public record SaveSectionReq(int SectionId, string SectionName, bool IsActive = true);
    public record SaveBatchReq(int BatchId, string BatchName, bool IsActive = true);
    public record SaveSubjectReq(int SubjectId, string SubjectName, string? SubjectCode, int ClassId = 0, int MaxMarks = 100, int PassMarks = 35, bool IsActive = true);
    public record SaveExpenseCatReq(int CategoryId, string CategoryName, bool IsActive = true);
    public record CreateAttendanceBatchReq(string BatchName, List<int> StudentIds);
    public record SaveAttendanceBatchReq(int BatchId, string BatchName, List<int> StudentIds);

    // ── Academic Year ─────────────────────────────────────────
    [HttpPost("years/save")]
    public IActionResult SaveYear([FromBody] SaveYearReq req)
    {
        try
        {
            svc.SaveYear(new AcademicYear { YearId = req.YearId, YearName = req.YearName, IsCurrent = req.IsCurrent, IsActive = req.IsActive });
            return Ok(new { success = true });
        }
        catch (Exception ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPost("years/{id:int}/delete")]
    public IActionResult DeleteYear(int id)
    {
        try { svc.DeleteYear(id); return Ok(new { success = true }); }
        catch (Exception ex) { return BadRequest(new { error = ex.Message }); }
    }

    // ── Class ─────────────────────────────────────────────────
    [HttpPost("classes/save")]
    public IActionResult SaveClass([FromBody] SaveClassReq req)
    {
        try
        {
            svc.SaveClass(new Class { ClassId = req.ClassId, ClassName = req.ClassName, OrderNo = req.OrderNo, IsActive = req.IsActive });
            return Ok(new { success = true });
        }
        catch (Exception ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPost("classes/{id:int}/delete")]
    public IActionResult DeleteClass(int id)
    {
        try { svc.DeleteClass(id); return Ok(new { success = true }); }
        catch (Exception ex) { return BadRequest(new { error = ex.Message }); }
    }

    // ── Section ───────────────────────────────────────────────
    [HttpPost("sections/save")]
    public IActionResult SaveSection([FromBody] SaveSectionReq req)
    {
        try
        {
            svc.SaveSection(new Section { SectionId = req.SectionId, SectionName = req.SectionName, IsActive = req.IsActive });
            return Ok(new { success = true });
        }
        catch (Exception ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPost("sections/{id:int}/delete")]
    public IActionResult DeleteSection(int id)
    {
        try { svc.DeleteSection(id); return Ok(new { success = true }); }
        catch (Exception ex) { return BadRequest(new { error = ex.Message }); }
    }

    // ── Batch ─────────────────────────────────────────────────
    [HttpPost("batches/save")]
    public IActionResult SaveBatch([FromBody] SaveBatchReq req)
    {
        try
        {
            svc.SaveBatch(new Batch { BatchId = req.BatchId, BatchName = req.BatchName, IsActive = req.IsActive });
            return Ok(new { success = true });
        }
        catch (Exception ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPost("batches/{id:int}/delete")]
    public IActionResult DeleteBatch(int id)
    {
        try { svc.DeleteBatch(id); return Ok(new { success = true }); }
        catch (Exception ex) { return BadRequest(new { error = ex.Message }); }
    }

    // ── Subject ───────────────────────────────────────────────
    [HttpPost("subjects/save")]
    public IActionResult SaveSubject([FromBody] SaveSubjectReq req)
    {
        try
        {
            svc.SaveSubject(new Subject {
                SubjectId = req.SubjectId, SubjectName = req.SubjectName, SubjectCode = req.SubjectCode,
                ClassId = req.ClassId == 0 ? (int?)null : req.ClassId, MaxMarks = req.MaxMarks,
                PassMarks = req.PassMarks, IsActive = req.IsActive
            });
            return Ok(new { success = true });
        }
        catch (Exception ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPost("subjects/{id:int}/delete")]
    public IActionResult DeleteSubject(int id)
    {
        try { svc.DeleteSubject(id); return Ok(new { success = true }); }
        catch (Exception ex) { return BadRequest(new { error = ex.Message }); }
    }

    // ── Expense Categories ────────────────────────────────────
    [HttpPost("expcats/save")]
    public IActionResult SaveExpenseCat([FromBody] SaveExpenseCatReq req)
    {
        try
        {
            svc.SaveExpenseCat(new ExpenseCat { CategoryId = req.CategoryId, CategoryName = req.CategoryName, IsActive = req.IsActive });
            return Ok(new { success = true });
        }
        catch (Exception ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPost("expcats/{id:int}/delete")]
    public IActionResult DeleteExpenseCat(int id)
    {
        try { svc.DeleteExpenseCat(id); return Ok(new { success = true }); }
        catch (Exception ex) { return BadRequest(new { error = ex.Message }); }
    }

    // ── Attendance Batches ─────────────────────────────────────
    [HttpGet("attendance-batches")]
    public IActionResult AttendanceBatches()
    {
        return Ok(new {
            classes = svc.GetClasses(), sections = svc.GetSections(), batches = svc.GetBatches(),
            attendanceBatches = batchSvc.GetAll(),
        });
    }

    [HttpGet("attendance-batches/students")]
    public IActionResult GetStudentsForBatch(int classId, int? sectionId, int? batchId)
    {
        try
        {
            var (students, _) = studentSvc.GetAll(1, 10000, null, classId > 0 ? classId : null,
                sectionId, batchId, null, "Active");
            return Ok(students.Select(s => new {
                studentId = s.StudentId,
                fullName = s.FullName,
                admissionNo = s.AdmissionNo,
                className = s.ClassName,
                sectionName = s.SectionName,
                batchName = s.BatchName
            }));
        }
        catch (Exception ex)
        {
            return Ok(new { error = ex.Message });
        }
    }

    [HttpPost("attendance-batches/create")]
    public IActionResult CreateAttendanceBatch([FromBody] CreateAttendanceBatchReq req)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(req?.BatchName) || req?.StudentIds == null || !req.StudentIds.Any())
                return Ok(new { success = false, message = "Batch name and students are required." });

            var batchId = batchSvc.CreateBatch(req.BatchName, req.StudentIds);
            return Ok(new { success = true, message = $"Batch '{req.BatchName}' created with {req.StudentIds.Count} students.", batchId });
        }
        catch (Exception ex)
        {
            return Ok(new { success = false, message = ex.Message });
        }
    }

    [HttpPost("attendance-batches/{batchId:int}/delete")]
    public IActionResult DeleteAttendanceBatch(int batchId)
    {
        try
        {
            batchSvc.DeleteBatch(batchId);
            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("attendance-batches/{batchId:int}/edit")]
    public IActionResult EditAttendanceBatch(int batchId)
    {
        try
        {
            var batch = batchSvc.GetById(batchId);
            if (batch == null) return NotFound(new { error = "Batch not found." });

            int? preselectedClassId = null;
            if (batch.StudentIds.Any())
            {
                var (students, _) = studentSvc.GetAll(1, 10000, null, null, null, null, null, "Active");
                var firstStudent = students.FirstOrDefault(s => batch.StudentIds.Contains(s.StudentId));
                if (firstStudent != null) preselectedClassId = firstStudent.ClassId;
            }

            return Ok(new {
                batch, classes = svc.GetClasses(), sections = svc.GetSections(), batches = svc.GetBatches(),
                preselectedClassId,
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("attendance-batches/save")]
    public IActionResult SaveAttendanceBatch([FromBody] SaveAttendanceBatchReq req)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(req.BatchName) || req.StudentIds == null || !req.StudentIds.Any())
                throw new Exception("Batch name and students are required.");

            // CreateBatch handles both insert and update based on BatchId internally via the repo.
            batchSvc.CreateBatch(req.BatchName, req.StudentIds);
            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
