using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Data;
using Aplication.Commands.ExportCAD.Models;
using Aplication.Common.Mvvm;
using Autodesk.Revit.DB;

namespace Aplication.Commands.ExportCAD.ViewModels
{
    // ViewModel cho cửa sổ Export CAD.
    // - Quản lý danh sách view + filter realtime theo SearchText.
    // - Nhóm view theo GroupLabel (Sheets, Floor Plans, ...) bằng CollectionView.
    // - Theo dõi số item đang tick để bật/tắt nút Export và cập nhật text "Export (N)".
    // - Khi nhấn Export: mở SaveFileDialog rồi raise RequestClose(true) để Command thực thi.
    public class ExportCADViewModel : ObservableObject
    {
        private string _searchText = string.Empty;
        private int _selectedCount;

        public ExportCADViewModel(IReadOnlyList<ExportViewItem> items)
        {
            AllItems = new ObservableCollection<ExportViewItemViewModel>();
            foreach (var item in items)
            {
                var vm = new ExportViewItemViewModel(item.Id, item.Name, item.GroupLabel);
                vm.PropertyChanged += OnItemPropertyChanged;
                AllItems.Add(vm);
            }

            FilteredItems = CollectionViewSource.GetDefaultView(AllItems);
            FilteredItems.Filter = FilterPredicate;
            // Group theo GroupLabel để UI hiển thị các header "Sheets", "Floor Plans"...
            FilteredItems.GroupDescriptions.Add(new PropertyGroupDescription(nameof(ExportViewItemViewModel.GroupLabel)));

            CheckAllCommand = new RelayCommand(OnCheckAll);
            CheckNoneCommand = new RelayCommand(OnCheckNone);
            ExportCommand = new RelayCommand(OnExport, () => SelectedCount > 0);
            CancelCommand = new RelayCommand(OnCancel);
        }

        // ============== Bindable properties ==============

        public ObservableCollection<ExportViewItemViewModel> AllItems { get; }
        public ICollectionView FilteredItems { get; }

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                    FilteredItems.Refresh();
            }
        }

        public int TotalCount => AllItems.Count;

        public int SelectedCount
        {
            get => _selectedCount;
            private set
            {
                if (SetProperty(ref _selectedCount, value))
                {
                    OnPropertyChanged(nameof(ExportButtonText));
                    ExportCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public string ExportButtonText => $"Export ({SelectedCount})";

        // ============== Commands ==============

        public RelayCommand CheckAllCommand { get; }
        public RelayCommand CheckNoneCommand { get; }
        public RelayCommand ExportCommand { get; }
        public RelayCommand CancelCommand { get; }

        // ============== Output (đọc sau khi dialog close) ==============

        public IReadOnlyList<ElementId> SelectedIds { get; private set; }
        public string ResultPath { get; private set; }

        public event Action<bool> RequestClose;

        // ============== Private logic ==============

        private bool FilterPredicate(object obj)
        {
            if (string.IsNullOrWhiteSpace(_searchText)) return true;
            if (!(obj is ExportViewItemViewModel item)) return false;
            return item.Name.IndexOf(_searchText.Trim(), StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void OnItemPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ExportViewItemViewModel.IsSelected))
                SelectedCount = AllItems.Count(i => i.IsSelected);
        }

        // Check All chỉ tick các item hiển thị sau filter — khớp UX người dùng mong đợi.
        private void OnCheckAll()
        {
            foreach (var item in FilteredItems.Cast<ExportViewItemViewModel>())
                item.IsSelected = true;
        }

        private void OnCheckNone()
        {
            foreach (var item in FilteredItems.Cast<ExportViewItemViewModel>())
                item.IsSelected = false;
        }

        private void OnExport()
        {
            var selected = AllItems.Where(i => i.IsSelected).ToList();
            if (selected.Count == 0) return;

            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Lưu file CAD tổng",
                Filter = "AutoCAD Drawing (*.dwg)|*.dwg",
                DefaultExt = ".dwg",
                FileName = "MergedExport.dwg",
                AddExtension = true,
                OverwritePrompt = true
            };
            if (dlg.ShowDialog() != true) return;

            SelectedIds = selected.Select(i => i.Id).ToList();
            ResultPath = dlg.FileName;
            RequestClose?.Invoke(true);
        }

        private void OnCancel()
        {
            RequestClose?.Invoke(false);
        }
    }
}
