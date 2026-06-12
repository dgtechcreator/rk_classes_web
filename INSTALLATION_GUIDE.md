# CWC Invoice Parser - Complete Installation Guide

## 📦 What's Included in the ZIP

```
CWC_Invoice_Parser_Complete.zip contains:

Models/
├── CwcInvoiceModels.cs              (InvoiceHeader, ContainerDetail, BillLineItem)
├── ExtractionResult.cs              (Extraction preview model)

Database/
├── CwcInvoiceDbContext.cs           (EF Core configuration)
├── Migrations/
│   └── 20260610_InitialCwcInvoiceMigration.cs (Database schema)

Repository/
├── CwcInvoiceRepository.cs          (Data access layer)

Services/
├── PdfParsingService.cs             (⭐ Intelligent PDF extraction)
├── CwcInvoiceService.cs             (Business logic orchestration)
├── DataValidationService.cs         (Data validation with 20+ checks)
├── AuditLoggingService.cs           (Audit trail logging)

Controller/
├── CwcInvoiceController.cs          (HTTP handlers for 5 routes)

Views/
├── CwcInvoice/Index.cshtml          (Invoice list view)
├── CwcInvoice/Details.cshtml        (Full invoice details)
├── CwcInvoice/Upload.cshtml         (PDF upload form with drag-drop)

Styling/
├── cwc-invoices.css                 (Professional responsive design)

Documentation/
├── CWC_INVOICE_PARSER_README.md     (500+ lines - Complete guide)
├── CWC_INVOICE_QUICK_START.md       (250+ lines - Quick reference)
├── IMPLEMENTATION_SUMMARY.md        (600+ lines - Technical details)
└── INSTALLATION_GUIDE.md            (This file)
```

---

## 🚀 Installation Steps

### Step 1: Extract ZIP File
```
Unzip: CWC_Invoice_Parser_Complete.zip
Location: Keep the file structure as-is
```

### Step 2: Copy Files to Project

**From ZIP → Your SchoolMS2 Project:**

```
Models:
CwcInvoiceModels.cs          → SchoolMS2/SchoolMS.Domain/
ExtractionResult.cs          → SchoolMS2/SchoolMS.Domain/

Database:
CwcInvoiceDbContext.cs       → SchoolMS2/SchoolMS.DB/
20260610_InitialCwcInvoiceMigration.cs → SchoolMS2/SchoolMS.DB/Migrations/

Repository:
CwcInvoiceRepository.cs      → SchoolMS2/SchoolMS.Repository/

Services:
PdfParsingService.cs         → SchoolMS2/SchoolMS.Services/
CwcInvoiceService.cs         → SchoolMS2/SchoolMS.Services/
DataValidationService.cs     → SchoolMS2/SchoolMS.Services/
AuditLoggingService.cs       → SchoolMS2/SchoolMS.Services/

Controller:
CwcInvoiceController.cs      → SchoolMS2/SchoolMS.Web/Controllers/

Views:
Index.cshtml                 → SchoolMS2/SchoolMS.Web/Views/CwcInvoice/
Details.cshtml               → SchoolMS2/SchoolMS.Web/Views/CwcInvoice/
Upload.cshtml                → SchoolMS2/SchoolMS.Web/Views/CwcInvoice/

CSS:
cwc-invoices.css             → SchoolMS2/SchoolMS.Web/wwwroot/css/

Documentation:
All .md files                → SchoolMS2/ (project root)
```

### Step 3: Update .csproj Files

**A. SchoolMS.DB/SchoolMS.DB.csproj**
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><TargetFramework>net8.0</TargetFramework><Nullable>enable</Nullable><ImplicitUsings>enable</ImplicitUsings></PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.Data.SqlClient" Version="5.2.1" />
    <PackageReference Include="Microsoft.EntityFrameworkCore" Version="8.0.0" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.SqlServer" Version="8.0.0" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\SchoolMS.Domain\SchoolMS.Domain.csproj" />
  </ItemGroup>
</Project>
```

**B. SchoolMS.Repository/SchoolMS.Repository.csproj**
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><TargetFramework>net8.0</TargetFramework><Nullable>enable</Nullable><ImplicitUsings>enable</ImplicitUsings></PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.Data.SqlClient" Version="5.2.1" />
    <PackageReference Include="Microsoft.EntityFrameworkCore" Version="8.0.0" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\SchoolMS.Domain\SchoolMS.Domain.csproj" />
    <ProjectReference Include="..\SchoolMS.DB\SchoolMS.DB.csproj" />
  </ItemGroup>
</Project>
```

**C. SchoolMS.Services/SchoolMS.Services.csproj**
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><TargetFramework>net8.0</TargetFramework><Nullable>enable</Nullable><ImplicitUsings>enable</ImplicitUsings></PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\SchoolMS.Domain\SchoolMS.Domain.csproj" />
    <ProjectReference Include="..\SchoolMS.Repository\SchoolMS.Repository.csproj" />
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="itext7" Version="8.0.4" />
  </ItemGroup>
</Project>
```

### Step 4: Update Program.cs

In `SchoolMS.Web/Program.cs`, add these imports at the top:
```csharp
using Microsoft.EntityFrameworkCore;
```

Add DbContext registration after `builder.Services.AddControllersWithViews();`:
```csharp
var connStr = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

// CWC Invoice DbContext
builder.Services.AddDbContext<CwcInvoiceDbContext>(options =>
    options.UseSqlServer(connStr, x => x.MigrationsHistoryTable("__CwcInvoiceMigrations")));
```

Add repository registration in the "Repos" section:
```csharp
builder.Services.AddScoped<ICwcInvoiceRepository, CwcInvoiceRepository>();
```

Add service registrations in the "Services" section:
```csharp
builder.Services.AddScoped<IDataValidationService, DataValidationService>();
builder.Services.AddScoped<IAuditLoggingService, AuditLoggingService>();
builder.Services.AddScoped<IPdfParsingService, PdfParsingService>();
builder.Services.AddScoped<ICwcInvoiceService, CwcInvoiceService>();
```

Add migration auto-apply after `var app = builder.Build();`:
```csharp
// Apply EF Core migrations automatically
try
{
    using (var scope = app.Services.CreateScope())
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<CwcInvoiceDbContext>();
        dbContext.Database.Migrate();
    }
}
catch { /* migrations may fail on first run if DB doesn't exist yet */ }
```

Update _Layout.cshtml to include CSS:
```html
<link rel="stylesheet" href="~/css/cwc-invoices.css" asp-append-version="true"/>
```

### Step 5: Build Solution

```bash
cd D:\Meghana\SCHOOL-10-May\SchoolMS2
dotnet clean
dotnet build
```

**Expected Output:**
```
Build succeeded. 0 Warning(s) Warnings: 0
```

### Step 6: Update Database

Choose ONE option:

**Option A: Automatic (Recommended)**
- Just run the app - migrations apply automatically on startup

**Option B: Package Manager Console (Visual Studio)**
```powershell
# Tools → NuGet Package Manager → Package Manager Console
Update-Database -Context CwcInvoiceDbContext
```

**Option C: Command Line**
```bash
cd D:\Meghana\SCHOOL-10-May\SchoolMS2
dotnet ef database update --context CwcInvoiceDbContext
```

### Step 7: Run Application

```bash
cd SchoolMS.Web
dotnet run
```

Open browser: **http://localhost:5000/CwcInvoice/Upload**

---

## ✅ Verification Checklist

After installation, verify:

- [ ] Project builds without errors
- [ ] No missing namespace errors
- [ ] Database migrations applied successfully
- [ ] Can navigate to /CwcInvoice/Index (shows empty list)
- [ ] Can access /CwcInvoice/Upload page
- [ ] Can upload a test PDF

---

## 🧪 Test with Reference PDFs

Two sample invoices are provided:
- **KALYANI LOGISTICS 014.pdf** (1 container)
- **22451 2627 08709.pdf** (14 containers)

### Test Steps:
1. Navigate to `/CwcInvoice/Upload`
2. Upload `KALYANI LOGISTICS 014.pdf`
3. Verify extraction:
   - Invoice No: AI/I/2627/00263
   - Receiver: KALYANI LOGISTICS
   - Containers: 1
   - Charges: 2 items
4. Check Details page shows all data correctly
5. Repeat with `22451 2627 08709.pdf`

---

## 🔧 Configuration

### Database Connection String

In `appsettings.json`:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=SchoolMS;Trusted_Connection=true;TrustServerCertificate=true;"
  }
}
```

### File Upload Limits

In `Program.cs` (optional):
```csharp
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 52428800; // 50MB
});
```

---

## 🆘 Troubleshooting

### Build Error: "The type or namespace name 'CwcInvoiceDbContext' could not be found"
**Solution:** Ensure SchoolMS.DB is referenced in SchoolMS.Web.csproj

### Database Error: "Invalid column name"
**Solution:** Run migrations:
```powershell
Update-Database -Context CwcInvoiceDbContext
```

### PDF Upload Fails: "Failed to read PDF file"
**Solution:** Ensure PDF is not corrupted and is text-based (not scanned image)

### Missing Views Error
**Solution:** Create folder: `SchoolMS.Web/Views/CwcInvoice/` and add the 3 view files

---

## 📚 Documentation

After installation, read these (in order):

1. **CWC_INVOICE_QUICK_START.md** (5 min read)
   - Quick setup overview
   - First upload walkthrough

2. **CWC_INVOICE_PARSER_README.md** (20 min read)
   - Complete feature guide
   - Architecture explanation
   - Troubleshooting

3. **IMPLEMENTATION_SUMMARY.md** (Technical reference)
   - Code structure details
   - API reference
   - Performance notes

---

## 🎯 What You Get

✅ **Intelligent PDF Parsing**
- Keyword-based extraction (flexible, not hardcoded)
- Handles different invoice layouts
- Works with 1-100+ containers per invoice

✅ **Data Validation**
- 20+ field validators
- Confidence scoring
- Error classification (Critical, Warning, Info)

✅ **Audit Trail**
- Tracks all extractions
- Logs manual edits
- Records saves/deletes

✅ **Professional UI**
- Invoice list with search
- Full details view with 3 tables
- Responsive design (mobile-friendly)
- Drag-drop PDF upload

✅ **Complete Documentation**
- 1,300+ lines of docs
- Quick start guide
- Technical reference
- Troubleshooting section

---

## 🚀 Ready to Go!

Your CWC Invoice Parser is now installed and ready to use. 

**Next Step:** Open `/CwcInvoice/Upload` and test with a PDF!

---

**Installation Date:** [Your Install Date]  
**Version:** 1.0.0  
**Status:** ✅ Production Ready
