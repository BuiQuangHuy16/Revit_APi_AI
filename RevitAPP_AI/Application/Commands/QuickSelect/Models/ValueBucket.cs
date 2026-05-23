using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace Aplication.Commands.QuickSelect.Models
{
    public class ValueBucket
    {
        public string DisplayValue { get; set; }
        public string RawKey { get; set; }
        public HashSet<ElementId> ElementIds { get; } = new HashSet<ElementId>();
        public int Count => ElementIds.Count;
    }
}
