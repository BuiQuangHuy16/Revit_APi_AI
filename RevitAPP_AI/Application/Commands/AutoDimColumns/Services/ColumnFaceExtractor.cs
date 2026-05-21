using System;
using System.Collections.Generic;
using Aplication.Commands.AutoDimColumns.Models;
using Autodesk.Revit.DB;

namespace Aplication.Commands.AutoDimColumns.Services
{
    public static class ColumnFaceExtractor
    {
        private const double Tolerance = 1e-6;

        public static bool TryExtract(FamilyInstance column, View view, ColumnDimTask task)
        {
            if (column.Location is LocationPoint lp)
            {
                if (Math.Abs(NormalizeAngle(lp.Rotation)) > 1e-3)
                {
                    task.SkipReason = $"Cột '{column.Name}' bị xoay — chỉ hỗ trợ cột thẳng theo trục project.";
                    return false;
                }
                task.Center = lp.Point;
            }
            else
            {
                task.SkipReason = $"Cột '{column.Name}' không có LocationPoint.";
                return false;
            }

            var opts = new Options
            {
                ComputeReferences = true,
                IncludeNonVisibleObjects = false,
                View = view
            };

            var planarFaces = new List<PlanarFace>();
            CollectVerticalPlanarFaces(column.get_Geometry(opts), planarFaces);

            if (planarFaces.Count < 4)
            {
                task.SkipReason = $"Cột '{column.Name}' không phải cột vuông/chữ nhật (chỉ có {planarFaces.Count} mặt thẳng đứng).";
                return false;
            }

            PlanarFace facePlusX = null, faceMinusX = null, facePlusY = null, faceMinusY = null;
            foreach (var f in planarFaces)
            {
                var n = f.FaceNormal;
                if (Math.Abs(n.X) > 1 - 1e-3)
                {
                    if (n.X > 0 && (facePlusX == null || f.Origin.X > facePlusX.Origin.X)) facePlusX = f;
                    if (n.X < 0 && (faceMinusX == null || f.Origin.X < faceMinusX.Origin.X)) faceMinusX = f;
                }
                else if (Math.Abs(n.Y) > 1 - 1e-3)
                {
                    if (n.Y > 0 && (facePlusY == null || f.Origin.Y > facePlusY.Origin.Y)) facePlusY = f;
                    if (n.Y < 0 && (faceMinusY == null || f.Origin.Y < faceMinusY.Origin.Y)) faceMinusY = f;
                }
            }

            if (facePlusX == null || faceMinusX == null || facePlusY == null || faceMinusY == null)
            {
                task.SkipReason = $"Cột '{column.Name}' không đủ 4 mặt vuông góc trục project.";
                return false;
            }

            task.FacePlusX = facePlusX.Reference;
            task.FaceMinusX = faceMinusX.Reference;
            task.FacePlusY = facePlusY.Reference;
            task.FaceMinusY = faceMinusY.Reference;

            task.FaceXPlusCoord = facePlusX.Origin.X;
            task.FaceXMinusCoord = faceMinusX.Origin.X;
            task.FaceYPlusCoord = facePlusY.Origin.Y;
            task.FaceYMinusCoord = faceMinusY.Origin.Y;

            return true;
        }

        private static void CollectVerticalPlanarFaces(GeometryElement geom, List<PlanarFace> acc)
        {
            if (geom == null) return;
            foreach (var obj in geom)
            {
                switch (obj)
                {
                    case Solid solid when solid.Faces.Size > 0 && solid.Volume > Tolerance:
                        foreach (Face f in solid.Faces)
                        {
                            if (f is PlanarFace pf && Math.Abs(pf.FaceNormal.Z) < 1e-3)
                                acc.Add(pf);
                        }
                        break;
                    case GeometryInstance gi:
                        CollectVerticalPlanarFaces(gi.GetInstanceGeometry(), acc);
                        break;
                }
            }
        }

        private static double NormalizeAngle(double radians)
        {
            const double twoPi = Math.PI * 2;
            var r = radians % twoPi;
            if (r > Math.PI) r -= twoPi;
            if (r < -Math.PI) r += twoPi;
            if (Math.Abs(Math.Abs(r) - Math.PI / 2) < 1e-3) return 0;
            if (Math.Abs(Math.Abs(r) - Math.PI) < 1e-3) return 0;
            return r;
        }
    }
}
