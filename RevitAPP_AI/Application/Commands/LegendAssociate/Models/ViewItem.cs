using Autodesk.Revit.DB;

namespace Aplication.Commands.LegendAssociate.Models
{
    public class ViewItem
    {
        public ElementId Id { get; set; }
        public string Name { get; set; }
        public bool IsLegend { get; set; }
        public string ViewTypeLabel { get; set; }

        public string DisplayName => string.IsNullOrEmpty(ViewTypeLabel)
            ? Name
            : $"{Name}  ·  {ViewTypeLabel}";

        public override string ToString() => DisplayName;
    }
}
