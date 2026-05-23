using System.Collections.Generic;
using Aplication.Common.Mvvm;
using Autodesk.Revit.DB;

namespace Aplication.Commands.QuickSelect.ViewModels
{
    public class ValueNodeViewModel : ObservableObject
    {
        private bool _isChecked;
        internal bool SuppressParentNotify;

        public ValueNodeViewModel(ParameterNodeViewModel parent, string display, IReadOnlyList<ElementId> ids)
        {
            Parent = parent;
            Display = display;
            ElementIds = ids;
        }

        public ParameterNodeViewModel Parent { get; }
        public string Display { get; }
        public IReadOnlyList<ElementId> ElementIds { get; }

        public bool IsChecked
        {
            get => _isChecked;
            set
            {
                if (SetProperty(ref _isChecked, value) && !SuppressParentNotify)
                    Parent?.OnChildChanged();
            }
        }
    }
}
