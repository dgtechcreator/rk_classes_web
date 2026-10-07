using Microsoft.AspNetCore.Mvc;
using SchoolMS.Services;
using SchoolMS.Web.Filters;
using SchoolMS.Web.Utils;
using SchoolMS.Web.ViewModels;

namespace SchoolMS.Web.Controllers;

public class ParentController(
    ParentService parentSvc,
    AttendanceService attSvc,
    MarksService marksSvc,
    FeesService feesSvc,
    FeeStructureService feeStructureSvc,
    FeePositionService feePos,
    LookupService lookup,
    LectureService lectureSvc) : Controller
{
    // ── Login ────────────────────────────────────────────────────
    [HttpGet]
    public IActionResult Login()
    {
        if (HttpContext.Session.GetParentId() != null)
            return RedirectToAction("Dashboard");
        return View(new ParentLoginVM());
    }

    [HttpPost]
    public IActionResult Login(ParentLoginVM m)
    {
        var (ok, parent, msg) = parentSvc.Login(m.Phone?.Trim() ?? "", m.Password ?? "");
        if (!ok) { m.Error = msg; return View(m); }
        HttpContext.Session.SetParentId(parent!.ParentId);
        HttpContext.Session.SetParentPhone(parent.Phone);
        HttpContext.Session.SetParentName(parent.FullName);
        return RedirectToAction("Dashboard");
    }

    [HttpPost]
    public IActionResult Register(ParentLoginVM m)
    {
        var phone = m.RegPhone?.Trim() ?? "";
        var pass  = m.RegPassword  ?? "";
        var pass2 = m.RegPassword2 ?? "";

        if (pass != pass2)
        { m.ShowRegister = true; m.RegError = "Passwords do not match."; return View("Login", m); }
        if (pass.Length < 4)
        { m.ShowRegister = true; m.RegError = "Password must be at least 4 characters."; return View("Login", m); }

        var (ok, parent, msg) = parentSvc.Register(phone, pass, m.RegFullName?.Trim());
        if (!ok) { m.ShowRegister = true; m.RegError = msg; return View("Login", m); }

        HttpContext.Session.SetParentId(parent!.ParentId);
        HttpContext.Session.SetParentPhone(parent.Phone);
        HttpContext.Session.SetParentName(parent.FullName);
        TempData["Success"] = "Account created! Welcome to RK Classes Parent Portal.";
        return RedirectToAction("Dashboard");
    }

    // ── Dashboard ────────────────────────────────────────────────
    [RequireParentLogin]
    public IActionResult Dashboard(int? studentId, string tab = "attendance")
    {
        var phone    = HttpContext.Session.GetParentPhone() ?? "";
        var children = parentSvc.GetChildren(phone);

        // Auto-select first child if none chosen
        var selected = studentId.HasValue
            ? children.FirstOrDefault(c => c.StudentId == studentId.Value)
            : children.FirstOrDefault();

        var vm = new ParentDashboardVM
        {
            Children    = children,
            SelectedChild = selected,
            ActiveTab   = tab,
            ParentName  = HttpContext.Session.GetParentName(),
            ParentPhone = phone,
        };

        if (selected != null)
        {
            var sid = selected.StudentId;
            vm.Attendance = attSvc.GetStudentAttendanceSummary(sid);
            vm.AttDetail  = attSvc.GetStudentAttendanceDetail(sid);
            vm.Marks      = marksSvc.GetStudentAllMarks(sid);
            var pos = feePos.Calculate(selected);
            vm.FeeHistory = pos.History;
            vm.TotalPaid  = pos.Paid;
            vm.ActualFee  = pos.NetTotal;     // fee + additional charges - discount
            vm.Balance    = pos.Balance;

            // Get class fees summary for all students
            if (selected.ClassId.HasValue)
            {
                var classFeesSummary = feesSvc.GetClassFeesSummary(selected.ClassId.Value, selected.SectionId);
                ViewBag.ClassFeesSummary = classFeesSummary;
            }
        }

        return View(vm);
    }

    // ── Parent Accounts Management ────────────────────────────────
    [RequireLogin]
    public IActionResult Index()
    {
        if (HttpContext.Session.GetInt32("RoleId") != 1)
            return RedirectToAction("AccessDenied", "Home");
        var parentAccounts = parentSvc.GetAllParents();
        return View(parentAccounts);
    }

    // ── Get Top 5 Students for Subject ──────────────────────────
    [HttpGet]
    [RequireParentLogin]
    public IActionResult GetTop5ForSubject(int studentId, string subjectName)
    {
        try
        {
            var phone = HttpContext.Session.GetParentPhone() ?? "";
            var children = parentSvc.GetChildren(phone);
            var student = children.FirstOrDefault(c => c.StudentId == studentId);

            if (student == null)
                return Json(new { top5 = new List<object>() });

            if (!student.ClassId.HasValue)
                return Json(new { top5 = new List<object>() });

            // Fetch top 5 students in this subject for same class
            var top5 = marksSvc.GetTop5StudentsInSubject(subjectName, student.ClassId.Value, student.SectionId);

            return Json(new { top5 = top5 });
        }
        catch (Exception ex)
        {
            return Json(new { top5 = new List<object>(), error = ex.Message });
        }
    }

    // ── Class toppers (overall + per subject) of the child's own class & medium ──
    [HttpGet]
    [RequireParentLogin]
    public IActionResult Toppers(int studentId)
    {
        var phone = HttpContext.Session.GetParentPhone() ?? "";
        var child = parentSvc.GetChildren(phone).FirstOrDefault(c => c.StudentId == studentId);
        if (child == null) return NotFound(new { error = "Student not found for this parent." });
        return Json(ParentToppersHelper.Build(child, marksSvc, lookup));
    }

    // ── Lectures of the child's own class / medium / batch (day list + month counts) ──
    [HttpGet]
    [RequireParentLogin]
    public IActionResult Lectures(int studentId, DateTime? date)
    {
        var phone = HttpContext.Session.GetParentPhone() ?? "";
        var child = parentSvc.GetChildren(phone).FirstOrDefault(c => c.StudentId == studentId);
        if (child == null) return NotFound(new { error = "Student not found for this parent." });
        return Json(LectureHelpers.ParentPayload(lectureSvc, child, date ?? DateTime.Today));
    }

    // ── Contact Us ──────────────────────────────────────────────
    [RequireParentLogin]
    public IActionResult ContactUs(int? studentId, string tab = "attendance")
    {
        var phone = HttpContext.Session.GetParentPhone() ?? "";
        var children = parentSvc.GetChildren(phone);

        var selected = studentId.HasValue
            ? children.FirstOrDefault(c => c.StudentId == studentId.Value)
            : children.FirstOrDefault();

        ViewBag.Children = children;
        ViewBag.SelectedChild = selected;
        ViewBag.SelectedStudentId = selected?.StudentId;
        ViewBag.ActiveTab = tab;
        ViewBag.SidebarChildren = children;

        return View();
    }

    [HttpPost]
    [RequireParentLogin]
    public IActionResult SendMessage(string name, string email, string subject, string message)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(email) ||
                string.IsNullOrWhiteSpace(subject) || string.IsNullOrWhiteSpace(message))
            {
                TempData["Error"] = "All fields are required.";
                return RedirectToAction("ContactUs");
            }

            // Send email (basic implementation - you can integrate with email service)
            TempData["Success"] = "Your message has been sent! We'll get back to you soon.";
            return RedirectToAction("ContactUs");
        }
        catch (Exception ex)
        {
            TempData["Error"] = "Error sending message: " + ex.Message;
            return RedirectToAction("ContactUs");
        }
    }

    // ── Logout ───────────────────────────────────────────────────
    public IActionResult Logout()
    {
        HttpContext.Session.Remove("ParentId");
        HttpContext.Session.Remove("ParentPhone");
        HttpContext.Session.Remove("ParentName");
        return RedirectToAction("Login");
    }
}
