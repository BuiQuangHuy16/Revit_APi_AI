using System.Collections.ObjectModel;
using System.Linq;
using Aplication.Common.Mvvm;

namespace Aplication.Commands.QuickSelect.ViewModels
{
    public class ParameterNodeViewModel : ObservableObject
    {
        private bool? _isChecked = false;
        private bool _suppressChildPropagation;

        public ParameterNodeViewModel(string name)
        {
            Name = name;
        }

        public string Name { get; }
        public ObservableCollection<ValueNodeViewModel> Values { get; } = new ObservableCollection<ValueNodeViewModel>();

        public bool? IsChecked
        {
            get => _isChecked;
            set
            {
                if (_isChecked == value) return;
                _isChecked = value;
                OnPropertyChanged();
                if (!_suppressChildPropagation && value.HasValue)
                    PropagateToChildren(value.Value);
            }
        }

        private void PropagateToChildren(bool isChecked)
        {
            foreach (var child in Values)
            {
                child.SuppressParentNotify = true;
                child.IsChecked = isChecked;
                child.SuppressParentNotify = false;
            }
        }

        internal void OnChildChanged()
        {
            var checkedCount = Values.Count(v => v.IsChecked);
            bool? newState;
            if (checkedCount == 0) newState = false;
            else if (checkedCount == Values.Count) newState = true;
            else newState = null;

            if (_isChecked == newState) return;
            _suppressChildPropagation = true;
            _isChecked = newState;
            OnPropertyChanged(nameof(IsChecked));
            _suppressChildPropagation = false;
        }
    }
}
