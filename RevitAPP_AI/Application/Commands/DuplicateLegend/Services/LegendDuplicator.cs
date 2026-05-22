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
            ViewSheet sheet,
            IEnumerable<SelectedViewport> selection,
            DuplicateLegendOptions options)
        {
            var result = new LegendDuplicationResult();
            var viewNames = LoadExistingViewNames(doc);
            var titleType = FindTitleViewportType(doc);

            var spacingFt = UnitUtils.ConvertToInternalUnits(options.HorizontalSpacingMm, UnitTypeId.Millimeters);
            double cursorX = options.PickedPoint?.X ?? 0.0;
            double cursorY = options.PickedPoint?.Y ?? 0.0;

            using (var tx = new Transaction(doc, "Duplicate Legends"))
            {
                tx.Start();

                // CRITICAL: chặn Revit auto-rollback SubTransaction khi gặp warning
                // (View.Duplicate / Viewport.Create / Regenerate đều có thể sinh warning).
                var fho = tx.GetFailureHandlingOptions();
                fho.SetFailuresPreprocessor(new SuppressWarningsPreprocessor());
                fho.SetForcedModalHandling(false);
                fho.SetClearAfterRollback(true);
                fho.SetDelayedMiniWarnings(true);
                tx.SetFailureHandlingOptions(fho);

                foreach (var sv in selection)
                {
                    var sourceLegend = doc.GetElement(sv.LegendId) as View;
                    if (sourceLegend == null || sourceLegend.ViewType != ViewType.Legend) continue;

                    using (var sub = new SubTransaction(doc))
                    {
                        sub.Start();
                        try
                        {
                            Viewport newVp;
                            if (options.Mode == PlacementMode.PickPoint)
                            {
                                newVp = PlacePickPoint(doc, sheet, sv, sourceLegend, ref cursorX, cursorY, spacingFt, viewNames, titleType);
                            }
                            else
                            {
                                newVp = PlaceReplace(doc, sheet, sv, sourceLegend, viewNames, titleType);
                            }

                            // Revit có thể đã tự rollback sub do warning trong khi regenerate.
                            // Chỉ commit nếu sub thực sự còn ở trạng thái Started.
                            if (sub.GetStatus() == TransactionStatus.Started)
                            {
                                sub.Commit();

                                result.CreatedCount++;
                                if (result.FirstCreatedViewportId == null || result.FirstCreatedViewportId == ElementId.InvalidElementId)
                                    result.FirstCreatedViewportId = newVp.Id;
                            }
                            else
                            {
                                result.Errors.Add($"Legend '{sourceLegend.Name}': Revit huỷ thao tác (status={sub.GetStatus()}).");
                            }
                        }
                        catch (Exception ex)
                        {
                            if (sub.GetStatus() == TransactionStatus.Started)
                            {
                                try { sub.RollBack(); } catch { /* sub đã ended */ }
                            }
                            result.Errors.Add($"Legend '{sourceLegend.Name}': {ex.Message}");
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
            SelectedViewport sv,
            View sourceLegend,
            ref double cursorX,
            double cursorY,
            double spacingFt,
            HashSet<string> viewNames,
            ElementType titleType)
        {
            // Đo width từ source viewport (đã regenerate sẵn) — tránh phụ thuộc
            // vào regen sau Viewport.Create.
            var sourceVp = doc.GetElement(sv.ViewportId) as Viewport;
            var width = sourceVp != null
                ? TryReadOutlineWidth(sourceVp, fallback: spacingFt * 10)
                : spacingFt * 10;

            var newViewId = DuplicateLegendView(doc, sourceLegend, viewNames);

            // Đặt CENTER tại (cursorX + width/2) để mép trái = cursorX.
            var placeCenter = new XYZ(cursorX + width / 2.0, cursorY, 0);
            var vp = Viewport.Create(doc, sheet.Id, newViewId, placeCenter);
            if (vp == null)
                throw new InvalidOperationException("Không tạo được viewport (vị trí ngoài sheet hoặc bị Revit từ chối).");

            // Áp viewport type có hiển thị title TRƯỚC khi regenerate để box bao gồm title bar.
            if (titleType != null)
            {
                try { vp.ChangeTypeId(titleType.Id); } catch { }
            }

            doc.Regenerate();

            // Viewport.Create() đặt ORIGIN tại point, không phải box center.
            // SetBoxCenter() buộc box center vào đúng vị trí mong muốn.
            try { vp.SetBoxCenter(placeCenter); } catch { }

            cursorX += width + spacingFt;

            return vp;
        }

        private static Viewport PlaceReplace(
            Document doc,
            ViewSheet sheet,
            SelectedViewport sv,
            View sourceLegend,
            HashSet<string> viewNames,
            ElementType titleType)
        {
            var existing = doc.GetElement(sv.ViewportId) as Viewport;
            if (existing == null)
                throw new InvalidOperationException("Viewport gốc không còn trên sheet.");

            // Dùng giá trị đã capture lúc chọn. Fallback nếu thiếu.
            var center = sv.Center ?? existing.GetBoxCenter();
            var existingTypeId = sv.TypeId ?? existing.GetTypeId();

            doc.Delete(sv.ViewportId);

            var newViewId = DuplicateLegendView(doc, sourceLegend, viewNames);
            var vp = Viewport.Create(doc, sheet.Id, newViewId, center);
            if (vp == null)
                throw new InvalidOperationException("Không tạo được viewport mới tại vị trí gốc.");

            // Áp viewport type TRƯỚC khi Regenerate để box outline bao gồm title bar.
            // Ưu tiên type của viewport gốc; nếu type đó KHÔNG hiển thị label thì
            // fallback sang titleType để title luôn hiện ra trên viewport mới.
            var typeIdToApply = ResolveTypeShowingLabel(doc, existingTypeId, titleType);
            if (typeIdToApply != null && typeIdToApply != ElementId.InvalidElementId)
            {
                try { vp.ChangeTypeId(typeIdToApply); } catch { }
            }

            doc.Regenerate();

            // FIX vị trí: Viewport.Create dùng point làm ORIGIN, không phải BOX CENTER.
            // Phải SetBoxCenter sau khi đã áp type (box outline đã gồm title bar).
            try { vp.SetBoxCenter(center); } catch { }

            return vp;
        }

        private static ElementId ResolveTypeShowingLabel(Document doc, ElementId preferredTypeId, ElementType titleType)
        {
            if (preferredTypeId != null && preferredTypeId != ElementId.InvalidElementId)
            {
                if (doc.GetElement(preferredTypeId) is ElementType pref)
                {
                    var p = pref.get_Parameter(BuiltInParameter.VIEWPORT_ATTR_SHOW_LABEL);
                    if (p != null && p.AsInteger() == 1)
                        return pref.Id;
                }
            }
            return titleType?.Id;
        }

        private static double TryReadOutlineWidth(Viewport vp, double fallback)
        {
            try
            {
                var outline = vp.GetBoxOutline();
                if (outline == null) return fallback;
                var min = outline.MinimumPoint;
                var max = outline.MaximumPoint;
                if (min == null || max == null) return fallback;
                var width = max.X - min.X;
                return width > 0 ? width : fallback;
            }
            catch
            {
                return fallback;
            }
        }

        private static ElementId DuplicateLegendView(
            Document doc,
            View sourceLegend,
            HashSet<string> viewNames)
        {
            // CRITICAL: dùng WithDetailing để Revit tự copy nội dung legend.
            // KHÔNG dùng Duplicate + ElementTransformUtils.CopyElements vì gây
            // lỗi đè nét (lines bị nhân đôi) trên legend mới.
            var option = sourceLegend.CanViewBeDuplicated(ViewDuplicateOption.WithDetailing)
                ? ViewDuplicateOption.WithDetailing
                : ViewDuplicateOption.Duplicate;

            var newId = sourceLegend.Duplicate(option);
            if (newId == null || newId == ElementId.InvalidElementId)
                throw new InvalidOperationException($"Legend '{sourceLegend.Name}' không thể duplicate.");

            var newView = doc.GetElement(newId) as View;
            if (newView == null)
                throw new InvalidOperationException($"Không lấy được legend duplicate cho '{sourceLegend.Name}'.");

            var uniqueName = ResolveUniqueName(sourceLegend.Name, viewNames);
            try { newView.Name = uniqueName; } catch { /* keep auto name */ }

            return newId;
        }

        private static ElementType FindTitleViewportType(Document doc)
        {
            return new FilteredElementCollector(doc)
                .OfClass(typeof(ElementType))
                .OfCategory(BuiltInCategory.OST_Viewports)
                .Cast<ElementType>()
                .FirstOrDefault(t =>
                {
                    var p = t.get_Parameter(BuiltInParameter.VIEWPORT_ATTR_SHOW_LABEL);
                    return p != null && p.AsInteger() == 1;
                });
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

        private static string ResolveUniqueName(string originalName, HashSet<string> used)
        {
            for (var i = 1; i < 10000; i++)
            {
                var candidate = $"{originalName} - Copy {i}";
                if (used.Contains(candidate)) continue;
                used.Add(candidate);
                return candidate;
            }
            return originalName + " - Copy " + Guid.NewGuid().ToString("N").Substring(0, 6);
        }
    }

    /// Xoá mọi warning trước khi Revit tự rollback SubTransaction.
    /// Cần thiết vì View.Duplicate/Viewport.Create/Regenerate sinh warnings
    /// và default FailuresService sẽ rollback sub-transaction trước khi code trở lại.
    internal class SuppressWarningsPreprocessor : IFailuresPreprocessor
    {
        public FailureProcessingResult PreprocessFailures(FailuresAccessor a)
        {
            foreach (var f in a.GetFailureMessages())
            {
                if (f.GetSeverity() == FailureSeverity.Warning)
                    a.DeleteWarning(f);
            }
            return FailureProcessingResult.Continue;
        }
    }
}
