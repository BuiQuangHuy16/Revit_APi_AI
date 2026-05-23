using System;
using System.Collections.Generic;
using System.IO;
using Autodesk.Revit.DB;
using ClosedXML.Excel;

namespace Aplication.Commands.ExportSchedule.Services
{
    // Đọc dữ liệu hiển thị (Title + Header + Body + Summary) của một ViewSchedule
    // qua TableData/GetCellText và ghi nguyên vẹn sang Excel — đúng đủ những gì
    // Revit render trên UI, tôn trọng cả các ô bị merge ở phần Header.
    internal static class ScheduleExcelExporter
    {
        // Kết quả tổng hợp sau khi xuất nhiều schedule.
        public class ExportResult
        {
            public int SuccessCount;
            public int FailedCount;
            public List<string> Errors = new List<string>();
        }

        // TH1: Gộp tất cả các ViewSchedule đã chọn vào MỘT file Excel duy nhất,
        // mỗi schedule là một worksheet.
        public static ExportResult ExportToSingleFile(IReadOnlyList<ViewSchedule> schedules, string filePath)
        {
            var result = new ExportResult();
            using (var workbook = new XLWorkbook())
            {
                var usedSheetNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var vs in schedules)
                {
                    try
                    {
                        var sheetName = MakeUniqueSheetName(vs.Name, usedSheetNames);
                        var ws = workbook.Worksheets.Add(sheetName);
                        WriteScheduleToWorksheet(vs, ws);
                        result.SuccessCount++;
                    }
                    catch (Exception ex)
                    {
                        result.FailedCount++;
                        result.Errors.Add($"{SafeName(vs)}: {ex.Message}");
                    }
                }

                // Workbook bắt buộc phải có ít nhất 1 worksheet trước khi Save.
                if (workbook.Worksheets.Count == 0)
                    workbook.Worksheets.Add("Empty");

                workbook.SaveAs(filePath);
            }
            return result;
        }

        // TH2: Mỗi ViewSchedule được lưu thành MỘT file .xlsx riêng trong cùng
        // một thư mục, tên file lấy theo tên schedule (đã sanitize ký tự cấm).
        public static ExportResult ExportToSeparateFiles(IReadOnlyList<ViewSchedule> schedules, string folderPath)
        {
            var result = new ExportResult();
            var usedFileNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var vs in schedules)
            {
                try
                {
                    var baseFileName = SanitizeFileName(vs.Name);
                    var uniqueFileName = MakeUniqueFileName(baseFileName, usedFileNames);
                    var fullPath = Path.Combine(folderPath, uniqueFileName + ".xlsx");

                    using (var workbook = new XLWorkbook())
                    {
                        var sheetName = SanitizeSheetName(vs.Name);
                        var ws = workbook.Worksheets.Add(sheetName);
                        WriteScheduleToWorksheet(vs, ws);
                        workbook.SaveAs(fullPath);
                    }
                    result.SuccessCount++;
                }
                catch (Exception ex)
                {
                    result.FailedCount++;
                    result.Errors.Add($"{SafeName(vs)}: {ex.Message}");
                }
            }
            return result;
        }

        // Ghi nội dung một ViewSchedule vào 1 worksheet — đi qua tất cả các
        // SectionType (Header → Body → Summary) theo đúng thứ tự render của Revit.
        private static void WriteScheduleToWorksheet(ViewSchedule schedule, IXLWorksheet ws)
        {
            var tableData = schedule.GetTableData();

            int rowOffset = 0;
            rowOffset += WriteSection(schedule, tableData, SectionType.Header, ws, rowOffset);
            rowOffset += WriteSection(schedule, tableData, SectionType.Body, ws, rowOffset);
            rowOffset += WriteSection(schedule, tableData, SectionType.Summary, ws, rowOffset);

            // Auto-fit chiều rộng cột cho dễ đọc — không bắt buộc nhưng UX tốt hơn.
            try { ws.Columns().AdjustToContents(); }
            catch { /* AdjustToContents có thể throw khi thiếu font fallback — bỏ qua an toàn. */ }
        }

        // Ghi một section (Header/Body/Summary) bắt đầu từ dòng `rowOffset` trở xuống.
        // Trả về số dòng đã ghi để section tiếp theo nối tiếp ngay phía dưới.
        private static int WriteSection(ViewSchedule schedule, TableData tableData,
            SectionType sectionType, IXLWorksheet ws, int rowOffset)
        {
            TableSectionData section;
            try { section = tableData.GetSectionData(sectionType); }
            catch { return 0; }

            if (section == null) return 0;

            int rows = section.NumberOfRows;
            int cols = section.NumberOfColumns;
            if (rows <= 0 || cols <= 0) return 0;

            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    TableMergedCell merged;
                    try { merged = section.GetMergedCell(r, c); }
                    catch { merged = null; }

                    // Nếu ô (r,c) không phải gốc của vùng merge thì bỏ qua —
                    // ô gốc sẽ ghi text + merge range tương ứng.
                    bool isPrimary = merged == null || (merged.Top == r && merged.Left == c);
                    if (!isPrimary) continue;

                    string text;
                    try { text = schedule.GetCellText(sectionType, r, c) ?? string.Empty; }
                    catch { text = string.Empty; }

                    int excelRow = rowOffset + r + 1; // ClosedXML dùng index 1-based
                    int excelCol = c + 1;

                    ws.Cell(excelRow, excelCol).Value = text;

                    // Áp merge tương ứng nếu vùng merge bao nhiều hơn 1 ô.
                    if (merged != null &&
                        (merged.Right > merged.Left || merged.Bottom > merged.Top))
                    {
                        int excelRowEnd = rowOffset + merged.Bottom + 1;
                        int excelColEnd = merged.Right + 1;
                        ws.Range(excelRow, excelCol, excelRowEnd, excelColEnd).Merge();
                    }
                }
            }

            return rows;
        }

        // ============== Sanitize / dedupe helpers ==============

        // Excel cấm các ký tự sau trong tên sheet: : \ / ? * [ ]
        // Ngoài ra giới hạn 31 ký tự và không được trùng nhau trong 1 workbook.
        private static readonly char[] InvalidSheetChars = { ':', '\\', '/', '?', '*', '[', ']' };
        private const int MaxSheetNameLength = 31;

        private static string SanitizeSheetName(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "Sheet";
            var arr = raw.ToCharArray();
            for (int i = 0; i < arr.Length; i++)
            {
                if (Array.IndexOf(InvalidSheetChars, arr[i]) >= 0)
                    arr[i] = '_';
            }
            var clean = new string(arr).Trim().Trim('\'');
            if (clean.Length > MaxSheetNameLength)
                clean = clean.Substring(0, MaxSheetNameLength);
            if (string.IsNullOrEmpty(clean)) clean = "Sheet";
            return clean;
        }

        // Đảm bảo sheet name là duy nhất trong workbook (append "_2", "_3"...).
        private static string MakeUniqueSheetName(string raw, HashSet<string> used)
        {
            var baseName = SanitizeSheetName(raw);
            if (used.Add(baseName)) return baseName;

            for (int i = 2; i < int.MaxValue; i++)
            {
                var suffix = "_" + i;
                int allowedBaseLen = MaxSheetNameLength - suffix.Length;
                var candidateBase = baseName.Length > allowedBaseLen
                    ? baseName.Substring(0, allowedBaseLen)
                    : baseName;
                var candidate = candidateBase + suffix;
                if (used.Add(candidate)) return candidate;
            }
            // Fallback gần như không bao giờ xảy ra.
            return Guid.NewGuid().ToString("N").Substring(0, 8);
        }

        private static string SanitizeFileName(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "Schedule";
            var invalid = Path.GetInvalidFileNameChars();
            var arr = raw.ToCharArray();
            for (int i = 0; i < arr.Length; i++)
            {
                if (Array.IndexOf(invalid, arr[i]) >= 0)
                    arr[i] = '_';
            }
            var clean = new string(arr).Trim().TrimEnd('.');
            if (string.IsNullOrEmpty(clean)) clean = "Schedule";
            return clean;
        }

        // Đảm bảo tên file không trùng trong cùng thư mục/đợt xuất.
        private static string MakeUniqueFileName(string baseName, HashSet<string> used)
        {
            if (used.Add(baseName)) return baseName;
            for (int i = 2; i < int.MaxValue; i++)
            {
                var candidate = baseName + "_" + i;
                if (used.Add(candidate)) return candidate;
            }
            return Guid.NewGuid().ToString("N").Substring(0, 8);
        }

        private static string SafeName(ViewSchedule vs)
        {
            try { return vs?.Name ?? "<null>"; }
            catch { return "<error>"; }
        }
    }
}
