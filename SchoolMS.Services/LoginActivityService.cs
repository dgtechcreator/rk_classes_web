using SchoolMS.Domain;
using SchoolMS.Repository;

namespace SchoolMS.Services;

public class LoginActivityService(LoginActivityRepo repo)
{
    public void Add(LoginActivityEntry e) => repo.Add(e);

    public List<LoginActivityEntry> Query(string? accountType, int? accountId, string? search, DateTime? from, DateTime? to, bool? success, int take = 200)
        => repo.Query(accountType, accountId, search, from, to, success, take);

    /// <summary>A signed-in person's own history (newest first).</summary>
    public List<LoginActivityEntry> Mine(string accountType, int accountId, int take = 100)
        => repo.Query(accountType, accountId, null, null, null, null, take);

    public List<DeviceUse> Devices(string accountType, int accountId) => repo.Devices(accountType, accountId);

    public void EnsureSchema() => repo.EnsureSchema();
}
