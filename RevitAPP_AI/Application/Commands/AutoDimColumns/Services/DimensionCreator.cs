using System.Collections.Generic;
using System.Linq;
using Aplication.Commands.AutoDimColumns.Models;
using Autodesk.Revit.DB;

namespace Aplication.Commands.AutoDimColumns.Services
{
    public static class DimensionCreator
    {
        private enum AxisKind { X, Y }

        public static int CreateForColumn(
            Document doc, View view, ColumnDimTask task,
            AutoDimOptions opts, DimensionType dimType)
        {
            var off1 = ToFt(opts.OffsetMm);
            var off2 = 2 * off1;
            var clearance = ToFt(opts.TextClearanceMm);
            var z = task.Center.Z;
            int created = 0;

            // ── X-axis: dim placed BELOW column, dim line runs along X ──
            {
                var yDim1 = task.FaceYMinusCoord - off1;
                var yDim2 = task.FaceYMinusCoord - off2;

                var line1 = Line.CreateBound(
                    new XYZ(task.FaceXMinusCoord, yDim1, z),
                    new XYZ(task.FaceXPlusCoord, yDim1, z));
                var refs1 = new ReferenceArray();
                refs1.Append(task.FaceMinusX);
                if (task.NearestGridX != null) refs1.Append(new Reference(task.NearestGridX));
                refs1.Append(task.FacePlusX);
                var dim1 = TryCreate(doc, view, line1, refs1, dimType);
                if (dim1 != null) created++;

                var line2 = Line.CreateBound(
                    new XYZ(task.FaceXMinusCoord, yDim2, z),
                    new XYZ(task.FaceXPlusCoord, yDim2, z));
                var refs2 = new ReferenceArray();
                refs2.Append(task.FaceMinusX);
                refs2.Append(task.FacePlusX);
                var dim2 = TryCreate(doc, view, line2, refs2, dimType);
                if (dim2 != null) created++;

                var witnessXs = new List<double> { task.FaceXMinusCoord, task.FaceXPlusCoord };
                if (task.NearestGridX != null)
                    witnessXs.Add(((Line)task.NearestGridX.Curve).Origin.X);
                if (dim1 != null) ShiftTextsIfOverlap(dim1, witnessXs, clearance, AxisKind.X, yDim1, z);
                if (dim2 != null) ShiftTextsIfOverlap(dim2, witnessXs, clearance, AxisKind.X, yDim2, z);
            }

            // ── Y-axis: dim placed LEFT of column, dim line runs along Y ──
            {
                var xDim1 = task.FaceXMinusCoord - off1;
                var xDim2 = task.FaceXMinusCoord - off2;

                var line1 = Line.CreateBound(
                    new XYZ(xDim1, task.FaceYMinusCoord, z),
                    new XYZ(xDim1, task.FaceYPlusCoord, z));
                var refs1 = new ReferenceArray();
                refs1.Append(task.FaceMinusY);
                if (task.NearestGridY != null) refs1.Append(new Reference(task.NearestGridY));
                refs1.Append(task.FacePlusY);
                var dim1 = TryCreate(doc, view, line1, refs1, dimType);
                if (dim1 != null) created++;

                var line2 = Line.CreateBound(
                    new XYZ(xDim2, task.FaceYMinusCoord, z),
                    new XYZ(xDim2, task.FaceYPlusCoord, z));
                var refs2 = new ReferenceArray();
                refs2.Append(task.FaceMinusY);
                refs2.Append(task.FacePlusY);
                var dim2 = TryCreate(doc, view, line2, refs2, dimType);
                if (dim2 != null) created++;

                var witnessYs = new List<double> { task.FaceYMinusCoord, task.FaceYPlusCoord };
                if (task.NearestGridY != null)
                    witnessYs.Add(((Line)task.NearestGridY.Curve).Origin.Y);
                if (dim1 != null) ShiftTextsIfOverlap(dim1, witnessYs, clearance, AxisKind.Y, xDim1, z);
                if (dim2 != null) ShiftTextsIfOverlap(dim2, witnessYs, clearance, AxisKind.Y, xDim2, z);
            }

            return created;
        }

        public static int CreateOverallGridChain(
            Document doc, View view, GridLocator locator,
            AutoDimOptions opts, DimensionType dimType)
        {
            var off = ToFt(opts.OffsetMm);
            int created = 0;

            if (locator.VerticalGrids.Count >= 2)
            {
                var xs = locator.VerticalGrids.Select(g => ((Line)g.Curve).Origin.X).ToList();
                var minX = xs.Min();
                var maxX = xs.Max();
                var topY = locator.HorizontalGrids.Count > 0
                    ? locator.HorizontalGrids.Max(g => ((Line)g.Curve).Origin.Y) + off * 4
                    : ((Line)locator.VerticalGrids.First().Curve).GetEndPoint(1).Y + off;
                var z = ((Line)locator.VerticalGrids.First().Curve).Origin.Z;

                var line = Line.CreateBound(new XYZ(minX, topY, z), new XYZ(maxX, topY, z));
                var refs = new ReferenceArray();
                foreach (var g in locator.VerticalGrids) refs.Append(new Reference(g));
                if (TryCreate(doc, view, line, refs, dimType) != null) created++;
            }

            if (locator.HorizontalGrids.Count >= 2)
            {
                var ys = locator.HorizontalGrids.Select(g => ((Line)g.Curve).Origin.Y).ToList();
                var minY = ys.Min();
                var maxY = ys.Max();
                var leftX = locator.VerticalGrids.Count > 0
                    ? locator.VerticalGrids.Min(g => ((Line)g.Curve).Origin.X) - off * 4
                    : ((Line)locator.HorizontalGrids.First().Curve).GetEndPoint(0).X - off;
                var z = ((Line)locator.HorizontalGrids.First().Curve).Origin.Z;

                var line = Line.CreateBound(new XYZ(leftX, minY, z), new XYZ(leftX, maxY, z));
                var refs = new ReferenceArray();
                foreach (var g in locator.HorizontalGrids) refs.Append(new Reference(g));
                if (TryCreate(doc, view, line, refs, dimType) != null) created++;
            }

            return created;
        }

        private static Dimension TryCreate(Document doc, View view, Line dimLine, ReferenceArray refs, DimensionType dimType)
        {
            try
            {
                var dim = doc.Create.NewDimension(view, dimLine, refs, dimType);
                if (dim != null)
                {
                    var p = dim.LookupParameter("Comments");
                    p?.Set(AutoDimOptions.DimMarker);
                }
                return dim;
            }
            catch
            {
                return null;
            }
        }

        private static void ShiftTextsIfOverlap(
            Dimension dim, List<double> witnessCoords, double clearance,
            AxisKind axis, double dimLineOtherCoord, double z)
        {
            try
            {
                if (dim.NumberOfSegments == 0)
                {
                    // Single-segment dim (vd dim 2 face-face) — operate on dim.TextPosition.
                    var newPos = ComputeShiftedPosition(dim.TextPosition, witnessCoords, clearance, axis, dimLineOtherCoord, z);
                    if (newPos != null)
                    {
                        dim.TextPosition = newPos;
                        dim.HasLeader = true;
                    }
                }
                else
                {
                    // Multi-segment dim (vd dim 1 face-grid-face) — operate on each segment.
                    foreach (DimensionSegment seg in dim.Segments)
                    {
                        if (!seg.IsTextPositionAdjustable()) continue;
                        var newPos = ComputeShiftedPosition(seg.TextPosition, witnessCoords, clearance, axis, dimLineOtherCoord, z);
                        if (newPos != null) seg.TextPosition = newPos;
                    }
                }
            }
            catch
            {
                // Some DimensionTypes lock TextPosition — skip silently.
            }
        }

        private static XYZ ComputeShiftedPosition(
            XYZ currentText, List<double> witnessCoords, double clearance,
            AxisKind axis, double dimLineOtherCoord, double z)
        {
            if (currentText == null) return null;
            var textCoord = axis == AxisKind.X ? currentText.X : currentText.Y;

            var minDist = witnessCoords.Min(w => System.Math.Abs(textCoord - w));
            if (minDist >= clearance) return null;

            // Fewer witnesses on a side = more open space on that side.
            var leftCount = witnessCoords.Count(w => w < textCoord);
            var rightCount = witnessCoords.Count(w => w > textCoord);

            bool shiftRight = rightCount <= leftCount;
            var shiftAmount = clearance * 2;
            double newCoord = shiftRight
                ? witnessCoords.Max() + shiftAmount
                : witnessCoords.Min() - shiftAmount;

            return axis == AxisKind.X
                ? new XYZ(newCoord, dimLineOtherCoord, z)
                : new XYZ(dimLineOtherCoord, newCoord, z);
        }

        private static double ToFt(double mm) =>
            UnitUtils.ConvertToInternalUnits(mm, UnitTypeId.Millimeters);
    }
}
