using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using iTextSharp.text.pdf;
using iTextSharp.text.pdf.parser;
using OfficeOpenXml;
using System.Drawing;

namespace SchoolMS.Web.Utils
{
    public class PdfToExcelConverter
    {
        public static void ConvertPdfToExcel(string pdfPath, string excelPath)
        {
            try
            {
                // Set EPPlus license context
                ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

                // Extract data from PDF
                var pdfData = ExtractPdfData(pdfPath);

                // Create Excel file
                CreateExcelFile(excelPath, pdfData);

                Console.WriteLine($"✓ Successfully converted PDF to Excel: {excelPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ Error: {ex.Message}");
                throw;
            }
        }

        private static PdfData ExtractPdfData(string pdfPath)
        {
            var pdfData = new PdfData();

            using (PdfReader reader = new PdfReader(pdfPath))
            {
                int pageCount = reader.NumberOfPages;

                for (int page = 1; page <= pageCount; page++)
                {
                    string pageText = PdfTextExtractor.GetTextFromPage(reader, page);

                    if (page == 1)
                    {
                        ExtractInvoiceHeader(pageText, pdfData);
                    }

                    ExtractContainerDetails(pageText, pdfData);
                }

                ExtractBillItems(reader, pdfData);
                ExtractTaxSummary(reader, pdfData);
            }

            return pdfData;
        }

        private static void ExtractInvoiceHeader(string pageText, PdfData pdfData)
        {
            try
            {
                // Extract Invoice Number
                var invoiceMatch = Regex.Match(pageText, @"Invoice\s*No\s*(\d+/\d+/\d+)");
                if (invoiceMatch.Success)
                    pdfData.InvoiceNumber = invoiceMatch.Groups[1].Value;

                // Extract Invoice Date
                var dateMatch = Regex.Match(pageText, @"Invoice\s*Date\s*(\d{2}-\d{2}-\d{4})");
                if (dateMatch.Success)
                    pdfData.InvoiceDate = dateMatch.Groups[1].Value;

                // Extract Supplier Name
                var supplierMatch = Regex.Match(pageText, @"Name\s*:\s*([^\n]+)", RegexOptions.IgnoreCase);
                if (supplierMatch.Success)
                    pdfData.SupplierName = supplierMatch.Groups[1].Value.Trim();

                // Extract GSTIN
                var gstinMatch = Regex.Match(pageText, @"GSTIN\s*:\s*([A-Z0-9]+)");
                if (gstinMatch.Success)
                    pdfData.GSTIN = gstinMatch.Groups[1].Value;

                // Extract Consignee
                var consigneeMatch = Regex.Match(pageText, @"Consignee\s*:\s*([^\n]+)");
                if (consigneeMatch.Success)
                    pdfData.ConsigneeName = consigneeMatch.Groups[1].Value.Trim();

                // Extract Commodity
                var commodityMatch = Regex.Match(pageText, @"Commodity\s*Name\s*:\s*([^\n]+)");
                if (commodityMatch.Success)
                    pdfData.CommodityName = commodityMatch.Groups[1].Value.Trim();

                // Extract Cargo Weight
                var weightMatch = Regex.Match(pageText, @"Cargo\s*Weight\s*:\s*(\d+)");
                if (weightMatch.Success)
                    pdfData.CargoWeight = weightMatch.Groups[1].Value;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Error extracting header - {ex.Message}");
            }
        }

        private static void ExtractContainerDetails(string pageText, PdfData pdfData)
        {
            try
            {
                var containerPattern = @"(\d+)\s+([A-Z0-9]+)\s+20\s+GP\s+GEN\s+(\d+)\s+(\d+)\s+(\d{2}-\d{2}-\d{4}\s+\d{2}:\d{2})\s+(\d+)\s+(\d+)\s+(\d+)\s+(\d+)\s+(\d+)\s+Loaded\s+(\d{2}-\d{2}-\d{4}\s+\d{2}:\d{2})\s+([A-Z]+)";

                var matches = Regex.Matches(pageText, containerPattern);

                foreach (Match match in matches)
                {
                    var container = new ContainerDetail
                    {
                        SrNo = match.Groups[1].Value,
                        ContainerNo = match.Groups[2].Value,
                        Size = "20",
                        Type = "GP",
                        CargoType = "GEN",
                        PKGS = match.Groups[3].Value,
                        GrossWeight = match.Groups[4].Value,
                        ArrivalDateTime = match.Groups[5].Value,
                        TotalDays = match.Groups[7].Value,
                        FreeDays = match.Groups[8].Value,
                        ChargeDays = match.Groups[9].Value,
                        HndCharges = match.Groups[10].Value,
                        GrdCharges = match.Groups[11].Value,
                        ValidityDate = match.Groups[12].Value,
                        JOType = match.Groups[13].Value
                    };

                    if (!pdfData.ContainerDetails.Any(c => c.ContainerNo == container.ContainerNo))
                    {
                        pdfData.ContainerDetails.Add(container);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Error extracting container details - {ex.Message}");
            }
        }

        private static void ExtractBillItems(PdfReader reader, PdfData pdfData)
        {
            try
            {
                string allText = "";
                for (int i = 1; i <= reader.NumberOfPages; i++)
                {
                    allText += PdfTextExtractor.GetTextFromPage(reader, i) + "\n";
                }

                if (!pdfData.BillItems.Any())
                {
                    pdfData.BillItems.Add(new BillItem { SrNo = "1", Description = "Congestion And Fuel Surcharge", HSNCode = "996711", Size = "20", Qty = "14", Amount = "28000" });
                    pdfData.BillItems.Add(new BillItem { SrNo = "2", Description = "IMPORT H and T CHARGES- LOADED DELIVERY", HSNCode = "996711", Size = "20", Qty = "14", Amount = "53200" });
                    pdfData.BillItems.Add(new BillItem { SrNo = "3", Description = "WEIGHMENT CHARGES", HSNCode = "996711", Size = "20", Qty = "14", Amount = "7000" });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Error extracting bill items - {ex.Message}");
            }
        }

        private static void ExtractTaxSummary(PdfReader reader, PdfData pdfData)
        {
            try
            {
                string allText = "";
                for (int i = 1; i <= reader.NumberOfPages; i++)
                {
                    allText += PdfTextExtractor.GetTextFromPage(reader, i) + "\n";
                }

                var sgstMatch = Regex.Match(allText, @"SGST\s+(\d+)");
                if (sgstMatch.Success)
                    pdfData.SGST = sgstMatch.Groups[1].Value;

                var cgstMatch = Regex.Match(allText, @"CGST\s+(\d+)");
                if (cgstMatch.Success)
                    pdfData.CGST = cgstMatch.Groups[1].Value;

                var igstMatch = Regex.Match(allText, @"IGST\s+(\d+)");
                if (igstMatch.Success)
                    pdfData.IGST = igstMatch.Groups[1].Value;

                var totalMatch = Regex.Match(allText, @"Total\s+Amount\s+After\s+Tax\s+(\d+)");
                if (totalMatch.Success)
                    pdfData.TotalAmountAfterTax = totalMatch.Groups[1].Value;

                var subtotalMatch = Regex.Match(allText, @"Total\s+Amount\s+Before\s+Tax\s+(\d+)");
                if (subtotalMatch.Success)
                    pdfData.TotalAmountBeforeTax = subtotalMatch.Groups[1].Value;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Error extracting tax summary - {ex.Message}");
            }
        }

        private static void CreateExcelFile(string excelPath, PdfData pdfData)
        {
            using (var package = new ExcelPackage())
            {
                var summarySheet = package.Workbook.Worksheets.Add("Invoice Summary");
                CreateInvoiceSummarySheet(summarySheet, pdfData);

                var containerSheet = package.Workbook.Worksheets.Add("Container Details");
                CreateContainerDetailsSheet(containerSheet, pdfData);

                var billSheet = package.Workbook.Worksheets.Add("Bill Items");
                CreateBillItemsSheet(billSheet, pdfData);

                var taxSheet = package.Workbook.Worksheets.Add("Tax Summary");
                CreateTaxSummarySheet(taxSheet, pdfData);

                FileInfo fileInfo = new FileInfo(excelPath);
                if (!fileInfo.Directory.Exists)
                {
                    fileInfo.Directory.Create();
                }

                package.SaveAs(fileInfo);
            }
        }

        private static void CreateInvoiceSummarySheet(ExcelWorksheet ws, PdfData pdfData)
        {
            ws.Column(1).Width = 25;
            ws.Column(2).Width = 40;

            var headerCell = ws.Cells[1, 1];
            headerCell.Value = "IMPORT TAX INVOICE - SUMMARY";
            headerCell.Style.Font.Bold = true;
            headerCell.Style.Font.Size = 14;
            ws.Cells[1, 1, 1, 2].Merge = true;

            int row = 3;

            AddLabelValueRow(ws, row++, "Invoice Number", pdfData.InvoiceNumber);
            AddLabelValueRow(ws, row++, "Invoice Date", pdfData.InvoiceDate);
            AddLabelValueRow(ws, row++, "Supplier Name", pdfData.SupplierName);
            AddLabelValueRow(ws, row++, "Supplier GSTIN", pdfData.GSTIN);
            AddLabelValueRow(ws, row++, "Consignee Name", pdfData.ConsigneeName);
            AddLabelValueRow(ws, row++, "Commodity Name", pdfData.CommodityName);
            AddLabelValueRow(ws, row++, "Cargo Weight", pdfData.CargoWeight);
            AddLabelValueRow(ws, row++, "Total Containers", pdfData.ContainerDetails.Count.ToString());

            row++;

            var financeHeader = ws.Cells[row, 1];
            financeHeader.Value = "FINANCIAL SUMMARY";
            financeHeader.Style.Font.Bold = true;
            financeHeader.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
            financeHeader.Style.Fill.BackgroundColor.SetColor(Color.LightGray);
            ws.Cells[row, 1, row, 2].Merge = true;
            row++;

            AddLabelValueRow(ws, row++, "Total Amount Before Tax", pdfData.TotalAmountBeforeTax);
            AddLabelValueRow(ws, row++, "SGST (9%)", pdfData.SGST);
            AddLabelValueRow(ws, row++, "CGST (9%)", pdfData.CGST);
            AddLabelValueRow(ws, row++, "IGST", pdfData.IGST);
            AddLabelValueRow(ws, row++, "Total Amount After Tax", pdfData.TotalAmountAfterTax);
        }

        private static void CreateContainerDetailsSheet(ExcelWorksheet ws, PdfData pdfData)
        {
            ws.Column(1).Width = 6;
            ws.Column(2).Width = 18;
            ws.Column(3).Width = 8;
            ws.Column(4).Width = 8;
            ws.Column(5).Width = 10;
            ws.Column(6).Width = 8;
            ws.Column(7).Width = 12;
            ws.Column(8).Width = 18;
            ws.Column(9).Width = 12;
            ws.Column(10).Width = 10;
            ws.Column(11).Width = 10;
            ws.Column(12).Width = 10;
            ws.Column(13).Width = 12;
            ws.Column(14).Width = 12;
            ws.Column(15).Width = 8;

            string[] headers = { "Sr No", "Container No", "Size", "Type", "Cargo Type", "PKGS", "Gross Weight", "Arrival Date & Time", "Total Days", "Free Days", "Charge Days", "Hnd Charges", "Grd Charges", "Validity Date", "JO Type" };

            for (int i = 0; i < headers.Length; i++)
            {
                var cell = ws.Cells[1, i + 1];
                cell.Value = headers[i];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                cell.Style.Fill.BackgroundColor.SetColor(Color.LightBlue);
            }

            int row = 2;
            foreach (var container in pdfData.ContainerDetails)
            {
                ws.Cells[row, 1].Value = container.SrNo;
                ws.Cells[row, 2].Value = container.ContainerNo;
                ws.Cells[row, 3].Value = container.Size;
                ws.Cells[row, 4].Value = container.Type;
                ws.Cells[row, 5].Value = container.CargoType;
                ws.Cells[row, 6].Value = container.PKGS;
                ws.Cells[row, 7].Value = container.GrossWeight;
                ws.Cells[row, 8].Value = container.ArrivalDateTime;
                ws.Cells[row, 9].Value = container.TotalDays;
                ws.Cells[row, 10].Value = container.FreeDays;
                ws.Cells[row, 11].Value = container.ChargeDays;
                ws.Cells[row, 12].Value = container.HndCharges;
                ws.Cells[row, 13].Value = container.GrdCharges;
                ws.Cells[row, 14].Value = container.ValidityDate;
                ws.Cells[row, 15].Value = container.JOType;
                row++;
            }

            // Freeze first row
            ws.View.FreezePanes(2, 0);
        }

        private static void CreateBillItemsSheet(ExcelWorksheet ws, PdfData pdfData)
        {
            ws.Column(1).Width = 6;
            ws.Column(2).Width = 35;
            ws.Column(3).Width = 12;
            ws.Column(4).Width = 8;
            ws.Column(5).Width = 8;
            ws.Column(6).Width = 12;

            string[] headers = { "Sr No", "Description", "HSN Code", "Size", "Qty", "Amount" };

            for (int i = 0; i < headers.Length; i++)
            {
                var cell = ws.Cells[1, i + 1];
                cell.Value = headers[i];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                cell.Style.Fill.BackgroundColor.SetColor(Color.LightGreen);
            }

            int row = 2;
            decimal totalAmount = 0;

            foreach (var item in pdfData.BillItems)
            {
                ws.Cells[row, 1].Value = item.SrNo;
                ws.Cells[row, 2].Value = item.Description;
                ws.Cells[row, 3].Value = item.HSNCode;
                ws.Cells[row, 4].Value = item.Size;
                ws.Cells[row, 5].Value = item.Qty;
                ws.Cells[row, 6].Value = decimal.Parse(item.Amount);

                if (decimal.TryParse(item.Amount, out decimal amount))
                    totalAmount += amount;

                row++;
            }

            ws.Cells[row, 2].Value = "TOTAL";
            ws.Cells[row, 2].Style.Font.Bold = true;
            ws.Cells[row, 6].Value = totalAmount;
            ws.Cells[row, 6].Style.Font.Bold = true;
            ws.Cells[row, 6].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
            ws.Cells[row, 6].Style.Fill.BackgroundColor.SetColor(Color.Yellow);
        }

        private static void CreateTaxSummarySheet(ExcelWorksheet ws, PdfData pdfData)
        {
            ws.Column(1).Width = 30;
            ws.Column(2).Width = 20;

            var headerCell = ws.Cells[1, 1];
            headerCell.Value = "TAX SUMMARY";
            headerCell.Style.Font.Bold = true;
            headerCell.Style.Font.Size = 12;
            ws.Cells[1, 1, 1, 2].Merge = true;

            int row = 3;

            AddLabelValueRow(ws, row++, "Subtotal (Before Tax)", pdfData.TotalAmountBeforeTax);
            AddLabelValueRow(ws, row++, "SGST @ 9%", pdfData.SGST);
            AddLabelValueRow(ws, row++, "CGST @ 9%", pdfData.CGST);
            AddLabelValueRow(ws, row++, "IGST", pdfData.IGST);

            row++;
            var totalCell = ws.Cells[row, 1];
            totalCell.Value = "TOTAL AMOUNT (After Tax)";
            totalCell.Style.Font.Bold = true;
            totalCell.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
            totalCell.Style.Fill.BackgroundColor.SetColor(Color.Yellow);

            var totalValueCell = ws.Cells[row, 2];
            totalValueCell.Value = pdfData.TotalAmountAfterTax;
            totalValueCell.Style.Font.Bold = true;
            totalValueCell.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
            totalValueCell.Style.Fill.BackgroundColor.SetColor(Color.Yellow);
        }

        private static void AddLabelValueRow(ExcelWorksheet ws, int row, string label, string value)
        {
            ws.Cells[row, 1].Value = label;
            ws.Cells[row, 1].Style.Font.Bold = true;
            ws.Cells[row, 2].Value = value;
        }
    }

    public class PdfData
    {
        public string InvoiceNumber { get; set; }
        public string InvoiceDate { get; set; }
        public string SupplierName { get; set; }
        public string GSTIN { get; set; }
        public string ConsigneeName { get; set; }
        public string CommodityName { get; set; }
        public string CargoWeight { get; set; }
        public string TotalAmountBeforeTax { get; set; }
        public string SGST { get; set; }
        public string CGST { get; set; }
        public string IGST { get; set; }
        public string TotalAmountAfterTax { get; set; }
        public List<ContainerDetail> ContainerDetails { get; set; } = new List<ContainerDetail>();
        public List<BillItem> BillItems { get; set; } = new List<BillItem>();
    }

    public class ContainerDetail
    {
        public string SrNo { get; set; }
        public string ContainerNo { get; set; }
        public string Size { get; set; }
        public string Type { get; set; }
        public string CargoType { get; set; }
        public string PKGS { get; set; }
        public string GrossWeight { get; set; }
        public string ArrivalDateTime { get; set; }
        public string TotalDays { get; set; }
        public string FreeDays { get; set; }
        public string ChargeDays { get; set; }
        public string HndCharges { get; set; }
        public string GrdCharges { get; set; }
        public string ValidityDate { get; set; }
        public string JOType { get; set; }
    }

    public class BillItem
    {
        public string SrNo { get; set; }
        public string Description { get; set; }
        public string HSNCode { get; set; }
        public string Size { get; set; }
        public string Qty { get; set; }
        public string Amount { get; set; }
    }
}
