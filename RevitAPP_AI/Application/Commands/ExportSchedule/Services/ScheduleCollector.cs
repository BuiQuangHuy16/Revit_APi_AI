using System.Collections.Generic;
using System.Linq;
using Aplication.Commands.ExportSchedule.Models;
using Autodesk.Revit.DB;

namespace Aplication.Commands.ExportSchedule.Services
{
    // Quét toàn bộ ViewSchedule trong dự án và lọc bỏ những schedule không
    // phải do người dùng tạo (Revision Schedules trên titleblock, Internal
    // Keynote Schedules, các schedule template). Mục đích: danh sách hiển
    // thị trên UI khớp với những gì người dùng nhìn thấy trong Project Browser.
    internal static class ScheduleCollector
    {
        public static IReadOnlyList<ScheduleItem> Run(Document doc)
        {
            return new FilteredElementCollector(doc)
                .OfClass(typeof(ViewSchedule))
                .Cast<ViewSchedule>()
                .Where(IsUserSchedule)
                .OrderBy(vs => vs.Name, System.StringComparer.OrdinalIgnoreCase)
                .Select(vs => new ScheduleItem
                {
                    Id = vs.Id,
                    UniqueId = vs.UniqueId,
                    Name = vs.Name
                })
                .ToList();
        }

        private static bool IsUserSchedule(ViewSchedule vs)
        {
            if (vs == null) return false;
            if (vs.IsTemplate) return false;
            if (vs.IsTitleblockRevisionSchedule) return false;
            if (vs.IsInternalKeynoteSchedule) return false;
            return true;
        }
    }
}
