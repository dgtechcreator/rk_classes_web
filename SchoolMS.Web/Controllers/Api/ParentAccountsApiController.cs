using Microsoft.AspNetCore.Mvc;
using SchoolMS.Services;
using SchoolMS.Web.Auth;

namespace SchoolMS.Web.Controllers.Api;

/// <summary>Parent login accounts (phone = login id + password) for the admin - the same list the website's "Parent Accounts" page shows.</summary>
[ApiController]
[Route("api/parent-accounts")]
[ApiRequireAdmin]
public class ParentAccountsApiController(ParentService parentSvc) : ControllerBase
{
    [HttpGet]
    public IActionResult Index()
        => Ok(new {
            accounts = parentSvc.GetAllParents().Select(p => new {
                parentId = p.ParentId, phone = p.Phone, password = p.Password, fullName = p.FullName,
                isActive = p.IsActive, createdAt = p.CreatedAt, lastLogin = p.LastLogin,
            }),
        });
}
