using System;
using System.Collections.Generic;
using System.Linq;
using Aplication.Commands.ExportCAD.Models;
using Autodesk.Revit.DB;

namespace Aplication.Commands.ExportCAD.Services
{
    // Quét toàn bộ View/Sheet có thể export ra DWG. Loại bỏ template,
    // schedule, project browser, system browser, internal views và các
    // view không thể in (Revit không cho export những view này ra DWG).
    internal static class ExportableViewCollector
    {
        public static IReadOnlyList<ExportViewItem> Run(Document doc)
        {
            var items = new List<ExportViewItem>();

            var views = new FilteredElementCollector(doc)
                .OfClass(typeof(View))
                .Cast<View>()
                .Where(IsExportable);

            foreach (var v in views)
            {
                string sheetNumber = null;
                string displayName = v.Name;

                if (v is ViewSheet sheet)
                {
                    sheetNumber = sheet.SheetNumber;
                    displayName = $"{sheet.SheetNumber} - {sheet.Name}";
                }

                items.Add(new ExportViewItem
                {
                    Id = v.Id,
                    UniqueId = v.UniqueId,
                    Name = displayName,
                    SheetNumber = sheetNumber,
                    GroupLabel = GetGroupLabel(v.ViewType),
                    ViewType = v.ViewType
                });
            }

            // Sắp xếp: Sheets trước, sau đó theo nhóm rồi theo tên/số sheet.
            return items
                .OrderBy(i => GroupOrder(i.ViewType))
                .ThenBy(i => i.SheetNumber ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .ThenBy(i => i.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static bool IsExportable(View v)
        {
            if (v == null) return false;
            if (v.IsTemplate) return false;
            if (!v.CanBePrinted) return false;

            // Allow-list: chỉ những ViewType có thể export ra DWG. Tất cả các loại
            // khác (Schedule, ProjectBrowser, Report,...) fall through return false.
            switch (v.ViewType)
            {
                case ViewType.FloorPlan:
                case ViewType.CeilingPlan:
                case ViewType.AreaPlan:
                case ViewType.EngineeringPlan:
                case ViewType.Elevation:
                case ViewType.Section:
                case ViewType.Detail:
                case ViewType.ThreeD:
                case ViewType.DraftingView:
                case ViewType.Legend:
                case ViewType.DrawingSheet:
                case ViewType.Rendering:
                    return true;
                default:
                    return false;
            }
        }

        private static string GetGroupLabel(ViewType vt)
        {
            switch (vt)
            {
                case ViewType.DrawingSheet: return "Sheets";
                case ViewType.FloorPlan: return "Floor Plans";
                case ViewType.CeilingPlan: return "Ceiling Plans";
                case ViewType.AreaPlan: return "Area Plans";
                case ViewType.EngineeringPlan: return "Structural Plans";
                case ViewType.Elevation: return "Elevations";
                case ViewType.Section: return "Sections";
                case ViewType.Detail: return "Detail Views";
                case ViewType.ThreeD: return "3D Views";
                case ViewType.DraftingView: return "Drafting Views";
                case ViewType.Legend: return "Legends";
                case ViewType.Rendering: return "Renderings";
                default: return "Other";
            }
        }

        // Sheets ưu tiên hiển thị đầu tiên (use case phổ biến nhất là export sheet).
        private static int GroupOrder(ViewType vt)
        {
            switch (vt)
            {
                case ViewType.DrawingSheet: return 0;
                case ViewType.FloorPlan: return 1;
                case ViewType.CeilingPlan: return 2;
                case ViewType.EngineeringPlan: return 3;
                case ViewType.AreaPlan: return 4;
                case ViewType.Elevation: return 5;
                case ViewType.Section: return 6;
                case ViewType.Detail: return 7;
                case ViewType.ThreeD: return 8;
                case ViewType.DraftingView: return 9;
                case ViewType.Legend: return 10;
                case ViewType.Rendering: return 11;
                default: return 99;
            }
        }
    }
}
