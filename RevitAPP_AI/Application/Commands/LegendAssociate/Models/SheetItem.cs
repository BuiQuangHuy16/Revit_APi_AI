using Autodesk.Revit.DB;

namespace Aplication.Commands.LegendAssociate.Models
{
    public class SheetItem
    {
        public ElementId Id { get; set; }
        public string SheetNumber { get; set; }
        public string SheetName { get; set; }

        /// Tên Assembly nếu sheet thuộc về 1 Assembly (precast); null nếu sheet thường.
        public string AssemblyName { get; set; }

        public bool IsAssemblySheet => !string.IsNullOrEmpty(AssemblyName);

        public string DisplayName
        {
            get
            {
                var head = $"{SheetNumber} - {SheetName}";
                return IsAssemblySheet ? $"{head}   [Assembly: {AssemblyName}]" : head;
            }
        }

        public override string ToString() => DisplayName;
    }
}
