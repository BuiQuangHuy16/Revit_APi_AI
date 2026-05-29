using System.Windows.Interop;
using Aplication.Commands.ExportCAD.Services;
using Aplication.Commands.ExportCAD.ViewModels;
using Aplication.Commands.ExportCAD.Views;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI;
using Nice3point.Revit.Toolkit.External;

namespace Aplication.Commands.ExportCAD
{
    // Entry point của lệnh "Export CAD (Merged)" trên ribbon.
    // - Manual: Document.Export(...) cần Revit regenerate dữ liệu view trước khi
    //   ghi DWG. ReadOnly sẽ chặn các thao tác này.
    // - Không mở Transaction của ta vì doc.Export tự quản lý nội bộ.
    [UsedImplicitly]
    [Transaction(TransactionMode.Manual)]
    public class ExportCADCommand : ExternalCommand
    {
        public override void Execute()
        {
            var uidoc = Application.ActiveUIDocument;
            var doc = uidoc.Document;

            // 1. Quét view/sheet có thể export.
            var items = ExportableViewCollector.Run(doc);
            if (items.Count == 0)
            {
                TaskDialog.Show("Export CAD",
                    "Dự án không có view/sheet nào có thể export ra CAD.");
                return;
            }

            // 2. Mở dialog chọn view + đường dẫn file đích.
            var viewModel = new ExportCADViewModel(items);
            var window = new ExportCADWindow(viewModel);
            new WindowInteropHelper(window).Owner = uidoc.Application.MainWindowHandle;

            if (window.ShowDialog() != true) return;
            if (viewModel.SelectedIds == null || viewModel.SelectedIds.Count == 0) return;
            if (string.IsNullOrWhiteSpace(viewModel.ResultPath)) return;

            // 3. Thực hiện export ra 1 file DWG tổng.
            var result = CADExporter.ExportMergedToSingleFile(
                doc, viewModel.SelectedIds, viewModel.ResultPath);

            // 4. Báo kết quả.
            if (result.Success)
            {
                TaskDialog.Show("Export CAD",
                    $"Đã export {result.ViewCount} view/sheet vào:\n{result.OutputFilePath}");
            }
            else
            {
                TaskDialog.Show("Export CAD",
                    $"Export thất bại:\n{result.ErrorMessage}");
            }
        }
    }
}
