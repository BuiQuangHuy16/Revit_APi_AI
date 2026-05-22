using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Data;
using System.Windows.Media.Imaging;
using Aplication.Commands.LegendAssociate.Models;
using Aplication.Commands.LegendAssociate.Services;
using Aplication.Common.Mvvm;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace Aplication.Commands.LegendAssociate.ViewModels
{
    public class LegendAssociateViewModel : ObservableObject
    {
        private const int PreviewSizePx = 1024;

        private readonly Document _doc;
        private readonly LegendAssociateHandler _handler;
        private readonly ExternalEvent _externalEvent;
        private readonly PreviewImageProvider _preview;

        private LegendIndex _index;
        private ElementId _pendingPreviewId;

        private ObservableCollection<object> _leftSource;
        private ICollectionView _leftView;

        public ICollectionView LeftItems
        {
            get => _leftView;
            private set { _leftView = value; OnPropertyChanged(); }
        }

        public ObservableCollection<object> RightItems { get; } = new ObservableCollection<object>();

        public event Action RequestClose;

        public LegendAssociateViewModel(
            Document doc,
            LegendIndex initialIndex,
            LegendAssociateHandler handler,
            ExternalEvent externalEvent,
            PreviewImageProvider previewProvider)
        {
            _doc = doc;
            _index = initialIndex;
            _handler = handler;
            _externalEvent = externalEvent;
            _preview = previewProvider;

            _handler.ActionCompleted += () => RaiseCommandsCanExecute();
            _handler.Reload += msg =>
            {
                _index = LegendAssociateIndexer.Build(_doc);
                _preview.Clear();
                RebuildLeftSource();
                RebuildRightFromSelection();
                StatusMessage = msg;
            };
            _handler.PreviewReady += renderedId =>
            {
                // Chỉ áp dụng nếu id render xong đúng là id đang chờ hiển thị.
                if (Equals(renderedId, _pendingPreviewId))
                {
                    PreviewImage = _preview.GetCached(renderedId);
                }
            };

            GoToCommand = new RelayCommand(() => Enqueue(ActionKind.GoTo), () => HasGoToTarget());
            InsertCommand = new RelayCommand(() => Enqueue(ActionKind.Insert), () => HasViewTarget());
            DuplicateInsertCommand = new RelayCommand(() => Enqueue(ActionKind.DuplicateInsert), () => HasLegendTarget());
            ReplaceCommand = new RelayCommand(() => Enqueue(ActionKind.Replace), () => HasViewTarget());
            CloseCommand = new RelayCommand(() => RequestClose?.Invoke());
            RefreshCommand = new RelayCommand(() =>
            {
                _index = LegendAssociateIndexer.Build(_doc);
                _preview.Clear();
                RebuildLeftSource();
                RebuildRightFromSelection();
                StatusMessage = "Đã refresh.";
            });

            RebuildLeftSource();
        }

        #region Bindable scalar state

        private string _searchText = string.Empty;
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value ?? string.Empty))
                    _leftView?.Refresh();
            }
        }

        private ViewTypeFilter _viewTypeFilter = ViewTypeFilter.Legend;
        public ViewTypeFilter ViewTypeFilter
        {
            get => _viewTypeFilter;
            set
            {
                if (SetProperty(ref _viewTypeFilter, value)) RebuildLeftSource();
            }
        }

        public bool ViewTypeIsView
        {
            get => ViewTypeFilter == ViewTypeFilter.View;
            set { if (value) ViewTypeFilter = ViewTypeFilter.View; OnPropertyChanged(); }
        }
        public bool ViewTypeIsLegend
        {
            get => ViewTypeFilter == ViewTypeFilter.Legend;
            set { if (value) ViewTypeFilter = ViewTypeFilter.Legend; OnPropertyChanged(); }
        }

        private SelectMode _selectMode = SelectMode.ByView;
        public SelectMode SelectMode
        {
            get => _selectMode;
            set
            {
                if (SetProperty(ref _selectMode, value)) RebuildLeftSource();
            }
        }

        public bool SelectByView
        {
            get => SelectMode == SelectMode.ByView;
            set { if (value) SelectMode = SelectMode.ByView; OnPropertyChanged(); }
        }
        public bool SelectBySheet
        {
            get => SelectMode == SelectMode.BySheet;
            set { if (value) SelectMode = SelectMode.BySheet; OnPropertyChanged(); }
        }

        private PreviewMode _previewMode = PreviewMode.LegendPreview;
        public PreviewMode PreviewMode
        {
            get => _previewMode;
            set
            {
                if (SetProperty(ref _previewMode, value)) UpdatePreview();
            }
        }

        public bool PreviewIsLegend
        {
            get => PreviewMode == PreviewMode.LegendPreview;
            set { if (value) PreviewMode = PreviewMode.LegendPreview; OnPropertyChanged(); }
        }
        public bool PreviewIsSheet
        {
            get => PreviewMode == PreviewMode.SheetPreview;
            set { if (value) PreviewMode = PreviewMode.SheetPreview; OnPropertyChanged(); }
        }

        private object _selectedLeftItem;
        public object SelectedLeftItem
        {
            get => _selectedLeftItem;
            set
            {
                if (SetProperty(ref _selectedLeftItem, value))
                {
                    RebuildRightFromSelection();
                    UpdatePreview();
                    RaiseCommandsCanExecute();
                }
            }
        }

        private object _selectedRightItem;
        public object SelectedRightItem
        {
            get => _selectedRightItem;
            set
            {
                if (SetProperty(ref _selectedRightItem, value))
                {
                    UpdatePreview();
                    RaiseCommandsCanExecute();
                }
            }
        }

        private BitmapSource _previewImage;
        public BitmapSource PreviewImage
        {
            get => _previewImage;
            private set => SetProperty(ref _previewImage, value);
        }

        private string _statusMessage;
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public string LeftHeader =>
            SelectMode == SelectMode.ByView
                ? (ViewTypeFilter == ViewTypeFilter.Legend ? "Legends" : "Views")
                : "View Sheets";

        public string RightHeader =>
            SelectMode == SelectMode.ByView ? "View Sheets" : "Views in Sheet";

        #endregion

        #region Commands

        public RelayCommand GoToCommand { get; }
        public RelayCommand InsertCommand { get; }
        public RelayCommand DuplicateInsertCommand { get; }
        public RelayCommand ReplaceCommand { get; }
        public RelayCommand CloseCommand { get; }
        public RelayCommand RefreshCommand { get; }

        private void RaiseCommandsCanExecute()
        {
            GoToCommand.RaiseCanExecuteChanged();
            InsertCommand.RaiseCanExecuteChanged();
            DuplicateInsertCommand.RaiseCanExecuteChanged();
            ReplaceCommand.RaiseCanExecuteChanged();
        }

        private bool HasGoToTarget()
        {
            return _selectedRightItem != null || _selectedLeftItem != null;
        }

        private bool HasViewTarget()
        {
            return GetTargetViewItem() != null;
        }

        private bool HasLegendTarget()
        {
            var v = GetTargetViewItem();
            return v != null && v.IsLegend;
        }

        #endregion

        #region Helpers

        private void RebuildLeftSource()
        {
            IEnumerable<object> source;
            if (SelectMode == SelectMode.ByView)
            {
                source = ViewTypeFilter == ViewTypeFilter.Legend
                    ? _index.Legends.Cast<object>()
                    : _index.Views.Cast<object>();
            }
            else
            {
                source = _index.Sheets.Cast<object>();
            }

            _leftSource = new ObservableCollection<object>(source);
            var cv = CollectionViewSource.GetDefaultView(_leftSource);
            cv.Filter = FilterLeft;
            LeftItems = cv;

            SelectedLeftItem = null;
            RebuildRightFromSelection();
            OnPropertyChanged(nameof(LeftHeader));
            OnPropertyChanged(nameof(RightHeader));
        }

        private bool FilterLeft(object obj)
        {
            if (string.IsNullOrWhiteSpace(_searchText)) return true;
            var q = _searchText.Trim();

            if (obj is ViewItem v)
                return Contains(v.Name, q) || Contains(v.ViewTypeLabel, q);

            if (obj is SheetItem s)
                return Contains(s.SheetNumber, q) || Contains(s.SheetName, q) || Contains(s.AssemblyName, q);

            return false;
        }

        private static bool Contains(string haystack, string needle) =>
            !string.IsNullOrEmpty(haystack) &&
            haystack.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;

        private void RebuildRightFromSelection()
        {
            RightItems.Clear();
            SelectedRightItem = null;

            if (_selectedLeftItem == null) return;

            if (SelectMode == SelectMode.ByView && _selectedLeftItem is ViewItem v)
            {
                if (_index.ViewToSheets.TryGetValue(v.Id, out var sheetIds))
                {
                    var sheetById = _index.Sheets.ToDictionary(s => s.Id, s => s);
                    foreach (var sid in sheetIds)
                        if (sheetById.TryGetValue(sid, out var sItem))
                            RightItems.Add(sItem);
                }
            }
            else if (SelectMode == SelectMode.BySheet && _selectedLeftItem is SheetItem s)
            {
                if (_index.SheetToViews.TryGetValue(s.Id, out var viewIds))
                {
                    var byId = _index.Legends.Concat(_index.Views).ToDictionary(x => x.Id, x => x);
                    foreach (var vid in viewIds)
                        if (byId.TryGetValue(vid, out var vItem))
                            RightItems.Add(vItem);
                }
            }
        }

        private void UpdatePreview()
        {
            ElementId previewId = null;

            if (PreviewMode == PreviewMode.LegendPreview)
            {
                var vi = GetTargetViewItem();
                if (vi != null) previewId = vi.Id;
            }
            else
            {
                var si = GetTargetSheetItem();
                if (si != null) previewId = si.Id;
            }

            _pendingPreviewId = previewId;

            if (previewId == null)
            {
                PreviewImage = null;
                return;
            }

            var cached = _preview.GetCached(previewId);
            if (cached != null)
            {
                PreviewImage = cached;
                return;
            }

            // Cache miss → set tạm rỗng (UI sẽ hiện "Loading…") rồi raise external event.
            PreviewImage = null;
            StatusMessage = "Đang render preview...";
            _handler.Kind = ActionKind.RenderPreview;
            _handler.PreviewTargetId = previewId;
            _handler.PreviewSizePx = PreviewSizePx;
            _externalEvent.Raise();
        }

        private ViewItem GetTargetViewItem()
        {
            if (SelectMode == SelectMode.ByView)
                return _selectedLeftItem as ViewItem;
            return _selectedRightItem as ViewItem;
        }

        private SheetItem GetTargetSheetItem()
        {
            if (SelectMode == SelectMode.ByView)
                return _selectedRightItem as SheetItem;
            return _selectedLeftItem as SheetItem;
        }

        private void Enqueue(ActionKind kind)
        {
            if (_handler == null || _externalEvent == null) return;

            _handler.Kind = kind;
            _handler.TargetViewId = GetTargetViewItem()?.Id;
            _handler.TargetSheetId = GetTargetSheetItem()?.Id;
            _externalEvent.Raise();
        }

        #endregion
    }
}
