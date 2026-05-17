using SchoolMS.DB;
using SchoolMS.Repository;
using SchoolMS.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllersWithViews();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(o => {
    o.IdleTimeout = TimeSpan.FromHours(8);
    o.Cookie.HttpOnly = true;
    o.Cookie.IsEssential = true;
    o.Cookie.Name = ".SchoolMS.Session";
});

var cs = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddSingleton(new CommonConnectivity(cs));

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

var app = builder.Build();
if (!app.Environment.IsDevelopment()) { app.UseExceptionHandler("/Home/Error"); app.UseHsts(); }
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseSession();
app.UseAuthorization();
app.MapControllerRoute("default", "{controller=Account}/{action=Login}/{id?}");
app.Run();
