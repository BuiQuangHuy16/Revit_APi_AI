using Autodesk.Revit.DB;

namespace Aplication.Commands.ExportSchedule.Models
{
    // POCO mô tả 1 ViewSchedule trong dự án (dùng để hiển thị trên UI).
    public class ScheduleItem
    {
        public ElementId Id { get; set; }
        public string UniqueId { get; set; }
        public string Name { get; set; }
    }
}
