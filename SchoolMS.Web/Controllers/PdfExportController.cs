using Microsoft.AspNetCore.Mvc;
using SchoolMS.Web.Utils;
using System.IO;

namespace SchoolMS.Web.Controllers
{
    public class PdfExportController : Controller
    {
        /// <summary>
        /// Convert PDF to Excel and download
        /// Usage: /PdfExport/ConvertAndDownload?pdfPath=D:\path\to\file.pdf
        /// </summary>
        [HttpPost]
        public IActionResult ConvertAndDownload([FromForm] IFormFile pdfFile)
        {
            try
            {
                if (pdfFile == null || pdfFile.Length == 0)
                    return BadRequest("No file uploaded");

                if (!pdfFile.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                    return BadRequest("Only PDF files are allowed");

                // Save uploaded PDF temporarily
                var tempPdfPath = Path.Combine(Path.GetTempPath(), $"temp_{Guid.NewGuid()}.pdf");
                using (var stream = new FileStream(tempPdfPath, FileMode.Create))
                {
                    pdfFile.CopyTo(stream);
                }

                // Convert to Excel
                var tempExcelPath = Path.Combine(Path.GetTempPath(), $"Invoice_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");
                PdfToExcelConverter.ConvertPdfToExcel(tempPdfPath, tempExcelPath);

                // Read Excel file
                var fileBytes = System.IO.File.ReadAllBytes(tempExcelPath);

                // Clean up temporary files
                System.IO.File.Delete(tempPdfPath);
                System.IO.File.Delete(tempExcelPath);

                // Download
                return File(fileBytes,
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    $"Invoice_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");
            }
            catch (Exception ex)
            {
                return BadRequest($"Error converting PDF: {ex.Message}");
            }
        }

        /// <summary>
        /// Convert PDF from file path and download
        /// Usage: POST /PdfExport/ConvertFromPath
        /// Body: { "pdfPath": "D:\\path\\to\\file.pdf" }
        /// </summary>
        [HttpPost]
        public IActionResult ConvertFromPath([FromBody] ConvertRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request?.PdfPath))
                    return BadRequest("PDF path is required");

                if (!System.IO.File.Exists(request.PdfPath))
                    return NotFound($"PDF file not found: {request.PdfPath}");

                // Convert to Excel
                var excelPath = Path.Combine(Path.GetTempPath(), $"Invoice_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");
                PdfToExcelConverter.ConvertPdfToExcel(request.PdfPath, excelPath);

                // Read Excel file
                var fileBytes = System.IO.File.ReadAllBytes(excelPath);

                // Clean up
                System.IO.File.Delete(excelPath);

                return File(fileBytes,
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    $"Invoice_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");
            }
            catch (Exception ex)
            {
                return BadRequest($"Error converting PDF: {ex.Message}");
            }
        }
    }

    public class ConvertRequest
    {
        public string PdfPath { get; set; }
    }
}
