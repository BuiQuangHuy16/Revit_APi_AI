using System.Collections.Generic;
using System.Linq;
using System.Windows.Interop;
using Aplication.Commands.AutoDimColumns.Models;
using Aplication.Commands.AutoDimColumns.Services;
using Aplication.Commands.AutoDimColumns.ViewModels;
using Aplication.Commands.AutoDimColumns.Views;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Nice3point.Revit.Toolkit.External;

namespace Aplication.Commands.AutoDimColumns
{
    [UsedImplicitly]
    [Transaction(TransactionMode.Manual)]
    public class AutoDimColumnsCommand : ExternalCommand
    {
        public override void Execute()
        {
            var uidoc = Application.ActiveUIDocument;
            var doc = uidoc.Document;
            var view = uidoc.ActiveView;

            if (!(view is ViewPlan))
            {
                TaskDialog.Show("Auto Dim Columns", "Lệnh chỉ chạy trên Floor Plan / Structural Plan.");
                return;
            }

            var dimTypes = new FilteredElementCollector(doc)
                .OfClass(typeof(DimensionType))
                .Cast<DimensionType>()
                .Where(d => d.StyleType == DimensionStyleType.Linear)
                .OrderBy(d => d.Name)
                .Select(d => new DimensionTypeItem { Id = d.Id, Name = d.Name })
                .ToList();

            if (dimTypes.Count == 0)
            {
                TaskDialog.Show("Auto Dim Columns", "Project không có DimensionType dạng Linear nào.");
                return;
            }

            var saved = SettingsStore.Load();
            var vm = new AutoDimColumnsViewModel(dimTypes, saved);
            var window = new AutoDimColumnsWindow(vm);
            new WindowInteropHelper(window).Owner = uidoc.Application.MainWindowHandle;

            if (window.ShowDialog() != true) return;

            var opts = vm.GetOptions();
            SettingsStore.Save(opts);

            var dimType = doc.GetElement(vm.SelectedDimensionType.Id) as DimensionType;
            if (dimType == null)
            {
                TaskDialog.Show("Auto Dim Columns", "Không tìm thấy DimensionType đã chọn trong document.");
                return;
            }

            var columns = new FilteredElementCollector(doc, view.Id)
                .OfCategory(BuiltInCategory.OST_StructuralColumns)
                .WhereElementIsNotElementType()
                .Cast<FamilyInstance>()
                .ToList();

            if (columns.Count == 0)
            {
                TaskDialog.Show("Auto Dim Columns", "Không có cột kết cấu nào trong view này.");
                return;
            }

            var locator = new GridLocator(doc, view);
            var maxRadiusFt = UnitUtils.ConvertToInternalUnits(opts.MaxGridSearchRadiusMm, UnitTypeId.Millimeters);
            var result = new DimRunResult();

            using (var tx = new Transaction(doc, "Auto Dim Columns"))
            {
                tx.Start();

                result.OldDimensionsDeleted = OldDimensionCleaner.CleanFromView(doc, view);

                foreach (var col in columns)
                {
                    var task = new ColumnDimTask { Column = col };
                    if (!ColumnFaceExtractor.TryExtract(col, view, task))
                    {
                        result.ColumnsSkipped++;
                        if (!string.IsNullOrEmpty(task.SkipReason))
                            result.SkipReasons.Add(task.SkipReason);
                        continue;
                    }

                    var (gx, gy) = locator.FindNearest(task.Center, maxRadiusFt);
                    task.NearestGridX = gx;
                    task.NearestGridY = gy;

                    if (opts.SkipColumnsWithoutNearbyGrid && gx == null && gy == null)
                    {
                        result.ColumnsSkipped++;
                        result.SkipReasons.Add($"Cột '{col.Name}' không có grid trong bán kính {opts.MaxGridSearchRadiusMm} mm.");
                        continue;
                    }

                    try
                    {
                        result.DimensionsCreated += DimensionCreator.CreateForColumn(doc, view, task, opts, dimType);
                        result.ColumnsProcessed++;
                    }
                    catch (System.Exception ex)
                    {
                        result.Errors.Add($"Cột '{col.Name}': {ex.Message}");
                    }
                }

                if (opts.IncludeOverallGridChain)
                {
                    try
                    {
                        result.DimensionsCreated += DimensionCreator.CreateOverallGridChain(doc, view, locator, opts, dimType);
                    }
                    catch (System.Exception ex)
                    {
                        result.Errors.Add($"Overall grid chain: {ex.Message}");
                    }
                }

                tx.Commit();
            }

            ShowSummary(result);
        }

        private static void ShowSummary(DimRunResult r)
        {
            var lines = new List<string>
            {
                $"Đã xoá {r.OldDimensionsDeleted} dim cũ.",
                $"Đã tạo {r.DimensionsCreated} dim cho {r.ColumnsProcessed} cột.",
                $"Bỏ qua {r.ColumnsSkipped} cột."
            };

            if (r.SkipReasons.Count > 0)
            {
                lines.Add("");
                lines.Add("Lý do bỏ qua (10 đầu):");
                lines.AddRange(r.SkipReasons.Take(10).Select(s => " • " + s));
            }

            if (r.Errors.Count > 0)
            {
                lines.Add("");
                lines.Add($"Lỗi ({r.Errors.Count}):");
                lines.AddRange(r.Errors.Take(10).Select(s => " • " + s));
            }

            TaskDialog.Show("Auto Dim Columns", string.Join("\n", lines));
        }
    }
}
