using System;
using System.Collections.Generic;
using System.Linq;
using Aplication.Commands.DuplicateSheet.Models;
using Autodesk.Revit.DB;

namespace Aplication.Commands.DuplicateSheet.Services
{
    public class DuplicationResult
    {
        public int CreatedCount;
        public List<string> Errors = new List<string>();
        public ElementId FirstCreatedSheetId;
    }

    public static class SheetDuplicator
    {
        public static DuplicationResult Run(
            Document doc,
            IEnumerable<SheetItem> selectedSheets,
            DuplicateOptions options)
        {
            var result = new DuplicationResult();
            var resolver = new SheetNameResolver(doc);
            var viewNames = LoadExistingViewNames(doc);

            using (var tx = new Transaction(doc, "Duplicate Sheets"))
            {
                tx.Start();

                foreach (var sheetItem in selectedSheets)
                {
                    var sourceSheet = doc.GetElement(sheetItem.SheetId) as ViewSheet;
                    if (sourceSheet == null) continue;

                    for (var copyIndex = 1; copyIndex <= options.CopiesPerSheet; copyIndex++)
                    {
                        using (var sub = new SubTransaction(doc))
                        {
                            sub.Start();
                            try
                            {
                                var newSheet = DuplicateOneSheet(doc, sourceSheet, options, resolver, viewNames, copyIndex);
                                sub.Commit();

                                result.CreatedCount++;
                                if (result.FirstCreatedSheetId == null || result.FirstCreatedSheetId == ElementId.InvalidElementId)
                                    result.FirstCreatedSheetId = newSheet.Id;
                            }
                            catch (Exception ex)
                            {
                                sub.RollBack();
                                result.Errors.Add($"Sheet '{sourceSheet.SheetNumber} - {sourceSheet.Name}' (copy {copyIndex}): {ex.Message}");
                            }
                        }
                    }
                }

                tx.Commit();
            }

            return result;
        }

        private static ViewSheet DuplicateOneSheet(
            Document doc,
            ViewSheet sourceSheet,
            DuplicateOptions options,
            SheetNameResolver resolver,
            HashSet<string> viewNames,
            int copyIndex)
        {
            var titleBlockTypeId = GetSourceTitleBlockTypeId(doc, sourceSheet);
            var newSheet = ViewSheet.Create(doc, titleBlockTypeId);

            newSheet.SheetNumber = resolver.ResolveUniqueNumber(sourceSheet.SheetNumber);
            newSheet.Name = $"{sourceSheet.Name} Copy {copyIndex}";

            CopyEditableParameters(sourceSheet, newSheet);

            var legendCache = new Dictionary<ElementId, ElementId>();
            var scheduleCache = new Dictionary<ElementId, ElementId>();

            CopyViewports(doc, sourceSheet, newSheet, options, viewNames, legendCache, copyIndex);
            CopyScheduleInstances(doc, sourceSheet, newSheet, options, viewNames, scheduleCache, copyIndex);
            CopySheetOnlyAnnotations(doc, sourceSheet, newSheet);

            return newSheet;
        }

        private static ElementId GetSourceTitleBlockTypeId(Document doc, ViewSheet sourceSheet)
        {
            var titleBlock = new FilteredElementCollector(doc, sourceSheet.Id)
                .OfCategory(BuiltInCategory.OST_TitleBlocks)
                .WhereElementIsNotElementType()
                .Cast<FamilyInstance>()
                .FirstOrDefault();

            return titleBlock?.Symbol.Id ?? ElementId.InvalidElementId;
        }

        private static void CopyEditableParameters(ViewSheet src, ViewSheet dst)
        {
            foreach (Parameter srcParam in src.Parameters)
            {
                if (srcParam == null || srcParam.IsReadOnly) continue;

                Parameter dstParam = null;
                if (srcParam.Definition is InternalDefinition def && def.BuiltInParameter != BuiltInParameter.INVALID)
                {
                    if (def.BuiltInParameter == BuiltInParameter.SHEET_NUMBER) continue;
                    if (def.BuiltInParameter == BuiltInParameter.SHEET_NAME) continue;
                    dstParam = dst.get_Parameter(def.BuiltInParameter);
                }

                if (dstParam == null)
                    dstParam = dst.LookupParameter(srcParam.Definition.Name);

                if (dstParam == null || dstParam.IsReadOnly) continue;

                try
                {
                    switch (srcParam.StorageType)
                    {
                        case StorageType.String:
                            dstParam.Set(srcParam.AsString() ?? string.Empty);
                            break;
                        case StorageType.Integer:
                            dstParam.Set(srcParam.AsInteger());
                            break;
                        case StorageType.Double:
                            dstParam.Set(srcParam.AsDouble());
                            break;
                        case StorageType.ElementId:
                            dstParam.Set(srcParam.AsElementId());
                            break;
                    }
                }
                catch
                {
                    // ignore params that refuse the value (locked, formula-driven, etc.)
                }
            }
        }

        private static void CopyViewports(
            Document doc,
            ViewSheet src,
            ViewSheet dst,
            DuplicateOptions options,
            HashSet<string> viewNames,
            Dictionary<ElementId, ElementId> legendCache,
            int copyIndex)
        {
            foreach (var vpId in src.GetAllViewports())
            {
                var vp = doc.GetElement(vpId) as Viewport;
                if (vp == null) continue;

                var srcView = doc.GetElement(vp.ViewId) as View;
                if (srcView == null) continue;

                var center = vp.GetBoxCenter();
                ElementId targetViewId;

                if (srcView.ViewType == ViewType.Legend)
                {
                    targetViewId = options.DuplicateLegends
                        ? GetOrDuplicateView(doc, srcView, viewNames, legendCache, copyIndex)
                        : srcView.Id;
                }
                else
                {
                    targetViewId = GetOrDuplicateView(doc, srcView, viewNames, new Dictionary<ElementId, ElementId>(), copyIndex);
                }

                if (targetViewId == null || targetViewId == ElementId.InvalidElementId) continue;

                Viewport newVp = null;
                try
                {
                    newVp = Viewport.Create(doc, dst.Id, targetViewId, center);
                }
                catch
                {
                    // skip viewport that Revit refuses (e.g., view already placed on another sheet)
                }

                if (newVp != null)
                {
                    try { newVp.ChangeTypeId(vp.GetTypeId()); } catch { }
                }
            }
        }

        private static void CopyScheduleInstances(
            Document doc,
            ViewSheet src,
            ViewSheet dst,
            DuplicateOptions options,
            HashSet<string> viewNames,
            Dictionary<ElementId, ElementId> scheduleCache,
            int copyIndex)
        {
            var instances = new FilteredElementCollector(doc, src.Id)
                .OfClass(typeof(ScheduleSheetInstance))
                .Cast<ScheduleSheetInstance>()
                .ToList();

            foreach (var si in instances)
            {
                var srcSchedule = doc.GetElement(si.ScheduleId) as ViewSchedule;
                if (srcSchedule == null) continue;

                ElementId targetScheduleId = options.DuplicateSchedules
                    ? GetOrDuplicateView(doc, srcSchedule, viewNames, scheduleCache, copyIndex)
                    : srcSchedule.Id;

                try
                {
                    ScheduleSheetInstance.Create(doc, dst.Id, targetScheduleId, si.Point);
                }
                catch (ArgumentException)
                {
                    // skip internal/revision schedules that Revit does not allow on sheets
                }
            }
        }

        private static void CopySheetOnlyAnnotations(Document doc, ViewSheet src, ViewSheet dst)
        {
            var idsToCopy = new FilteredElementCollector(doc, src.Id)
                .WhereElementIsNotElementType()
                .Where(e => e.OwnerViewId == src.Id)
                .Where(e => !(e is Viewport))
                .Where(e => !(e is ScheduleSheetInstance))
                .Where(e => !IsTitleBlock(e))
                .Select(e => e.Id)
                .ToList();

            if (idsToCopy.Count == 0) return;

            try
            {
                ElementTransformUtils.CopyElements(
                    src,
                    idsToCopy,
                    dst,
                    Transform.Identity,
                    new CopyPasteOptions());
            }
            catch
            {
                // best-effort: ignore items that cannot be copied across sheets
            }
        }

        private static readonly ElementId TitleBlockCategoryId = new ElementId(BuiltInCategory.OST_TitleBlocks);

        private static bool IsTitleBlock(Element e)
        {
            return e is FamilyInstance fi
                && fi.Category != null
                && fi.Category.Id == TitleBlockCategoryId;
        }

        private static ElementId GetOrDuplicateView(
            Document doc,
            View srcView,
            HashSet<string> viewNames,
            Dictionary<ElementId, ElementId> cache,
            int copyIndex)
        {
            if (cache.TryGetValue(srcView.Id, out var existing))
                return existing;

            ViewDuplicateOption option;
            if (srcView.CanViewBeDuplicated(ViewDuplicateOption.WithDetailing))
                option = ViewDuplicateOption.WithDetailing;
            else if (srcView.CanViewBeDuplicated(ViewDuplicateOption.Duplicate))
                option = ViewDuplicateOption.Duplicate;
            else
            {
                cache[srcView.Id] = srcView.Id;
                return srcView.Id;
            }

            ElementId newId;
            try
            {
                newId = srcView.Duplicate(option);
            }
            catch
            {
                cache[srcView.Id] = srcView.Id;
                return srcView.Id;
            }

            if (newId == null || newId == ElementId.InvalidElementId)
            {
                cache[srcView.Id] = srcView.Id;
                return srcView.Id;
            }

            if (doc.GetElement(newId) is View newView)
            {
                var uniqueName = ResolveUniqueName(srcView.Name, viewNames, copyIndex);
                try { newView.Name = uniqueName; }
                catch { /* keep auto name */ }
            }

            cache[srcView.Id] = newId;
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
