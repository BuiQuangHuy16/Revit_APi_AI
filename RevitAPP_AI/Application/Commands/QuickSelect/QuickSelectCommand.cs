using System.Linq;
using System.Windows.Interop;
using Aplication.Commands.QuickSelect.Services;
using Aplication.Commands.QuickSelect.ViewModels;
using Aplication.Commands.QuickSelect.Views;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI;
using Nice3point.Revit.Toolkit.External;

namespace Aplication.Commands.QuickSelect
{
    [UsedImplicitly]
    [Transaction(TransactionMode.ReadOnly)]
    public class QuickSelectCommand : ExternalCommand
    {
        public override void Execute()
        {
            var uidoc = Application.ActiveUIDocument;
            var doc = uidoc.Document;

            var ids = uidoc.Selection.GetElementIds();
            if (ids == null || ids.Count == 0)
            {
                TaskDialog.Show("Quick Select",
                    "Hãy quét chọn các đối tượng trước khi chạy lệnh Quick Select.");
                return;
            }

            var groups = ParameterExtractor.Run(doc, ids);
            if (groups.Count == 0)
            {
                TaskDialog.Show("Quick Select",
                    "Không trích xuất được tham số nào từ các đối tượng đã chọn.");
                return;
            }

            var viewModel = new QuickSelectViewModel(groups, ids);
            var window = new QuickSelectWindow(viewModel);
            new WindowInteropHelper(window).Owner = uidoc.Application.MainWindowHandle;

            if (window.ShowDialog() != true) return;
            if (viewModel.ResultIds == null || viewModel.ResultIds.Count == 0) return;

            uidoc.Selection.SetElementIds(viewModel.ResultIds.ToList());
        }
    }
}
