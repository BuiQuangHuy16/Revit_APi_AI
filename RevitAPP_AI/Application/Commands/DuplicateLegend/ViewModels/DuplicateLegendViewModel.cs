using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Data;
using Aplication.Commands.DuplicateLegend.Models;
using Aplication.Common.Mvvm;

namespace Aplication.Commands.DuplicateLegend.ViewModels
{
    public class DuplicateLegendViewModel : ObservableObject
    {
        public ObservableCollection<LegendItem> AllLegends { get; }
        public ICollectionView FilteredLegends { get; }

        public event Action<bool> RequestClose;

        public bool ActiveViewIsSheet { get; }

        public DuplicateLegendViewModel(IEnumerable<LegendItem> legends, bool activeViewIsSheet)
        {
            ActiveViewIsSheet = activeViewIsSheet;

            AllLegends = new ObservableCollection<LegendItem>(legends);
            FilteredLegends = CollectionViewSource.GetDefaultView(AllLegends);
            FilteredLegends.Filter = FilterLegend;

            foreach (var l in AllLegends)
                l.PropertyChanged += OnLegendPropertyChanged;

            // Default mode: PickPoint only if active view is a sheet, otherwise force PickPoint anyway —
            // the command layer will block execution if !activeViewIsSheet when user tries to proceed.
            _isPickPointMode = true;
            _isReplaceMode = false;

            SelectAllCommand = new RelayCommand(SelectAllInView);
            SelectNoneCommand = new RelayCommand(SelectNoneInView);
            OkCommand = new RelayCommand(() => RequestClose?.Invoke(true), CanExecuteOk);
            CancelCommand = new RelayCommand(() => RequestClose?.Invoke(false));
        }

        private string _searchText = string.Empty;
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value ?? string.Empty))
                    FilteredLegends.Refresh();
            }
        }

        private int _copiesPerLegend = 1;
        public int CopiesPerLegend
        {
            get => _copiesPerLegend;
            set
            {
                var clamped = Math.Max(1, Math.Min(50, value));
                if (SetProperty(ref _copiesPerLegend, clamped))
                    OkCommand.RaiseCanExecuteChanged();
            }
        }

        private bool _isPickPointMode;
        public bool IsPickPointMode
        {
            get => _isPickPointMode;
            set
            {
                if (SetProperty(ref _isPickPointMode, value) && value)
                {
                    IsReplaceMode = false;
                    OkCommand.RaiseCanExecuteChanged();
                }
            }
        }

        private bool _isReplaceMode;
        public bool IsReplaceMode
        {
            get => _isReplaceMode;
            set
            {
                if (SetProperty(ref _isReplaceMode, value) && value)
                {
                    IsPickPointMode = false;
                    // Replace mode forces 1 copy
                    CopiesPerLegend = 1;
                    OkCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public int SelectedCount => AllLegends.Count(l => l.IsSelected);

        public RelayCommand SelectAllCommand { get; }
        public RelayCommand SelectNoneCommand { get; }
        public RelayCommand OkCommand { get; }
        public RelayCommand CancelCommand { get; }

        public IEnumerable<LegendItem> GetSelected() => AllLegends.Where(l => l.IsSelected);

        public DuplicateLegendOptions GetOptions() => new DuplicateLegendOptions
        {
            CopiesPerLegend = IsReplaceMode ? 1 : CopiesPerLegend,
            Mode = IsReplaceMode ? PlacementMode.Replace : PlacementMode.PickPoint,
            HorizontalSpacingMm = 10.0
        };

        private bool FilterLegend(object obj)
        {
            if (string.IsNullOrWhiteSpace(_searchText)) return true;
            if (!(obj is LegendItem l)) return false;
            var q = _searchText.Trim();
            return l.LegendName?.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void SelectAllInView()
        {
            foreach (var item in FilteredLegends.Cast<LegendItem>())
                item.IsSelected = true;
        }

        private void SelectNoneInView()
        {
            foreach (var item in FilteredLegends.Cast<LegendItem>())
                item.IsSelected = false;
        }

        private bool CanExecuteOk()
        {
            if (SelectedCount <= 0) return false;
            if (CopiesPerLegend < 1) return false;
            if (IsReplaceMode && !ActiveViewIsSheet) return false;
            return true;
        }

        private void OnLegendPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(LegendItem.IsSelected))
            {
                OnPropertyChanged(nameof(SelectedCount));
                OkCommand.RaiseCanExecuteChanged();
            }
        }
    }
}
