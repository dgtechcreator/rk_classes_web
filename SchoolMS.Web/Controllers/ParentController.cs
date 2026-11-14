using Microsoft.AspNetCore.Mvc;
using SchoolMS.Services;
using SchoolMS.Web.Auth;
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
    LectureService lectureSvc,
    ActivityNotifier activity,
    ClassNotesService notesSvc,
    SchoolMS.Web.Auth.NoteFileStore noteFiles,
    PortalAccessService portal,
    StudentService studentSvc,
    SchoolMS.Web.Auth.ProfilePhotoStore photos,
    FacultyService facultySvc,
    LoginTracker tracker) : Controller
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
        if (!ok) { tracker.Record(HttpContext, "parent", null, m.Phone, null, "Student / Parent", false); m.Error = msg; return View(m); }
        tracker.Record(HttpContext, "parent", parent!.ParentId, m.Phone, parent.FullName, "Student / Parent", true);
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

        tracker.Record(HttpContext, "parent", parent!.ParentId, phone, parent.FullName, "Student / Parent", true);
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

        // A tab the admin has switched off (Roles & Access) is never shown: fall back to the first one that is on.
        var hidden = portal.Hidden();
        if (hidden.Contains(tab ?? ""))
            tab = new[] { "attendance", "lectures", "marks", "fees" }.FirstOrDefault(t => !hidden.Contains(t)) ?? "";

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
            if (!hidden.Contains("attendance"))
            {
                vm.Attendance = attSvc.GetStudentAttendanceSummary(sid);
                vm.AttDetail  = attSvc.GetStudentAttendanceDetail(sid);
                facultySvc.ApplyTeacherLabels(vm.AttDetail);   // "Rakesh Mishra SIR" instead of the bare name
            }
            if (!hidden.Contains("marks")) vm.Marks = marksSvc.GetStudentAllMarks(sid);
            var pos = feePos.Calculate(selected);
            vm.FeeHistory = pos.History;
            vm.TotalPaid  = pos.Paid;
            vm.ActualFee  = pos.NetTotal;     // fee + additional charges - discount
            vm.Balance    = pos.Balance;
            vm.PreviousYearsDue = pos.PreviousYearsDue;
            vm.InstallmentDate = pos.InstallmentDate;

            // (The old "class fees summary of all students" is gone: a parent sees only their own child's paid + balance.)
        }

        return View(vm);
    }

    /// <summary>A parent / student sets or replaces the child's profile photo (same file + DB column the staff side and the app use).</summary>
    [HttpPost]
    [RequireParentLogin]
    public IActionResult UploadChildPhoto(int studentId, IFormFile? photo)
    {
        var phone = HttpContext.Session.GetParentPhone() ?? "";
        if (!parentSvc.GetChildren(phone).Any(c => c.StudentId == studentId)) return NotFound();
        var s = studentSvc.GetById(studentId);
        if (s == null) return NotFound();
        var (path, err) = photos.Save(photo, "students");
        if (err != null) { TempData["Error"] = err; return RedirectToAction("Dashboard", new { studentId }); }
        studentSvc.SetPhoto(studentId, path);
        photos.Delete(s.ProfilePicPath);
        TempData["Success"] = "Photo updated.";
        return RedirectToAction("Dashboard", new { studentId });
    }

    /// <summary>The student / parent edits the child's own date of birth (required) and email (optional). Same rules as the app's api/parent/students/{id}/details.</summary>
    [HttpPost]
    [RequireParentLogin]
    public IActionResult UpdateChildDetails(int studentId, DateTime? dateOfBirth, string? email)
    {
        var phone = HttpContext.Session.GetParentPhone() ?? "";
        if (!parentSvc.GetChildren(phone).Any(c => c.StudentId == studentId)) return NotFound();
        email = email?.Trim();
        string? error = null;
        if (dateOfBirth == null) error = "Date of birth is required.";
        else if (dateOfBirth.Value.Date > DateTime.Today || dateOfBirth.Value.Year < 1980) error = "Please enter a valid date of birth.";
        else if (!string.IsNullOrEmpty(email) && (email.Length > 150 || !System.Text.RegularExpressions.Regex.IsMatch(email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$")))
            error = "Please enter a valid email address, or leave it empty.";
        if (error != null) { TempData["Error"] = error; return RedirectToAction("Dashboard", new { studentId }); }
        studentSvc.SetBasicDetails(studentId, dateOfBirth!.Value, email);
        TempData["Success"] = "Details updated.";
        return RedirectToAction("Dashboard", new { studentId });
    }

    // ── Parent Accounts Management ────────────────────────────────
    [RequireLogin]
    public IActionResult Index()
    {
        if (HttpContext.Session.GetInt32("RoleId") != 1)
            return RedirectToAction("AccessDenied", "Home");
        var parentAccounts = parentSvc.GetAllParents();
        // which student(s) / class / medium-stream each login belongs to
        foreach (var p in parentAccounts) { try { p.Children = parentSvc.GetChildren(p.Phone); } catch { } }
        return View(parentAccounts);
    }

    // ── Get Top 5 Students for Subject ──────────────────────────
    [HttpGet]
    [RequireParentLogin]
    [PortalSection("marks")]
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
    [PortalSection("marks")]
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
    [PortalSection("lectures")]
    public IActionResult Lectures(int studentId, DateTime? date)
    {
        var phone = HttpContext.Session.GetParentPhone() ?? "";
        var child = parentSvc.GetChildren(phone).FirstOrDefault(c => c.StudentId == studentId);
        if (child == null) return NotFound(new { error = "Student not found for this parent." });
        return Json(LectureHelpers.ParentPayload(lectureSvc, child, date ?? DateTime.Today));
    }

    // ── Notifications (what happened to my children) ─────────────
    [RequireParentLogin]
    [PortalSection("notifications")]
    public IActionResult Notifications(int? studentId)
    {
        var phone = HttpContext.Session.GetParentPhone() ?? "";
        var children = parentSvc.GetChildren(phone);
        var selected = studentId.HasValue ? children.FirstOrDefault(c => c.StudentId == studentId.Value) : null;

        List<SchoolMS.Domain.StudentActivity> items;
        try
        {
            items = activity.ForParent(phone, selected?.StudentId, 150, portal.Hidden());
            activity.MarkParentSeen(phone);   // the list below still shows what was new; the bell badge clears
        }
        catch { items = new(); }

        ViewBag.Children = children;
        ViewBag.SelectedChild = selected;
        ViewBag.FilterStudentId = selected?.StudentId;
        ViewBag.SelectedStudentId = children.FirstOrDefault()?.StudentId;
        ViewBag.SidebarChildren = children;
        ViewBag.ActiveTab = "notifications";
        return View(items);
    }

    [HttpGet]
    [RequireParentLogin]
    [PortalSection("notifications")]
    public IActionResult NotificationCount()
    {
        try { return Json(new { unreadCount = activity.UnreadForParent(HttpContext.Session.GetParentPhone() ?? "", portal.Hidden()) }); }
        catch { return Json(new { unreadCount = 0 }); }
    }

    // ── Notes & practice papers of my child's class ──────────────
    [RequireParentLogin]
    [PortalSection("notes")]
    public IActionResult Notes(int? studentId)
    {
        var phone = HttpContext.Session.GetParentPhone() ?? "";
        var children = parentSvc.GetChildren(phone);
        var selected = studentId.HasValue ? children.FirstOrDefault(c => c.StudentId == studentId.Value) : children.FirstOrDefault();

        List<SchoolMS.Domain.ClassNote> items = new();
        if (selected != null)
        {
            try
            {
                items = notesSvc.Search(new SchoolMS.Domain.ClassNoteFilter {
                    ForStudentClassId = selected.ClassId ?? 0, ForStudentSectionId = selected.SectionId, ForStudentBatchId = selected.BatchId });
            }
            catch { }
        }
        ViewBag.SelectedStudentId = selected?.StudentId;
        ViewBag.SidebarChildren = children;
        ViewBag.ActiveTab = "notes";
        return View(items);
    }

    [RequireParentLogin]
    [PortalSection("notes")]
    public IActionResult NoteFile(int id, int? studentId)
    {
        var phone = HttpContext.Session.GetParentPhone() ?? "";
        var note = notesSvc.GetById(id);
        if (note == null) return NotFound();
        var allowed = parentSvc.GetChildren(phone).Any(c => ClassNotesService.AppliesTo(note, c.ClassId, c.SectionId, c.BatchId));
        if (!allowed) return Forbid();
        var fs = noteFiles.Open(note.StoredName);
        if (fs == null) return NotFound();
        return File(fs, SchoolMS.Web.Auth.NoteFileStore.ContentType(note.FileName), note.FileName);
    }

    // ── Contact Us ──────────────────────────────────────────────
    [RequireParentLogin]
    [PortalSection("contact")]
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
    [PortalSection("contact")]
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
