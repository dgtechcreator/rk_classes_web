# CWC Invoice Parser - Quick Start Guide

## 30 Seconds Setup

### 1. Build Solution
```bash
# In project root
dotnet build
```

### 2. Update Database
Choose one:

**Option A** (Easiest - Automatic on Startup)
- Just run the application - migrations apply automatically

**Option B** (Package Manager Console)
```powershell
# Visual Studio → Tools → NuGet Package Manager → Package Manager Console
Update-Database -Context CwcInvoiceDbContext
```

**Option C** (CLI)
```bash
dotnet ef database update --context CwcInvoiceDbContext
```

### 3. Run Application
```bash
dotnet run --project SchoolMS.Web
```

### 4. Access Upload
Navigate to: **http://localhost:5000/CwcInvoice/Upload**

---

## First Invoice Upload

### Step 1: Open Upload Page
- Go to **CwcInvoice → Upload New Invoice**

### Step 2: Select PDF
- Drag & drop your invoice PDF, or click to browse
- File must be `.pdf` format
- Tested with Argosy Infra and CWC invoices

### Step 3: Parse & Import
- Click **"Parse & Import Invoice"** button
- Wait for processing (usually 1-2 seconds)
- You'll see success/error message

### Step 4: View Invoice
- System auto-redirects to details page on success
- Shows extracted data in three tables:
  - **Invoice Header** - Metadata and tax summary
  - **Container Details** - All containers with charges
  - **Charge Details** - Line items and tax breakdown

---

## Test with Reference PDFs

Two sample invoices are provided:
- **KALYANI LOGISTICS 014.pdf** - Single container
- **22451 2627 08709.pdf** - 14 containers (multi-container test)

Expected extraction:
```
✓ Invoice number (e.g., AI/I/2627/00263)
✓ Invoice date (e.g., 16-05-2026)
✓ Receiver name (e.g., KALYANI LOGISTICS)
✓ Consignee name (e.g., RAVI FOODS PVT LTD)
✓ Shipping line (e.g., WAN HAI LINES INDIA PVT. LTD.)
✓ BL No (Bill of Lading number)
✓ BOE No & Date (Bill of Entry)
✓ Container count (T20/T40/T45 breakdown)
✓ All container details
✓ All charge items with HSN/SAC codes
✓ Tax calculations (CGST, SGST, IGST)
```

---

## File Structure

```
CwcInvoice/
├── Controllers/
│   └── CwcInvoiceController.cs       ← HTTP handlers
├── Views/CwcInvoice/
│   ├── Index.cshtml                 ← Invoice list
│   ├── Details.cshtml               ← Invoice details
│   └── Upload.cshtml                ← PDF upload form
└── wwwroot/css/
    └── cwc-invoices.css             ← Styling
```

---

## Troubleshooting Common Issues

### Issue: "Invalid column name" on upload
**Solution**: Run migrations
```powershell
Update-Database -Context CwcInvoiceDbContext
```

### Issue: "Failed to read PDF file"
**Cause**: Corrupted or image-based PDF  
**Solution**: Use text-based PDFs only. Verify with online PDF validators.

### Issue: Some invoice fields are blank
**Cause**: PDF layout differs from expected format  
**Solution**: This is by design! Parser is flexible, but may miss fields in heavily custom formats. Check error logs.

### Issue: Container count is wrong
**Cause**: PDF uses non-standard section headers  
**Solution**: Edit `FindLineContaining()` in `PdfParsingService.cs` to match your PDF's headers.

---

## Project Structure Overview

```
SchoolMS2/
├── SchoolMS.Domain/              ← Database models
│   └── CwcInvoiceModels.cs
├── SchoolMS.DB/                  ← Database layer
│   ├── CwcInvoiceDbContext.cs
│   └── Migrations/
├── SchoolMS.Repository/          ← Data access
│   └── CwcInvoiceRepository.cs
├── SchoolMS.Services/            ← Business logic
│   ├── PdfParsingService.cs      ← ⭐ PDF extraction magic
│   └── CwcInvoiceService.cs
└── SchoolMS.Web/                 ← Web application
    ├── Controllers/
    │   └── CwcInvoiceController.cs
    ├── Views/CwcInvoice/
    └── wwwroot/css/
        └── cwc-invoices.css
```

---

## Key Technologies

| Component | Technology | Version |
|-----------|-----------|---------|
| Framework | .NET | 8.0 |
| Web Framework | ASP.NET Core MVC | 8.0 |
| Database | SQL Server | 2019+ |
| ORM | Entity Framework Core | 8.0 |
| PDF Parsing | iText7 | 8.0.4 |

---

## How Extraction Works (High Level)

```
PDF File
    ↓
[1] Extract Text (iText7)
    ↓ (raw text lines)
[2] Find Sections (keyword search)
    ↓ (Invoice Header, Containers, Bill Items, Tax)
[3] Parse Fields (regex + type conversion)
    ↓ (numbers, dates, decimals)
[4] Build Objects (create entities)
    ↓ (CwcInvoiceHeader + children)
[5] Save Database (EF Core)
    ↓
Invoice Ready ✓
```

### Why This Approach?
- **Not hardcoded**: Works with different PDF layouts
- **Flexible**: Adapts to supplier variations
- **Robust**: Handles missing/extra fields gracefully
- **Extensible**: Easy to add new field extraction rules

---

## Next Steps

### Explore the System
1. Upload a test PDF
2. View extracted data in Details page
3. Check SQL Server database tables (3 main tables):
   - `CwcInvoiceHeaders` (main invoice)
   - `CwcContainerDetails` (containers)
   - `CwcBillLineItems` (charges)

### Customize for Your Invoices
1. Identify PDF section headers in your invoices
2. Update keyword lists in `PdfParsingService.cs`
3. Test with a sample PDF
4. Adjust regex patterns if needed

### Enable Features (Future)
- Batch import UI
- Invoice search & filtering
- Data correction interface
- Excel export
- Custom field mappings

---

## Support

For detailed documentation: See `CWC_INVOICE_PARSER_README.md`

Quick troubleshooting:
1. Check application logs (Visual Studio Output window)
2. Verify PDF structure (open in Acrobat Reader)
3. Validate database connection
4. Check SQL Server is running

---

**Ready to start?**
```
1. Build solution
2. Update database
3. Run application
4. Go to http://localhost:5000/CwcInvoice/Upload
5. Upload a test PDF
6. Done! ✓
```

---

**Last Updated**: June 10, 2026  
**Created for**: SchoolMS2 Project
