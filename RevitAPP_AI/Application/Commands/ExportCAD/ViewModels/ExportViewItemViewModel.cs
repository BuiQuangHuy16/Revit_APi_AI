using Aplication.Common.Mvvm;
using Autodesk.Revit.DB;

namespace Aplication.Commands.ExportCAD.ViewModels
{
    // VM cho mỗi dòng View/Sheet trong danh sách export.
    public class ExportViewItemViewModel : ObservableObject
    {
        private bool _isSelected;

        public ExportViewItemViewModel(ElementId id, string name, string groupLabel)
        {
            Id = id;
            Name = name;
            GroupLabel = groupLabel;
        }

        public ElementId Id { get; }
        public string Name { get; }
        public string GroupLabel { get; }

        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }
    }
}
