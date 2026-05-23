using System.Collections.Generic;
using System.Linq;
using System.Windows.Interop;
using Aplication.Commands.ExportSchedule.Services;
using Aplication.Commands.ExportSchedule.ViewModels;
using Aplication.Commands.ExportSchedule.Views;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Nice3point.Revit.Toolkit.External;

namespace Aplication.Commands.ExportSchedule
{
    // Entry point của lệnh "Export Schedule to Excel" trên ribbon.
    // Dùng Manual (không phải ReadOnly) vì khi gọi ViewSchedule.GetTableData()
    // / GetCellText() Revit cần regenerate dữ liệu schedule (đặc biệt với các
    // schedule chứa filter/group/calculated values). ReadOnly khoá mọi thao tác
    // modify kể cả regenerate nội bộ -> ném "Changes are disabled". Manual cho
    // phép regenerate mà không tự mở transaction nào của ta.
    [UsedImplicitly]
    [Transaction(TransactionMode.Manual)]
    public class ExportScheduleCommand : ExternalCommand
    {
        public override void Execute()
        {
            var uidoc = Application.ActiveUIDocument;
            var doc = uidoc.Document;

            // 1. Quét danh sách schedule do người dùng tạo.
            var items = ScheduleCollector.Run(doc);
            if (items.Count == 0)
            {
                TaskDialog.Show("Export Schedule to Excel",
                    "Dự án không có bảng thống kê nào để xuất.");
                return;
            }

            // 2. Khởi tạo ViewModel + Window, gắn owner để dialog hiển thị đúng vị trí.
            var viewModel = new ExportScheduleViewModel(items);
            var window = new ExportScheduleWindow(viewModel);
            new WindowInteropHelper(window).Owner = uidoc.Application.MainWindowHandle;

            if (window.ShowDialog() != true) return;
            if (viewModel.SelectedIds == null || viewModel.SelectedIds.Count == 0) return;

            // 3. Resolve ViewSchedule từ ElementId (sau khi dialog đã đóng).
            var schedules = new List<ViewSchedule>();
            foreach (var id in viewModel.SelectedIds)
            {
                if (doc.GetElement(id) is ViewSchedule vs)
                    schedules.Add(vs);
            }
            if (schedules.Count == 0) return;

            // 4. Gọi service xuất Excel tương ứng với mode đã chọn.
            ScheduleExcelExporter.ExportResult result;
            if (viewModel.ResultSeparateFiles)
                result = ScheduleExcelExporter.ExportToSeparateFiles(schedules, viewModel.ResultPath);
            else
                result = ScheduleExcelExporter.ExportToSingleFile(schedules, viewModel.ResultPath);

            // 5. Tổng hợp kết quả và hiển thị cho người dùng.
            var summary = $"Đã xuất {result.SuccessCount}/{schedules.Count} bảng thống kê thành công.";
            if (result.FailedCount > 0)
            {
                summary += $"\n\nLỗi ({result.FailedCount}):\n - "
                           + string.Join("\n - ", result.Errors.Take(10));
                if (result.Errors.Count > 10)
                    summary += $"\n ... và {result.Errors.Count - 10} lỗi khác.";
            }

            TaskDialog.Show("Export Schedule to Excel", summary);
        }
    }
}
