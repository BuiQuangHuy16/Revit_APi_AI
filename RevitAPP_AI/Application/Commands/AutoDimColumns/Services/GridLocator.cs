using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace Aplication.Commands.AutoDimColumns.Services
{
    /// <summary>
    /// Vertical grid = grid line that separates columns along X axis (i.e. its line runs along Y).
    /// Horizontal grid = grid line that separates rows along Y axis (i.e. its line runs along X).
    /// </summary>
    public class GridLocator
    {
        public IReadOnlyList<Grid> VerticalGrids { get; }
        public IReadOnlyList<Grid> HorizontalGrids { get; }

        public GridLocator(Document doc, View view)
        {
            var all = new FilteredElementCollector(doc, view.Id)
                .OfClass(typeof(Grid))
                .Cast<Grid>()
                .Where(g => g.Curve is Line)
                .ToList();

            VerticalGrids = all
                .Where(g => IsAlongAxis(((Line)g.Curve).Direction, isAlongY: true))
                .OrderBy(g => ((Line)g.Curve).Origin.X)
                .ToList();

            HorizontalGrids = all
                .Where(g => IsAlongAxis(((Line)g.Curve).Direction, isAlongY: false))
                .OrderBy(g => ((Line)g.Curve).Origin.Y)
                .ToList();
        }

        public (Grid nearestX, Grid nearestY) FindNearest(XYZ columnCenter, double maxRadiusFt)
        {
            Grid nearestX = null, nearestY = null;
            double bestDx = double.MaxValue, bestDy = double.MaxValue;

            foreach (var g in VerticalGrids)
            {
                var gx = ((Line)g.Curve).Origin.X;
                var d = Math.Abs(gx - columnCenter.X);
                if (d < bestDx)
                {
                    bestDx = d;
                    nearestX = g;
                }
            }

            foreach (var g in HorizontalGrids)
            {
                var gy = ((Line)g.Curve).Origin.Y;
                var d = Math.Abs(gy - columnCenter.Y);
                if (d < bestDy)
                {
                    bestDy = d;
                    nearestY = g;
                }
            }

            if (bestDx > maxRadiusFt) nearestX = null;
            if (bestDy > maxRadiusFt) nearestY = null;

            return (nearestX, nearestY);
        }

        private static bool IsAlongAxis(XYZ dir, bool isAlongY)
        {
            const double eps = 1e-3;
            return isAlongY
                ? Math.Abs(dir.X) < eps && Math.Abs(dir.Y) > 1 - eps
                : Math.Abs(dir.Y) < eps && Math.Abs(dir.X) > 1 - eps;
        }
    }
}
