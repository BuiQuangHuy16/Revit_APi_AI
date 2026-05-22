using System.Windows.Interop;
using Aplication.Commands.LegendAssociate.Services;
using Aplication.Commands.LegendAssociate.ViewModels;
using Aplication.Commands.LegendAssociate.Views;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI;
using Nice3point.Revit.Toolkit.External;

namespace Aplication.Commands.LegendAssociate
{
    [UsedImplicitly]
    [Transaction(TransactionMode.Manual)]
    public class LegendAssociateCommand : ExternalCommand
    {
        public override void Execute()
        {
            var uidoc = Application.ActiveUIDocument;
            if (uidoc == null)
            {
                TaskDialog.Show("Legend Associate", "Chưa mở document.");
                return;
            }

            var doc = uidoc.Document;

            // 1. Index trong API context (valid context bắt buộc cho FilteredElementCollector).
            var index = LegendAssociateIndexer.Build(doc);

            // 2. Tạo ExternalEvent handler (modeless action runner).
            // Dùng fully-qualified vì Nice3point.Revit.Toolkit.External cũng export tên ExternalEvent.
            var previewProvider = new PreviewImageProvider();
            var handler = new LegendAssociateHandler(previewProvider);
            var externalEvent = Autodesk.Revit.UI.ExternalEvent.Create(handler);

            // 3. VM + Window.
            var vm = new LegendAssociateViewModel(doc, index, handler, externalEvent, previewProvider);
            var window = new LegendAssociateWindow(vm);
            new WindowInteropHelper(window).Owner = uidoc.Application.MainWindowHandle;

            // 4. Modeless show.
            window.Show();
        }
    }
}
