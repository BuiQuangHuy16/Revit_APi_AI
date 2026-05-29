using Autodesk.Revit.DB;

namespace Aplication.Commands.ExportCAD.Models
{
    // POCO mô tả 1 View/Sheet có thể export ra CAD.
    // GroupLabel dùng để gom nhóm trên UI (Sheets, Floor Plans, Sections, ...).
    public class ExportViewItem
    {
        public ElementId Id { get; set; }
        public string UniqueId { get; set; }
        public string Name { get; set; }
        public string SheetNumber { get; set; }
        public string GroupLabel { get; set; }
        public ViewType ViewType { get; set; }
    }
}
