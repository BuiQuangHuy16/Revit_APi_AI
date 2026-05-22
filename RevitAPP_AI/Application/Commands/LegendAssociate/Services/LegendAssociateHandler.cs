using System;
using System.Collections.Generic;
using System.Linq;
using Aplication.Commands.DuplicateLegend.Models;
using Aplication.Commands.DuplicateLegend.Services;
using Aplication.Commands.LegendAssociate.Models;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace Aplication.Commands.LegendAssociate.Services
{
    /// Chạy mọi tác vụ cần Revit API context cho cửa sổ modeless LegendAssociate.
    public class LegendAssociateHandler : IExternalEventHandler
    {
        public ActionKind Kind { get; set; } = ActionKind.None;
        public ElementId TargetViewId { get; set; }
        public ElementId TargetSheetId { get; set; }
        public ElementId PreviewTargetId { get; set; }
        public int PreviewSizePx { get; set; } = 1024;

        private readonly PreviewImageProvider _previewProvider;

        public LegendAssociateHandler(PreviewImageProvider previewProvider)
        {
            _previewProvider = previewProvider;
        }

        public event Action ActionCompleted;
        public event Action<ElementId> PreviewReady;
        public event Action<string> Reload;

        public string GetName() => "LegendAssociate.Handler";

        public void Execute(UIApplication app)
        {
            var uidoc = app.ActiveUIDocument;
            if (uidoc == null) return;
            var doc = uidoc.Document;

            try
            {
                switch (Kind)
                {
                    case ActionKind.GoTo:
                        HandleGoTo(uidoc, doc);
                        break;
                    case ActionKind.Insert:
                        HandleInsert(uidoc, doc);
                        break;
                    case ActionKind.DuplicateInsert:
                        HandleDuplicateInsert(uidoc, doc);
                        break;
                    case ActionKind.Replace:
                        HandleReplace(uidoc, doc);
                        break;
                    case ActionKind.RenderPreview:
                        HandleRenderPreview(doc);
                        return; // không phát ActionCompleted cho preview
                }
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                // user nhấn ESC khi pick — bỏ qua êm
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Legend Associate", "Lỗi: " + ex.Message);
            }
            finally
            {
                if (Kind != ActionKind.RenderPreview)
                {
                    var needsReload = Kind == ActionKind.DuplicateInsert; // tạo view mới → index thay đổi
                    Kind = ActionKind.None;
                    TargetViewId = null;
                    TargetSheetId = null;

                    ActionCompleted?.Invoke();
                    if (needsReload) Reload?.Invoke("Đã tạo legend mới — refresh danh sách.");
                }
            }
        }

        private void HandleRenderPreview(Document doc)
        {
            var id = PreviewTargetId;
            if (id == null || id == ElementId.InvalidElementId) return;
            try
            {
                _previewProvider.Render(doc, id, PreviewSizePx);
            }
            catch { /* swallow — UI sẽ hiển thị "No preview" */ }
            finally
            {
                Kind = ActionKind.None;
                PreviewReady?.Invoke(id);
            }
        }

        private void HandleGoTo(UIDocument uidoc, Document doc)
        {
            ElementId targetId = TargetSheetId != null && TargetSheetId != ElementId.InvalidElementId
                ? TargetSheetId
                : TargetViewId;

            if (targetId == null || targetId == ElementId.InvalidElementId)
            {
                TaskDialog.Show("Legend Associate", "Chưa chọn view/sheet để chuyển đến.");
                return;
            }

            if (!(doc.GetElement(targetId) is View view))
            {
                TaskDialog.Show("Legend Associate", "Đối tượng được chọn không phải là View.");
                return;
            }

            uidoc.ActiveView = view;
        }

        private void HandleInsert(UIDocument uidoc, Document doc)
        {
            if (!(uidoc.ActiveView is ViewSheet activeSheet))
            {
                TaskDialog.Show("Legend Associate", "Hãy mở 1 sheet rồi bấm Insert View.");
                return;
            }
            if (TargetViewId == null || TargetViewId == ElementId.InvalidElementId)
            {
                TaskDialog.Show("Legend Associate", "Chưa chọn view/legend để insert.");
                return;
            }

            var view = doc.GetElement(TargetViewId) as View;
            if (view == null || view.IsTemplate)
            {
                TaskDialog.Show("Legend Associate", "View đích không hợp lệ.");
                return;
            }

            // Legend có thể đặt trên nhiều sheet; view thường chỉ được đặt 1 lần.
            if (view.ViewType != ViewType.Legend)
            {
                if (IsViewAlreadyPlacedOnAnySheet(doc, view.Id))
                {
                    TaskDialog.Show("Legend Associate",
                        "View thường chỉ được đặt 1 lần. View này đã có viewport trên sheet khác.");
                    return;
                }
            }

            var point = uidoc.Selection.PickPoint("Chọn điểm đặt view/legend trên sheet");

            using (var tx = new Transaction(doc, "Insert View"))
            {
                tx.Start();
                var vp = Viewport.Create(doc, activeSheet.Id, view.Id, point);
                if (vp == null)
                {
                    tx.RollBack();
                    TaskDialog.Show("Legend Associate", "Không tạo được viewport (Revit từ chối vị trí).");
                    return;
                }
                tx.Commit();
                uidoc.Selection.SetElementIds(new List<ElementId> { vp.Id });
            }
        }

        private void HandleDuplicateInsert(UIDocument uidoc, Document doc)
        {
            if (!(uidoc.ActiveView is ViewSheet activeSheet))
            {
                TaskDialog.Show("Legend Associate", "Hãy mở 1 sheet rồi bấm Duplicate and Insert View.");
                return;
            }
            if (TargetViewId == null || TargetViewId == ElementId.InvalidElementId)
            {
                TaskDialog.Show("Legend Associate", "Chưa chọn legend để duplicate.");
                return;
            }

            var view = doc.GetElement(TargetViewId) as View;
            if (view == null || view.ViewType != ViewType.Legend)
            {
                TaskDialog.Show("Legend Associate", "Duplicate and Insert chỉ áp dụng cho Legend.");
                return;
            }

            var point = uidoc.Selection.PickPoint("Chọn điểm đặt bản copy của legend");

            // Reuse pipeline có sẵn của DuplicateLegend (transaction + suppress warnings +
            // copy detailing + đặt viewport title-bar đúng box).
            var selection = new List<SelectedViewport>
            {
                new SelectedViewport
                {
                    ViewportId = null,
                    LegendId = view.Id,
                    LegendName = view.Name
                }
            };

            var options = new DuplicateLegendOptions
            {
                Mode = PlacementMode.PickPoint,
                HorizontalSpacingMm = 10.0,
                PickedPoint = point
            };

            var result = LegendDuplicator.Run(doc, activeSheet, selection, options);

            if (result.FirstCreatedViewportId != null && result.FirstCreatedViewportId != ElementId.InvalidElementId)
            {
                uidoc.Selection.SetElementIds(new List<ElementId> { result.FirstCreatedViewportId });
            }

            if (result.Errors.Count > 0)
            {
                TaskDialog.Show("Legend Associate",
                    "Có lỗi khi duplicate:\n - " + string.Join("\n - ", result.Errors));
            }
        }

        private void HandleReplace(UIDocument uidoc, Document doc)
        {
            if (!(uidoc.ActiveView is ViewSheet activeSheet))
            {
                TaskDialog.Show("Legend Associate", "Hãy mở 1 sheet rồi bấm Replace in Sheet.");
                return;
            }
            if (TargetViewId == null || TargetViewId == ElementId.InvalidElementId)
            {
                TaskDialog.Show("Legend Associate", "Chưa chọn view/legend đích để thay thế.");
                return;
            }

            var newView = doc.GetElement(TargetViewId) as View;
            if (newView == null || newView.IsTemplate)
            {
                TaskDialog.Show("Legend Associate", "View đích không hợp lệ.");
                return;
            }

            // Pick viewport cần thay thế trên sheet hiện tại
            var reference = uidoc.Selection.PickObject(
                ObjectType.Element,
                new ViewportOnActiveSheetFilter(doc, activeSheet.Id),
                "Chọn viewport cần thay thế trên sheet hiện tại");

            if (reference == null) return;

            if (!(doc.GetElement(reference.ElementId) is Viewport oldVp))
            {
                TaskDialog.Show("Legend Associate", "Đối tượng được chọn không phải viewport.");
                return;
            }

            // Kiểm tra view thường: nếu newView không phải legend và đang được đặt nơi khác → từ chối.
            if (newView.ViewType != ViewType.Legend && IsViewAlreadyPlacedOnAnySheet(doc, newView.Id))
            {
                TaskDialog.Show("Legend Associate",
                    "View thường chỉ được đặt 1 lần. View này đã có viewport trên sheet khác.");
                return;
            }

            XYZ center;
            ElementId oldTypeId;
            try { center = oldVp.GetBoxCenter(); }
            catch { center = null; }
            try { oldTypeId = oldVp.GetTypeId(); }
            catch { oldTypeId = null; }

            using (var tx = new Transaction(doc, "Replace View in Sheet"))
            {
                tx.Start();
                try
                {
                    doc.Delete(oldVp.Id);
                    var newVp = Viewport.Create(doc, activeSheet.Id, newView.Id, center ?? XYZ.Zero);
                    if (newVp == null)
                    {
                        tx.RollBack();
                        TaskDialog.Show("Legend Associate", "Không tạo được viewport mới tại vị trí cũ.");
                        return;
                    }

                    if (oldTypeId != null && oldTypeId != ElementId.InvalidElementId)
                    {
                        try { newVp.ChangeTypeId(oldTypeId); } catch { }
                    }

                    doc.Regenerate();
                    if (center != null)
                    {
                        try { newVp.SetBoxCenter(center); } catch { }
                    }

                    tx.Commit();
                    uidoc.Selection.SetElementIds(new List<ElementId> { newVp.Id });
                }
                catch
                {
                    if (tx.GetStatus() == TransactionStatus.Started) tx.RollBack();
                    throw;
                }
            }
        }

        private static bool IsViewAlreadyPlacedOnAnySheet(Document doc, ElementId viewId)
        {
            foreach (var sheet in new FilteredElementCollector(doc).OfClass(typeof(ViewSheet)).Cast<ViewSheet>())
            {
                foreach (var vpId in sheet.GetAllViewports())
                {
                    if (doc.GetElement(vpId) is Viewport vp && vp.ViewId == viewId) return true;
                }
            }
            return false;
        }
    }

    public class ViewportOnActiveSheetFilter : ISelectionFilter
    {
        private readonly Document _doc;
        private readonly ElementId _sheetId;

        public ViewportOnActiveSheetFilter(Document doc, ElementId sheetId)
        {
            _doc = doc;
            _sheetId = sheetId;
        }

        public bool AllowElement(Element elem)
        {
            return elem is Viewport vp && vp.SheetId == _sheetId;
        }

        public bool AllowReference(Reference reference, XYZ position) => true;
    }
}
