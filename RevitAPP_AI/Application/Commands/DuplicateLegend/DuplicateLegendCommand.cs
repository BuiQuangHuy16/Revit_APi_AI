using System.Collections.Generic;
using System.Linq;
using System.Windows.Interop;
using Aplication.Commands.DuplicateLegend.Models;
using Aplication.Commands.DuplicateLegend.Services;
using Aplication.Commands.DuplicateLegend.ViewModels;
using Aplication.Commands.DuplicateLegend.Views;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Nice3point.Revit.Toolkit.External;

namespace Aplication.Commands.DuplicateLegend
{
    [UsedImplicitly]
    [Transaction(TransactionMode.Manual)]
    public class DuplicateLegendCommand : ExternalCommand
    {
        public override void Execute()
        {
            var uidoc = Application.ActiveUIDocument;
            var doc = uidoc.Document;
            var activeSheet = uidoc.ActiveView as ViewSheet;

            // Pre-selection: read legend viewports on active sheet that user already selected.
            var preselectedLegendIds = new HashSet<ElementId>();
            foreach (var id in uidoc.Selection.GetElementIds())
            {
                if (!(doc.GetElement(id) is Viewport vp)) continue;
                if (!(doc.GetElement(vp.ViewId) is View v)) continue;
                if (v.ViewType == ViewType.Legend && !v.IsTemplate)
                    preselectedLegendIds.Add(v.Id);
            }

            // Collect all legends + count usage on sheets.
            var allViewports = new FilteredElementCollector(doc)
                .OfClass(typeof(Viewport))
                .Cast<Viewport>()
                .ToList();

            var legends = new FilteredElementCollector(doc)
                .OfClass(typeof(View))
                .Cast<View>()
                .Where(v => v.ViewType == ViewType.Legend && !v.IsTemplate)
                .OrderBy(v => v.Name)
                .Select(v => new LegendItem
                {
                    LegendId = v.Id,
                    LegendName = v.Name,
                    SheetUsageCount = allViewports
                        .Where(vp => vp.ViewId == v.Id)
                        .Select(vp => vp.SheetId)
                        .Distinct()
                        .Count(),
                    IsSelected = preselectedLegendIds.Contains(v.Id)
                })
                .ToList();

            if (legends.Count == 0)
            {
                TaskDialog.Show("Duplicate Legends", "Không có legend nào trong project.");
                return;
            }

            var viewModel = new DuplicateLegendViewModel(legends, activeSheet != null);
            var window = new DuplicateLegendWindow(viewModel);
            new WindowInteropHelper(window).Owner = uidoc.Application.MainWindowHandle;

            var dialogResult = window.ShowDialog();
            if (dialogResult != true) return;

            var selected = viewModel.GetSelected().ToList();
            if (selected.Count == 0) return;

            var options = viewModel.GetOptions();

            // Both modes need active view to be a sheet.
            if (activeSheet == null)
            {
                TaskDialog.Show("Duplicate Legends", "View hiện tại không phải sheet. Hãy mở 1 sheet và thử lại.");
                return;
            }

            // PickPoint: prompt user AFTER dialog closes (Revit refuses pick while modal WPF dialog is open).
            if (options.Mode == PlacementMode.PickPoint)
            {
                try
                {
                    options.PickedPoint = uidoc.Selection.PickPoint("Chọn điểm đặt legend");
                }
                catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                {
                    return;
                }
            }

            var result = LegendDuplicator.Run(doc, activeSheet, selected, options);

            if (result.FirstCreatedViewportId != null && result.FirstCreatedViewportId != ElementId.InvalidElementId)
            {
                uidoc.Selection.SetElementIds(new List<ElementId> { result.FirstCreatedViewportId });
            }

            var summary = $"Đã tạo {result.CreatedCount} legend.";
            if (result.Errors.Count > 0)
                summary += $"\n\nLỗi ({result.Errors.Count}):\n - " + string.Join("\n - ", result.Errors.Take(10));

            TaskDialog.Show("Duplicate Legends", summary);
        }
    }
}
