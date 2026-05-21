namespace Aplication.Commands.AutoDimColumns.Models
{
    public class AutoDimOptions
    {
        public const string DimMarker = "AUTO_COL_DIM_v1";

        public string SelectedDimTypeName { get; set; }
        public double OffsetMm { get; set; } = 500;
        public double TextClearanceMm { get; set; } = 50;
        public bool IncludeOverallGridChain { get; set; }
        public bool SkipColumnsWithoutNearbyGrid { get; set; } = true;
        public double MaxGridSearchRadiusMm { get; set; } = 5000;
    }
}
