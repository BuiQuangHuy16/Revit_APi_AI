using Aplication.Common.Mvvm;
using Autodesk.Revit.DB;

namespace Aplication.Commands.DuplicateSheet.Models
{
    public class SheetItem : ObservableObject
    {
        public ElementId SheetId { get; set; }
        public string SheetNumber { get; set; }
        public string SheetName { get; set; }

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }
    }
}
