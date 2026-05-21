using Autodesk.Revit.DB;

namespace Aplication.Commands.DuplicateLegend.Models
{
    public class DuplicateLegendOptions
    {
        public int CopiesPerLegend { get; set; } = 1;
        public PlacementMode Mode { get; set; } = PlacementMode.PickPoint;
        public double HorizontalSpacingMm { get; set; } = 10.0;
        public XYZ PickedPoint { get; set; }
    }
}
