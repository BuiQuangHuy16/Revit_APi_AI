using Aplication.Common.Mvvm;
using Autodesk.Revit.DB;

namespace Aplication.Commands.DuplicateLegend.Models
{
    public class LegendItem : ObservableObject
    {
        public ElementId LegendId { get; set; }
        public string LegendName { get; set; }
        public int SheetUsageCount { get; set; }

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }
    }
}
