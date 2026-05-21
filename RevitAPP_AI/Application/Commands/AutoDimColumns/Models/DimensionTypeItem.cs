using Autodesk.Revit.DB;

namespace Aplication.Commands.AutoDimColumns.Models
{
    public class DimensionTypeItem
    {
        public ElementId Id { get; set; }
        public string Name { get; set; }

        public override string ToString() => Name;
    }
}
