using Aplication.Common.Mvvm;
using Autodesk.Revit.DB;

namespace Aplication.Commands.ExportSchedule.ViewModels
{
    // VM cho mỗi dòng trong danh sách schedule — hiển thị tên + trạng thái tick.
    // Cha (ExportScheduleViewModel) subscribe PropertyChanged để cập nhật counter
    // và text của nút Export khi IsSelected thay đổi.
    public class ScheduleItemViewModel : ObservableObject
    {
        private bool _isSelected;

        public ScheduleItemViewModel(ElementId id, string name)
        {
            Id = id;
            Name = name;
        }

        public ElementId Id { get; }
        public string Name { get; }

        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }
    }
}
