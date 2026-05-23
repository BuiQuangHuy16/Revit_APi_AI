using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Aplication.Commands.QuickSelect.Models;
using Aplication.Common.Mvvm;
using Autodesk.Revit.DB;

namespace Aplication.Commands.QuickSelect.ViewModels
{
    public class QuickSelectViewModel : ObservableObject
    {
        private bool _isOrMode = true;
        private bool _isAndMode;
        private readonly ICollection<ElementId> _originalIds;

        public QuickSelectViewModel(IReadOnlyList<ParameterValueGroup> groups, ICollection<ElementId> originalIds)
        {
            _originalIds = originalIds;
            ResultIds = originalIds;
            Parameters = new ObservableCollection<ParameterNodeViewModel>();

            foreach (var group in groups)
            {
                var node = new ParameterNodeViewModel(group.ParameterName);
                foreach (var bucket in group.Values)
                {
                    var display = $"{bucket.DisplayValue} ({bucket.Count})";
                    node.Values.Add(new ValueNodeViewModel(node, display, bucket.ElementIds.ToList()));
                }
                Parameters.Add(node);
            }

            OkCommand = new RelayCommand(OnOk);
            CancelCommand = new RelayCommand(OnCancel);
        }

        public ObservableCollection<ParameterNodeViewModel> Parameters { get; }

        public bool IsOrMode
        {
            get => _isOrMode;
            set
            {
                if (SetProperty(ref _isOrMode, value) && value)
                    IsAndMode = false;
            }
        }

        public bool IsAndMode
        {
            get => _isAndMode;
            set
            {
                if (SetProperty(ref _isAndMode, value) && value)
                    IsOrMode = false;
            }
        }

        public RelayCommand OkCommand { get; }
        public RelayCommand CancelCommand { get; }

        public ICollection<ElementId> ResultIds { get; private set; }

        public event Action<bool> RequestClose;

        private void OnOk()
        {
            var checkedByParam = Parameters
                .Select(p => new
                {
                    p.Name,
                    Ids = new HashSet<ElementId>(
                        p.Values.Where(v => v.IsChecked).SelectMany(v => v.ElementIds))
                })
                .Where(x => x.Ids.Count > 0)
                .ToList();

            if (checkedByParam.Count == 0)
            {
                ResultIds = _originalIds;
                RequestClose?.Invoke(true);
                return;
            }

            HashSet<ElementId> result;
            if (IsAndMode)
            {
                result = new HashSet<ElementId>(checkedByParam[0].Ids);
                for (int i = 1; i < checkedByParam.Count; i++)
                    result.IntersectWith(checkedByParam[i].Ids);
            }
            else
            {
                result = new HashSet<ElementId>();
                foreach (var entry in checkedByParam)
                    result.UnionWith(entry.Ids);
            }

            ResultIds = result;
            RequestClose?.Invoke(true);
        }

        private void OnCancel()
        {
            ResultIds = _originalIds;
            RequestClose?.Invoke(false);
        }
    }
}
