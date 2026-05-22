using Autodesk.Revit.DB;

namespace Aplication.Commands.DuplicateLegend.Models
{
    public class SelectedViewport
    {
        public ElementId ViewportId { get; set; }
        public ElementId LegendId { get; set; }
        public string LegendName { get; set; }
        public XYZ Center { get; set; }
        public ElementId TypeId { get; set; }
    }
}
