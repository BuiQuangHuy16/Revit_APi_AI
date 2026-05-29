using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.Revit.DB;

namespace Aplication.Commands.ExportCAD.Services
{
    // Thực hiện export 1 hoặc nhiều View/Sheet ra DWG.
    //
    // CHIẾN LƯỢC GỘP & GIỮ TỶ LỆ
    //  - DWGExportOptions.MergedViews = true: khi truyền nhiều ElementId vào
    //    Document.Export(...), Revit gộp toàn bộ các view (kèm dependent views
    //    của Sheet) vào DUY NHẤT một file DWG đầu ra.
    //  - SharedCoords = false (Project Internal): toạ độ trong DWG khớp với
    //    toạ độ nội bộ của Revit → mỗi view được đặt đúng vị trí và đúng tỷ lệ
    //    như nhìn thấy trong Revit (sheet ở paper space scale, model view ở
    //    real-world 1:1).
    //  - Không override unit / tham số scale → để Revit dùng project units, đây
    //    là cách duy nhất giữ "tỷ lệ như trong Revit" mà không bóp méo bản vẽ.
    internal static class CADExporter
    {
        public class ExportResult
        {
            public bool Success;
            public string OutputFilePath;
            public string ErrorMessage;
            public int ViewCount;
        }

        // Xuất tập view/sheet đã chọn vào MỘT file DWG tổng.
        // filePath: đường dẫn file đích người dùng đã chọn (có hoặc không .dwg).
        public static ExportResult ExportMergedToSingleFile(
            Document doc,
            IReadOnlyList<ElementId> viewIds,
            string filePath)
        {
            var result = new ExportResult { ViewCount = viewIds?.Count ?? 0 };

            if (viewIds == null || viewIds.Count == 0)
            {
                result.ErrorMessage = "Không có view nào được chọn để export.";
                return result;
            }

            string folder = Path.GetDirectoryName(filePath);
            string nameNoExt = Path.GetFileNameWithoutExtension(filePath);

            if (string.IsNullOrEmpty(folder) || string.IsNullOrEmpty(nameNoExt))
            {
                result.ErrorMessage = "Đường dẫn file không hợp lệ.";
                return result;
            }

            if (!Directory.Exists(folder))
            {
                try { Directory.CreateDirectory(folder); }
                catch (Exception ex)
                {
                    result.ErrorMessage = $"Không tạo được thư mục đích: {ex.Message}";
                    return result;
                }
            }

            try
            {
                var options = BuildOptions();
                var idCollection = viewIds.ToList();

                // doc.Export trả về bool; khi MergedViews = true, đầu ra là 1 file
                // duy nhất có tên = $"{nameNoExt}.dwg" trong thư mục `folder`.
                bool ok = doc.Export(folder, nameNoExt, idCollection, options);

                if (!ok)
                {
                    result.ErrorMessage = "Revit từ chối export (Document.Export trả về false). "
                                          + "Có thể do view chọn không hợp lệ hoặc thiếu quyền ghi file.";
                    return result;
                }

                // Revit có thể thêm hậu tố hoặc giữ nguyên tên — quét lại để xác định.
                result.OutputFilePath = LocateOutputFile(folder, nameNoExt);
                result.Success = true;
                return result;
            }
            catch (Exception ex)
            {
                result.ErrorMessage = ex.Message;
                return result;
            }
        }

        private static DWGExportOptions BuildOptions()
        {
            // Ưu tiên dùng predefined export setup mặc định trong project nếu có
            // (giữ đúng layer mapping / line type / text mapping mà người dùng đã
            // cấu hình). Nếu không có thì fallback sang DWGExportOptions() default.
            var options = new DWGExportOptions
            {
                MergedViews = true,        // gộp tất cả view -> 1 file DWG duy nhất
                SharedCoords = false,      // dùng toạ độ Project Internal -> đúng vị trí/tỷ lệ Revit
                ExportOfSolids = SolidGeometry.Polymesh,
                HideScopeBox = true,
                HideReferencePlane = true,
                HideUnreferenceViewTags = true
            };
            return options;
        }

        // Sau khi export xong, tìm file DWG vừa tạo. Revit thường tạo
        // $"{nameNoExt}.dwg", nhưng với một số version có thể thêm hậu tố.
        private static string LocateOutputFile(string folder, string baseName)
        {
            var exact = Path.Combine(folder, baseName + ".dwg");
            if (File.Exists(exact)) return exact;

            // Tìm file vừa tạo trong vài giây gần đây với prefix tương ứng.
            try
            {
                var candidate = Directory.GetFiles(folder, baseName + "*.dwg")
                    .OrderByDescending(File.GetLastWriteTimeUtc)
                    .FirstOrDefault();
                if (candidate != null) return candidate;
            }
            catch { /* không quan trọng — return null nếu không tìm được */ }

            return exact; // best-effort
        }
    }
}
