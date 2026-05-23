using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Data;
using Aplication.Commands.ExportSchedule.Models;
using Aplication.Common.Mvvm;
using Autodesk.Revit.DB;

namespace Aplication.Commands.ExportSchedule.ViewModels
{
    // ViewModel chính của cửa sổ Export Schedule.
    // - Quản lý danh sách schedule + filter realtime theo SearchText.
    // - Theo dõi số item đang tick để cập nhật text nút "Export (N)".
    // - Xử lý logic Check All / Check None trên TẬP ĐANG HIỂN THỊ (sau filter).
    // - Khi nhấn Export: hiển thị SaveFileDialog hoặc FolderBrowserDialog tuỳ
    //   chế độ "Export to separate files (.xlsx)" rồi raise RequestClose(true).
    public class ExportScheduleViewModel : ObservableObject
    {
        private string _searchText = string.Empty;
        private int _selectedCount;
        private bool _exportToSeparateFiles;

        public ExportScheduleViewModel(IReadOnlyList<ScheduleItem> items)
        {
            AllItems = new ObservableCollection<ScheduleItemViewModel>();
            foreach (var item in items)
            {
                var vm = new ScheduleItemViewModel(item.Id, item.Name);
                vm.PropertyChanged += OnItemPropertyChanged;
                AllItems.Add(vm);
            }

            // CollectionView cho phép filter realtime mà không cần clone list.
            FilteredItems = CollectionViewSource.GetDefaultView(AllItems);
            FilteredItems.Filter = FilterPredicate;

            CheckAllCommand = new RelayCommand(OnCheckAll);
            CheckNoneCommand = new RelayCommand(OnCheckNone);
            ExportCommand = new RelayCommand(OnExport, () => SelectedCount > 0);
            CancelCommand = new RelayCommand(OnCancel);
        }

        // ============== Bindable properties ==============

        public ObservableCollection<ScheduleItemViewModel> AllItems { get; }

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

        public bool ExportToSeparateFiles
        {
            get => _exportToSeparateFiles;
            set => SetProperty(ref _exportToSeparateFiles, value);
        }

        // ============== Commands ==============

        public RelayCommand CheckAllCommand { get; }
        public RelayCommand CheckNoneCommand { get; }
        public RelayCommand ExportCommand { get; }
        public RelayCommand CancelCommand { get; }

        // ============== Output (đọc sau khi dialog close) ==============

        // Danh sách ElementId của các schedule đã chọn để xuất.
        public IReadOnlyList<ElementId> SelectedIds { get; private set; }

        // Đường dẫn người dùng đã chọn: file path (TH1) hoặc folder path (TH2).
        public string ResultPath { get; private set; }

        // Cờ phản ánh chế độ xuất tại thời điểm bấm Export.
        public bool ResultSeparateFiles { get; private set; }

        public event Action<bool> RequestClose;

        // ============== Private logic ==============

        private bool FilterPredicate(object obj)
        {
            if (string.IsNullOrWhiteSpace(_searchText)) return true;
            if (!(obj is ScheduleItemViewModel item)) return false;
            return item.Name.IndexOf(_searchText.Trim(), StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void OnItemPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ScheduleItemViewModel.IsSelected))
                SelectedCount = AllItems.Count(i => i.IsSelected);
        }

        // Check All chỉ áp dụng cho các item đang hiển thị sau filter — khớp UX
        // người dùng mong đợi (gõ search rồi tick all = chỉ tick kết quả tìm).
        private void OnCheckAll()
        {
            foreach (var item in FilteredItems.Cast<ScheduleItemViewModel>())
                item.IsSelected = true;
        }

        private void OnCheckNone()
        {
            foreach (var item in FilteredItems.Cast<ScheduleItemViewModel>())
                item.IsSelected = false;
        }

        private void OnExport()
        {
            var selected = AllItems.Where(i => i.IsSelected).ToList();
            if (selected.Count == 0) return;

            string chosenPath;
            if (ExportToSeparateFiles)
            {
                // TH2: Chọn thư mục lưu — mỗi schedule = 1 file .xlsx
                // Dùng Ookii VistaFolderBrowserDialog (WPF-native) để tránh kéo
                // System.Windows.Forms vào project và gây xung đột namespace.
                var dlg = new Ookii.Dialogs.Wpf.VistaFolderBrowserDialog
                {
                    Description = "Chọn thư mục để lưu các file Excel:",
                    UseDescriptionForTitle = true,
                    ShowNewFolderButton = true
                };
                if (dlg.ShowDialog() != true) return;
                chosenPath = dlg.SelectedPath;
            }
            else
            {
                // TH1: Chọn đường dẫn + tên file gộp
                var dlg = new Microsoft.Win32.SaveFileDialog
                {
                    Title = "Lưu file Excel tổng hợp",
                    Filter = "Excel Workbook (*.xlsx)|*.xlsx",
                    DefaultExt = ".xlsx",
                    FileName = "Schedules.xlsx",
                    AddExtension = true,
                    OverwritePrompt = true
                };
                if (dlg.ShowDialog() != true) return;
                chosenPath = dlg.FileName;
            }

            SelectedIds = selected.Select(i => i.Id).ToList();
            ResultPath = chosenPath;
            ResultSeparateFiles = ExportToSeparateFiles;
            RequestClose?.Invoke(true);
        }

        private void OnCancel()
        {
            RequestClose?.Invoke(false);
        }
    }
}
