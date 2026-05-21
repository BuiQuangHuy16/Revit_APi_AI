using System.Linq;
using Aplication.Commands.AutoDimColumns.Models;
using Autodesk.Revit.DB;

namespace Aplication.Commands.AutoDimColumns.Services
{
    public static class OldDimensionCleaner
    {
        public static int CleanFromView(Document doc, View view)
        {
            var toDelete = new FilteredElementCollector(doc, view.Id)
                .OfClass(typeof(Dimension))
                .Cast<Dimension>()
                .Where(d => d.LookupParameter("Comments")?.AsString() == AutoDimOptions.DimMarker)
                .Select(d => d.Id)
                .ToList();

            if (toDelete.Count == 0) return 0;
            doc.Delete(toDelete);
            return toDelete.Count;
        }
    }
}
