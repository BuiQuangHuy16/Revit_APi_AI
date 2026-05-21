using System.Linq;
using System.Windows.Interop;
using Aplication.Commands.DuplicateSheet.Models;
using Aplication.Commands.DuplicateSheet.Services;
using Aplication.Commands.DuplicateSheet.ViewModels;
using Aplication.Commands.DuplicateSheet.Views;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Nice3point.Revit.Toolkit.External;

namespace Aplication.Commands.DuplicateSheet
{
    [UsedImplicitly]
    [Transaction(TransactionMode.Manual)]
    public class DuplicateSheetsCommand : ExternalCommand
    {
        public override void Execute()
        {
            var uidoc = Application.ActiveUIDocument;
            var doc = uidoc.Document;

            var sheets = new FilteredElementCollector(doc)
                .OfClass(typeof(ViewSheet))
                .Cast<ViewSheet>()
                .Where(s => !s.IsPlaceholder)
                .OrderBy(s => s.SheetNumber)
                .Select(s => new SheetItem
                {
                    SheetId = s.Id,
                    SheetNumber = s.SheetNumber,
                    SheetName = s.Name
                })
                .ToList();

            if (sheets.Count == 0)
            {
                TaskDialog.Show("Duplicate Sheets", "Không có sheet nào trong project.");
                return;
            }

            var viewModel = new DuplicateSheetsViewModel(sheets);
            var window = new DuplicateSheetsWindow(viewModel);
            new WindowInteropHelper(window).Owner = uidoc.Application.MainWindowHandle;

            var dialogResult = window.ShowDialog();
            if (dialogResult != true) return;

            var selected = viewModel.GetSelected().ToList();
            if (selected.Count == 0) return;

            var result = SheetDuplicator.Run(doc, selected, viewModel.GetOptions());

            if (result.FirstCreatedSheetId != null && result.FirstCreatedSheetId != ElementId.InvalidElementId)
            {
                if (doc.GetElement(result.FirstCreatedSheetId) is ViewSheet firstSheet)
                    uidoc.ActiveView = firstSheet;
            }

            var summary = $"Đã tạo {result.CreatedCount} sheet.";
            if (result.Errors.Count > 0)
                summary += $"\n\nLỗi ({result.Errors.Count}):\n - " + string.Join("\n - ", result.Errors.Take(10));

            TaskDialog.Show("Duplicate Sheets", summary);
        }
    }
}
