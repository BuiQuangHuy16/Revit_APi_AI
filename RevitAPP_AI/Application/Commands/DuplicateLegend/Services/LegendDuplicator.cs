using System;
using System.Collections.Generic;
using System.Linq;
using Aplication.Commands.DuplicateLegend.Models;
using Autodesk.Revit.DB;

namespace Aplication.Commands.DuplicateLegend.Services
{
    public class LegendDuplicationResult
    {
        public int CreatedCount;
        public List<string> Errors = new List<string>();
        public ElementId FirstCreatedViewportId;
    }

    public static class LegendDuplicator
    {
        public static LegendDuplicationResult Run(
            Document doc,
            ViewSheet targetSheet,
            IEnumerable<LegendItem> selectedLegends,
            DuplicateLegendOptions options)
        {
            var result = new LegendDuplicationResult();
            var viewNames = LoadExistingViewNames(doc);

            var spacingFt = UnitUtils.ConvertToInternalUnits(options.HorizontalSpacingMm, UnitTypeId.Millimeters);
            double cursorX = options.PickedPoint?.X ?? 0.0;
            double cursorY = options.PickedPoint?.Y ?? 0.0;

            using (var tx = new Transaction(doc, "Duplicate Legends"))
            {
                tx.Start();

                foreach (var item in selectedLegends)
                {
                    var sourceLegend = doc.GetElement(item.LegendId) as View;
                    if (sourceLegend == null || sourceLegend.ViewType != ViewType.Legend) continue;

                    var copies = options.Mode == PlacementMode.Replace ? 1 : Math.Max(1, options.CopiesPerLegend);

                    for (var copyIndex = 1; copyIndex <= copies; copyIndex++)
                    {
                        using (var sub = new SubTransaction(doc))
                        {
                            sub.Start();
                            try
                            {
                                Viewport newVp;
                                if (options.Mode == PlacementMode.PickPoint)
                                {
                                    newVp = PlacePickPoint(doc, targetSheet, sourceLegend, ref cursorX, cursorY, spacingFt, copyIndex, viewNames);
                                }
                                else
                                {
                                    newVp = PlaceReplace(doc, targetSheet, sourceLegend, copyIndex, viewNames);
                                    if (newVp == null)
                                    {
                                        sub.RollBack();
                                        result.Errors.Add($"Legend '{sourceLegend.Name}' không có viewport trên sheet hiện tại.");
                                        continue;
                                    }
                                }

                                sub.Commit();

                                result.CreatedCount++;
                                if (result.FirstCreatedViewportId == null || result.FirstCreatedViewportId == ElementId.InvalidElementId)
                                    result.FirstCreatedViewportId = newVp.Id;
                            }
                            catch (Exception ex)
                            {
                                sub.RollBack();
                                result.Errors.Add($"Legend '{sourceLegend.Name}' (copy {copyIndex}): {ex.Message}");
                            }
                        }
                    }
                }

                tx.Commit();
            }

            return result;
        }

        private static Viewport PlacePickPoint(
            Document doc,
            ViewSheet sheet,
            View sourceLegend,
            ref double cursorX,
            double cursorY,
            double spacingFt,
            int copyIndex,
            HashSet<string> viewNames)
        {
            var newViewId = DuplicateLegendView(doc, sourceLegend, copyIndex, viewNames);
            var placePoint = new XYZ(cursorX, cursorY, 0);
            var vp = Viewport.Create(doc, sheet.Id, newViewId, placePoint);

            var outline = vp.GetBoxOutline();
            var width = outline.MaximumPoint.X - outline.MinimumPoint.X;
            cursorX += width + spacingFt;

            return vp;
        }

        private static Viewport PlaceReplace(
            Document doc,
            ViewSheet sheet,
            View sourceLegend,
            int copyIndex,
            HashSet<string> viewNames)
        {
            var existing = new FilteredElementCollector(doc, sheet.Id)
                .OfClass(typeof(Viewport))
                .Cast<Viewport>()
                .FirstOrDefault(v => v.ViewId == sourceLegend.Id);

            if (existing == null) return null;

            var center = existing.GetBoxCenter();
            var existingTypeId = existing.GetTypeId();

            doc.Delete(existing.Id);

            var newViewId = DuplicateLegendView(doc, sourceLegend, copyIndex, viewNames);
            var vp = Viewport.Create(doc, sheet.Id, newViewId, center);

            try { vp.ChangeTypeId(existingTypeId); } catch { }

            return vp;
        }

        private static ElementId DuplicateLegendView(
            Document doc,
            View sourceLegend,
            int copyIndex,
            HashSet<string> viewNames)
        {
            if (!sourceLegend.CanViewBeDuplicated(ViewDuplicateOption.Duplicate))
                throw new InvalidOperationException($"Legend '{sourceLegend.Name}' không thể duplicate.");

            var newId = sourceLegend.Duplicate(ViewDuplicateOption.Duplicate);
            if (newId == null || newId == ElementId.InvalidElementId)
                throw new InvalidOperationException($"Legend '{sourceLegend.Name}' không thể duplicate.");

            if (doc.GetElement(newId) is View newView)
            {
                var uniqueName = ResolveUniqueName(sourceLegend.Name, viewNames, copyIndex);
                try { newView.Name = uniqueName; } catch { /* keep auto name */ }
            }

            // Legend.Duplicate() returns an empty legend view — must copy contents manually.
            var contentIds = new FilteredElementCollector(doc, sourceLegend.Id)
                .WhereElementIsNotElementType()
                .Where(e => e.OwnerViewId == sourceLegend.Id)
                .Select(e => e.Id)
                .ToList();

            if (contentIds.Count > 0)
            {
                try
                {
                    ElementTransformUtils.CopyElements(
                        sourceLegend,
                        contentIds,
                        doc.GetElement(newId) as View,
                        Transform.Identity,
                        new CopyPasteOptions());
                }
                catch
                {
                    // best-effort: ignore elements that cannot be copied
                }
            }

            return newId;
        }

        private static HashSet<string> LoadExistingViewNames(Document doc)
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var v in new FilteredElementCollector(doc).OfClass(typeof(View)).Cast<View>())
            {
                if (!string.IsNullOrEmpty(v.Name)) set.Add(v.Name);
            }
            return set;
        }

        private static string ResolveUniqueName(string originalName, HashSet<string> used, int preferredIndex)
        {
            var preferred = $"{originalName} Copy {preferredIndex}";
            if (!used.Contains(preferred))
            {
                used.Add(preferred);
                return preferred;
            }

            for (var i = 1; i < 10000; i++)
            {
                var candidate = $"{originalName} Copy {i}";
                if (used.Contains(candidate)) continue;
                used.Add(candidate);
                return candidate;
            }

            return originalName + " Copy " + Guid.NewGuid().ToString("N").Substring(0, 6);
        }
    }
}
