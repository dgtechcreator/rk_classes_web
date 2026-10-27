using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SchoolMS.DB;
using SchoolMS.Repository;
using SchoolMS.Services;
using SchoolMS.Web.Auth;
using SchoolMS.Web.Hubs;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<SchoolMS.Web.Auth.UserAccessCache>();
builder.Services.AddSingleton<SchoolMS.Web.Auth.ProfilePhotoStore>();
builder.Services.AddSingleton<SchoolMS.Web.Auth.NoteFileStore>();
builder.Services.AddSingleton<AppSettingsRepo>();
builder.Services.AddSingleton<MaintenanceService>();
builder.Services.AddControllersWithViews(o => {
    o.Filters.Add<SchoolMS.Web.Auth.ClassRepGuardFilter>();
    o.Filters.Add<SchoolMS.Web.Auth.PagePermissionFilter>();
});

// JWT auth — mobile app only. Existing MVC controllers keep using session-cookie auth (Filters/AuthFilters.cs)
// unaffected; this scheme only activates for requests carrying an Authorization: Bearer header (or ?token=
// for PDF/file links opened directly in a browser, e.g. via url_launcher on mobile).
builder.Services.AddScoped<JwtTokenService>();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o => {
        o.TokenValidationParameters = new TokenValidationParameters {
            ValidateIssuer           = true,
            ValidIssuer              = builder.Configuration["Jwt:Issuer"],
            ValidateAudience         = true,
            ValidAudience            = builder.Configuration["Jwt:Audience"],
            ValidateIssuerSigningKey = true,
            IssuerSigningKey         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Secret"]!)),
            ValidateLifetime         = true,
            ClockSkew                = TimeSpan.FromMinutes(2),
        };
        o.Events = new JwtBearerEvents {
            // Rights / role / on-off are read live, not from the (up to 30 day old) token.
            OnTokenValidated = ctx => { ctx.RefreshToken(); return Task.CompletedTask; },
            OnMessageReceived = ctx => {
                var path = ctx.HttpContext.Request.Path;
                var qsToken = ctx.Request.Query["token"];
                if (!string.IsNullOrEmpty(qsToken) && path.StartsWithSegments("/api"))
                    ctx.Token = qsToken;

                // SignalR: the WebSocket/SSE handshake can't carry an Authorization header, so the
                // client sends the JWT via ?access_token= instead (standard ASP.NET Core SignalR pattern).
                var accessToken = ctx.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                    ctx.Token = accessToken;

                return Task.CompletedTask;
            }
        };
    });
builder.Services.AddAuthorization();
builder.Services.AddSignalR();
builder.Services.AddScoped<INotificationBroadcaster, SignalRNotificationBroadcaster>();

// Dev-only CORS so the Flutter web build (used for local UI verification in a browser — native
// Android/iOS builds don't need this, they aren't subject to browser CORS) can call the API from a
// different localhost port. No-op in production (never registered as active middleware below).
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddCors(o => o.AddPolicy("DevMobileWeb", p => p
        .SetIsOriginAllowed(origin => Uri.TryCreate(origin, UriKind.Absolute, out var u) && (u.Host == "localhost" || u.Host == "127.0.0.1"))
        .AllowAnyMethod()
        .AllowAnyHeader()));
}

// Trust reverse-proxy headers (IIS / Nginx forwarding HTTPS as HTTP internally)
builder.Services.Configure<ForwardedHeadersOptions>(o => {
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    o.KnownNetworks.Clear();
    o.KnownProxies.Clear();
});

// Session — stored in SQL Server (survives App Pool restarts / server reboots)
var connStr = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
try {
    using (var testConn = new Microsoft.Data.SqlClient.SqlConnection(connStr)) { testConn.Open(); }
    builder.Services.AddDistributedSqlServerCache(o => {
        o.ConnectionString = connStr;
        o.SchemaName       = "dbo";
        o.TableName        = "SessionCache";
    });
} catch {
    builder.Services.AddDistributedMemoryCache();
}
builder.Services.AddSession(o => {
    o.IdleTimeout        = TimeSpan.FromHours(8);
    o.Cookie.HttpOnly    = true;
    o.Cookie.IsEssential = true;
    o.Cookie.Name        = ".SchoolMS.Session";
    o.Cookie.SameSite    = Microsoft.AspNetCore.Http.SameSiteMode.Lax;
});

builder.Services.AddSingleton(new CommonConnectivity(connStr));

// Repos
builder.Services.AddScoped<AuthRepo>();
builder.Services.AddScoped<LookupRepo>();
builder.Services.AddScoped<StudentRepo>();
builder.Services.AddScoped<AttendanceRepo>();
builder.Services.AddScoped<MarksRepo>();
builder.Services.AddScoped<FeesRepo>();
builder.Services.AddScoped<ExpensesRepo>();
builder.Services.AddScoped<FeeStructureRepo>();
builder.Services.AddScoped<MastersRepo>();
builder.Services.AddScoped<FacultyRepo>();
builder.Services.AddScoped<ClassFeeSetupRepo>();
builder.Services.AddScoped<UserMgmtRepo>();
builder.Services.AddScoped<ParentRepo>();
builder.Services.AddScoped<TaskRepo>();
builder.Services.AddScoped<NotificationRepo>();
builder.Services.AddScoped<ActivityRepo>();
builder.Services.AddScoped<ClassNotesRepo>();
builder.Services.AddScoped<ClassNotesService>();
builder.Services.AddScoped<AttendanceBatchRepo>();
builder.Services.AddScoped<TeacherPaymentRepo>();
builder.Services.AddScoped<TeacherAttendanceRepo>();
builder.Services.AddScoped<LectureRepo>();
builder.Services.AddScoped<DataHealthRepo>();
builder.Services.AddScoped<ClassRepRepo>();

// Services
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<LookupService>();
builder.Services.AddScoped<StudentService>();
builder.Services.AddScoped<AttendanceService>();
builder.Services.AddScoped<MarksService>();
builder.Services.AddScoped<FeesService>();
builder.Services.AddScoped<ExpensesService>();
builder.Services.AddScoped<FeeStructureService>();
builder.Services.AddScoped<MastersService>();
builder.Services.AddScoped<FacultyService>();
builder.Services.AddScoped<ClassFeeSetupService>();
builder.Services.AddScoped<UserMgmtService>();
builder.Services.AddScoped<ParentService>();
builder.Services.AddScoped<TaskService>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<ActivityNotifier>();
builder.Services.AddScoped<AttendanceBatchService>();
builder.Services.AddScoped<TeacherPaymentService>();
builder.Services.AddScoped<TeacherAttendanceService>();
builder.Services.AddScoped<LectureService>();
builder.Services.AddScoped<FeePositionService>();
builder.Services.AddScoped<DataHealthService>();
builder.Services.AddScoped<TeacherAccountLinker>();
builder.Services.AddScoped<ClassRepService>();
builder.Services.AddScoped<StaffNameService>();
builder.Services.AddScoped<DeletedRecordsService>();

var app = builder.Build();

// Auto-create SessionCache table if it doesn't exist (runs once on startup, safe to repeat)
try {
    using var sqlConn = new Microsoft.Data.SqlClient.SqlConnection(connStr);
    sqlConn.Open();
    var cmd = sqlConn.CreateCommand();
    cmd.CommandText = @"
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA='dbo' AND TABLE_NAME='SessionCache')
BEGIN
    CREATE TABLE [dbo].[SessionCache] (
        [Id]                         NVARCHAR(449) COLLATE SQL_Latin1_General_CP1_CS_AS NOT NULL,
        [Value]                      VARBINARY(MAX) NOT NULL,
        [ExpiresAtTime]              DATETIMEOFFSET(7) NOT NULL,
        [SlidingExpirationInSeconds] BIGINT NULL,
        [AbsoluteExpiration]         DATETIMEOFFSET(7) NULL,
        CONSTRAINT [pk_Id] PRIMARY KEY CLUSTERED ([Id] ASC)
    );
    CREATE NONCLUSTERED INDEX [Index_ExpiresAtTime] ON [dbo].[SessionCache]([ExpiresAtTime] ASC);
END";
    cmd.ExecuteNonQuery();
} catch { /* table already exists or DB unreachable — continue anyway */ }

// MUST be first — reads X-Forwarded-Proto so HTTPS is detected correctly behind IIS/Nginx
app.UseForwardedHeaders();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
    app.UseHttpsRedirection(); // only redirect HTTP→HTTPS in production, after proxy headers are read
}

app.UseStaticFiles();

// Clear old cookies from browser
app.Use(async (context, next) => {
    // Remove any old authentication cookies
    context.Response.Cookies.Delete(".SchoolMS.Auth");
    context.Response.Cookies.Delete(".AspNetCore.Session");
    await next();
});

app.UseSession();

// Maintenance mode (website > App Settings): every mobile API call is answered with 503 + the message, except the
// public settings call the app uses to learn the switch is off again. Even app versions without the maintenance
// screen show that message (the API client reads the "error" field). The website itself is not affected.
app.Use(async (context, next) => {
    var path = context.Request.Path;
    if (path.StartsWithSegments("/api") && !path.StartsWithSegments("/api/app-config"))
    {
        var m = context.RequestServices.GetRequiredService<MaintenanceService>().Get();
        if (m.Enabled)
        {
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            context.Response.Headers.RetryAfter = "300";
            await context.Response.WriteAsJsonAsync(new { error = m.Message, maintenance = true });
            return;
        }
    }
    await next();
});

// Keep the web session in step with the DB: changed rights apply on the next click, and a user who was
// deactivated / deleted / switched off is signed out immediately instead of at the next login.
app.Use(async (context, next) => {
    var path = context.Request.Path;
    if (!path.StartsWithSegments("/api") && !path.StartsWithSegments("/hubs"))
    {
        try { context.RefreshSession(context.RequestServices.GetRequiredService<SchoolMS.Web.Auth.UserAccessCache>()); }
        catch { /* never block a request because the refresh failed */ }
    }
    await next();
});

// Catch ANY 403/401 → redirect to Login (but avoid redirect loop)
app.UseStatusCodePages(async ctx => {
    var code = ctx.HttpContext.Response.StatusCode;
    var path = ctx.HttpContext.Request.Path.Value?.ToLower() ?? "";

    // Don't redirect if already on login page to avoid infinite loop
    if ((code == 401 || code == 403) && !path.Contains("/account/login") && !ctx.HttpContext.Response.HasStarted)
    {
        ctx.HttpContext.Response.Clear();
        ctx.HttpContext.Response.Redirect("/Account/Login");
    }
    await Task.CompletedTask;
});

app.UseRouting();
if (app.Environment.IsDevelopment()) app.UseCors("DevMobileWeb");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllerRoute("default", "{controller=Account}/{action=Login}/{id?}");
app.MapHub<NotificationHub>("/hubs/notifications");
// Tables / module rows of the newer features (idempotent; a failure here must never stop the site).
try
{
    using var scope = app.Services.CreateScope();
    scope.ServiceProvider.GetRequiredService<ClassNotesRepo>().EnsureSchema();
    scope.ServiceProvider.GetRequiredService<ActivityRepo>().EnsureSchema();
}
catch (Exception ex) { Console.Error.WriteLine("Start-up schema check failed: " + ex.Message); }

app.Run();
