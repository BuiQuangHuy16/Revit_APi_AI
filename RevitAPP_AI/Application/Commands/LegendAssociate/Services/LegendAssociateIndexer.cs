using System.Collections.Generic;
using System.Linq;
using Aplication.Commands.LegendAssociate.Models;
using Autodesk.Revit.DB;

namespace Aplication.Commands.LegendAssociate.Services
{
    public class LegendIndex
    {
        public List<ViewItem> Legends { get; set; } = new List<ViewItem>();
        public List<ViewItem> Views { get; set; } = new List<ViewItem>();
        public List<SheetItem> Sheets { get; set; } = new List<SheetItem>();

        // viewId → sheetIds chứa view đó
        public Dictionary<ElementId, List<ElementId>> ViewToSheets { get; set; }
            = new Dictionary<ElementId, List<ElementId>>();

        // sheetId → viewIds (theo viewport) trong sheet
        public Dictionary<ElementId, List<ElementId>> SheetToViews { get; set; }
            = new Dictionary<ElementId, List<ElementId>>();
    }

    public static class LegendAssociateIndexer
    {
        public static LegendIndex Build(Document doc)
        {
            var index = new LegendIndex();

            // 1. Sheets (kèm AssemblyName nếu có)
            var sheets = new FilteredElementCollector(doc)
                .OfClass(typeof(ViewSheet))
                .Cast<ViewSheet>()
                .Where(s => !s.IsTemplate)
                .ToList();

            var sheetById = new Dictionary<ElementId, ViewSheet>();
            foreach (var sheet in sheets)
            {
                sheetById[sheet.Id] = sheet;
                index.Sheets.Add(new SheetItem
                {
                    Id = sheet.Id,
                    SheetNumber = sheet.SheetNumber,
                    SheetName = sheet.Name,
                    AssemblyName = ResolveAssemblyName(doc, sheet)
                });
            }

            index.Sheets = index.Sheets
                .OrderBy(s => s.AssemblyName ?? string.Empty)
                .ThenBy(s => s.SheetNumber)
                .ToList();

            // 2. Views (legend + view thường, bỏ template / system / schedule)
            var allViews = new FilteredElementCollector(doc)
                .OfClass(typeof(View))
                .Cast<View>()
                .Where(v => !v.IsTemplate && IsListableView(v))
                .ToList();

            foreach (var v in allViews)
            {
                var item = new ViewItem
                {
                    Id = v.Id,
                    Name = v.Name,
                    IsLegend = v.ViewType == ViewType.Legend,
                    ViewTypeLabel = v.ViewType.ToString()
                };

                if (item.IsLegend) index.Legends.Add(item);
                else index.Views.Add(item);
            }

            index.Legends = index.Legends.OrderBy(v => v.Name).ToList();
            index.Views = index.Views.OrderBy(v => v.ViewTypeLabel).ThenBy(v => v.Name).ToList();

            // 3. Map view ↔ sheet qua viewports
            foreach (var sheet in sheets)
            {
                var viewportIds = sheet.GetAllViewports();
                var viewsInSheet = new List<ElementId>();

                foreach (var vpId in viewportIds)
                {
                    if (!(doc.GetElement(vpId) is Viewport vp)) continue;
                    var viewId = vp.ViewId;
                    if (viewId == null || viewId == ElementId.InvalidElementId) continue;

                    viewsInSheet.Add(viewId);

                    if (!index.ViewToSheets.TryGetValue(viewId, out var list))
                    {
                        list = new List<ElementId>();
                        index.ViewToSheets[viewId] = list;
                    }
                    if (!list.Contains(sheet.Id)) list.Add(sheet.Id);
                }

                index.SheetToViews[sheet.Id] = viewsInSheet.Distinct().ToList();
            }

            return index;
        }

        private static string ResolveAssemblyName(Document doc, ViewSheet sheet)
        {
            // ViewSheet không có property AssociatedAssemblyInstanceId trực tiếp;
            // nhưng View.AssociatedAssemblyInstanceId tồn tại trên cả ViewSheet (kế thừa View).
            var asmId = sheet.AssociatedAssemblyInstanceId;
            if (asmId == null || asmId == ElementId.InvalidElementId) return null;

            if (doc.GetElement(asmId) is AssemblyInstance asm)
            {
                // AssemblyTypeName là tên hiển thị của loại assembly (ví dụ "PC-COLUMN").
                var typeName = asm.AssemblyTypeName;
                return string.IsNullOrEmpty(typeName) ? asm.Name : typeName;
            }
            return null;
        }

        private static bool IsListableView(View v)
        {
            switch (v.ViewType)
            {
                case ViewType.Legend:
                case ViewType.FloorPlan:
                case ViewType.CeilingPlan:
                case ViewType.AreaPlan:
                case ViewType.EngineeringPlan:
                case ViewType.Section:
                case ViewType.Elevation:
                case ViewType.Detail:
                case ViewType.ThreeD:
                case ViewType.DraftingView:
                    return true;
                default:
                    return false;
            }
        }
    }
}
