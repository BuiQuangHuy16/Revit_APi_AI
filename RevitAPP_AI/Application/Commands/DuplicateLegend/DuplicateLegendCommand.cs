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
using Autodesk.Revit.UI.Selection;
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

            if (activeSheet == null)
            {
                TaskDialog.Show("Duplicate Legends", "Hãy mở 1 sheet rồi chạy lệnh.");
                return;
            }

            // 1. Pre-selection: lọc viewport legend thuộc active sheet.
            var selection = BuildFromPreselection(uidoc, activeSheet);

            // 2. Nếu rỗng → prompt PickObjects.
            if (selection.Count == 0)
            {
                IList<Reference> refs;
                try
                {
                    refs = uidoc.Selection.PickObjects(
                        ObjectType.Element,
                        new LegendViewportFilter(doc, activeSheet.Id),
                        "Chọn legend trên sheet (Esc để huỷ)");
                }
                catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                {
                    return;
                }

                selection = BuildFromReferences(doc, refs);
            }

            if (selection.Count == 0) return;

            // 3. Dialog hỏi mode (Duplicate / Replace).
            var viewModel = new DuplicateLegendViewModel(selection.Count);
            var window = new DuplicateLegendWindow(viewModel);
            new WindowInteropHelper(window).Owner = uidoc.Application.MainWindowHandle;

            var dialogResult = window.ShowDialog();
            if (dialogResult != true) return;

            var options = viewModel.GetOptions();

            // 4. PickPoint sau khi đóng dialog (Revit refuses pick while modal WPF dialog is open).
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

            // 5. Execute.
            var result = LegendDuplicator.Run(doc, activeSheet, selection, options);

            if (result.FirstCreatedViewportId != null && result.FirstCreatedViewportId != ElementId.InvalidElementId)
            {
                uidoc.Selection.SetElementIds(new List<ElementId> { result.FirstCreatedViewportId });
            }

            var summary = $"Đã tạo {result.CreatedCount} legend.";
            if (result.Errors.Count > 0)
                summary += $"\n\nLỗi ({result.Errors.Count}):\n - " + string.Join("\n - ", result.Errors.Take(10));

            TaskDialog.Show("Duplicate Legends", summary);
        }

        private static List<SelectedViewport> BuildFromPreselection(UIDocument uidoc, ViewSheet activeSheet)
        {
            var doc = uidoc.Document;
            var list = new List<SelectedViewport>();
            foreach (var id in uidoc.Selection.GetElementIds())
            {
                var vp = doc.GetElement(id) as Viewport;
                if (vp == null) continue;
                if (vp.SheetId != activeSheet.Id) continue;
                var v = doc.GetElement(vp.ViewId) as View;
                if (v == null || v.ViewType != ViewType.Legend || v.IsTemplate) continue;

                list.Add(BuildSelectedViewport(vp, v));
            }
            return list;
        }

        private static List<SelectedViewport> BuildFromReferences(Document doc, IList<Reference> refs)
        {
            var list = new List<SelectedViewport>();
            if (refs == null) return list;
            foreach (var r in refs)
            {
                var vp = doc.GetElement(r.ElementId) as Viewport;
                if (vp == null) continue;
                var v = doc.GetElement(vp.ViewId) as View;
                if (v == null || v.ViewType != ViewType.Legend || v.IsTemplate) continue;

                list.Add(BuildSelectedViewport(vp, v));
            }
            return list;
        }

        private static SelectedViewport BuildSelectedViewport(Viewport vp, View legend)
        {
            XYZ center = null;
            ElementId typeId = null;
            try { center = vp.GetBoxCenter(); } catch { }
            try { typeId = vp.GetTypeId(); } catch { }

            return new SelectedViewport
            {
                ViewportId = vp.Id,
                LegendId = legend.Id,
                LegendName = legend.Name,
                Center = center,
                TypeId = typeId
            };
        }
    }

    public class LegendViewportFilter : ISelectionFilter
    {
        private readonly Document _doc;
        private readonly ElementId _sheetId;

        public LegendViewportFilter(Document doc, ElementId sheetId)
        {
            _doc = doc;
            _sheetId = sheetId;
        }

        public bool AllowElement(Element elem)
        {
            if (!(elem is Viewport vp)) return false;
            if (vp.SheetId != _sheetId) return false;
            var v = _doc.GetElement(vp.ViewId) as View;
            return v != null && v.ViewType == ViewType.Legend && !v.IsTemplate;
        }

        public bool AllowReference(Reference reference, XYZ position) => true;
    }
}
