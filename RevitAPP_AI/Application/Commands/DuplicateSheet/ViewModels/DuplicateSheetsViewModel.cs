using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Data;
using Aplication.Commands.DuplicateSheet.Models;
using Aplication.Common.Mvvm;

namespace Aplication.Commands.DuplicateSheet.ViewModels
{
    public class DuplicateSheetsViewModel : ObservableObject
    {
        public ObservableCollection<SheetItem> AllSheets { get; }
        public ICollectionView FilteredSheets { get; }

        public event Action<bool> RequestClose;

        public DuplicateSheetsViewModel(IEnumerable<SheetItem> sheets)
        {
            AllSheets = new ObservableCollection<SheetItem>(sheets);
            FilteredSheets = CollectionViewSource.GetDefaultView(AllSheets);
            FilteredSheets.Filter = FilterSheet;

            foreach (var s in AllSheets)
                s.PropertyChanged += OnSheetPropertyChanged;

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
                    FilteredSheets.Refresh();
            }
        }

        private int _copiesPerSheet = 1;
        public int CopiesPerSheet
        {
            get => _copiesPerSheet;
            set
            {
                var clamped = Math.Max(1, Math.Min(50, value));
                if (SetProperty(ref _copiesPerSheet, clamped))
                    OkCommand.RaiseCanExecuteChanged();
            }
        }

        private bool _duplicateLegends;
        public bool DuplicateLegends
        {
            get => _duplicateLegends;
            set => SetProperty(ref _duplicateLegends, value);
        }

        private bool _duplicateSchedules;
        public bool DuplicateSchedules
        {
            get => _duplicateSchedules;
            set => SetProperty(ref _duplicateSchedules, value);
        }

        public int SelectedCount => AllSheets.Count(s => s.IsSelected);

        public RelayCommand SelectAllCommand { get; }
        public RelayCommand SelectNoneCommand { get; }
        public RelayCommand OkCommand { get; }
        public RelayCommand CancelCommand { get; }

        public IEnumerable<SheetItem> GetSelected() => AllSheets.Where(s => s.IsSelected);

        public DuplicateOptions GetOptions() => new DuplicateOptions
        {
            CopiesPerSheet = CopiesPerSheet,
            DuplicateLegends = DuplicateLegends,
            DuplicateSchedules = DuplicateSchedules
        };

        private bool FilterSheet(object obj)
        {
            if (string.IsNullOrWhiteSpace(_searchText)) return true;
            if (!(obj is SheetItem s)) return false;
            var q = _searchText.Trim();
            return (s.SheetNumber?.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0)
                || (s.SheetName?.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private void SelectAllInView()
        {
            foreach (var item in FilteredSheets.Cast<SheetItem>())
                item.IsSelected = true;
        }

        private void SelectNoneInView()
        {
            foreach (var item in FilteredSheets.Cast<SheetItem>())
                item.IsSelected = false;
        }

        private bool CanExecuteOk() => SelectedCount > 0 && CopiesPerSheet >= 1;

        private void OnSheetPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SheetItem.IsSelected))
            {
                OnPropertyChanged(nameof(SelectedCount));
                OkCommand.RaiseCanExecuteChanged();
            }
        }
    }
}
