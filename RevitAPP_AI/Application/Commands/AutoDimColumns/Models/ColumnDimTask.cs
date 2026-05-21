using Autodesk.Revit.DB;

namespace Aplication.Commands.AutoDimColumns.Models
{
    public class ColumnDimTask
    {
        public FamilyInstance Column { get; set; }
        public XYZ Center { get; set; }

        public Reference FacePlusX { get; set; }
        public Reference FaceMinusX { get; set; }
        public Reference FacePlusY { get; set; }
        public Reference FaceMinusY { get; set; }

        public double FaceXPlusCoord { get; set; }
        public double FaceXMinusCoord { get; set; }
        public double FaceYPlusCoord { get; set; }
        public double FaceYMinusCoord { get; set; }

        public Grid NearestGridX { get; set; }
        public Grid NearestGridY { get; set; }

        public string SkipReason { get; set; }
    }
}
