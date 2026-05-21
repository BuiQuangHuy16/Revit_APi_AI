using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Aplication.Commands.AutoDimColumns.Models;
using Aplication.Common.Mvvm;

namespace Aplication.Commands.AutoDimColumns.ViewModels
{
    public class AutoDimColumnsViewModel : ObservableObject
    {
        public ObservableCollection<DimensionTypeItem> DimensionTypes { get; }

        public event Action<bool> RequestClose;

        public AutoDimColumnsViewModel(IEnumerable<DimensionTypeItem> dimTypes, AutoDimOptions loaded)
        {
            DimensionTypes = new ObservableCollection<DimensionTypeItem>(dimTypes);

            _offset = loaded.OffsetMm;
            _textClearance = loaded.TextClearanceMm;
            _includeOverallGridChain = loaded.IncludeOverallGridChain;
            _skipColumnsWithoutNearbyGrid = loaded.SkipColumnsWithoutNearbyGrid;
            _maxGridSearchRadius = loaded.MaxGridSearchRadiusMm;

            _selectedDimensionType =
                DimensionTypes.FirstOrDefault(d => d.Name == loaded.SelectedDimTypeName)
                ?? DimensionTypes.FirstOrDefault();

            OkCommand = new RelayCommand(() => RequestClose?.Invoke(true), CanOk);
            CancelCommand = new RelayCommand(() => RequestClose?.Invoke(false));
        }

        private DimensionTypeItem _selectedDimensionType;
        public DimensionTypeItem SelectedDimensionType
        {
            get => _selectedDimensionType;
            set
            {
                if (SetProperty(ref _selectedDimensionType, value))
                    OkCommand.RaiseCanExecuteChanged();
            }
        }

        private double _offset;
        public double Offset
        {
            get => _offset;
            set
            {
                if (SetProperty(ref _offset, value))
                    OkCommand.RaiseCanExecuteChanged();
            }
        }

        private double _textClearance;
        public double TextClearance
        {
            get => _textClearance;
            set
            {
                if (SetProperty(ref _textClearance, value))
                    OkCommand.RaiseCanExecuteChanged();
            }
        }

        private bool _includeOverallGridChain;
        public bool IncludeOverallGridChain
        {
            get => _includeOverallGridChain;
            set => SetProperty(ref _includeOverallGridChain, value);
        }

        private bool _skipColumnsWithoutNearbyGrid;
        public bool SkipColumnsWithoutNearbyGrid
        {
            get => _skipColumnsWithoutNearbyGrid;
            set => SetProperty(ref _skipColumnsWithoutNearbyGrid, value);
        }

        private double _maxGridSearchRadius;
        public double MaxGridSearchRadius
        {
            get => _maxGridSearchRadius;
            set => SetProperty(ref _maxGridSearchRadius, value);
        }

        public RelayCommand OkCommand { get; }
        public RelayCommand CancelCommand { get; }

        public AutoDimOptions GetOptions() => new AutoDimOptions
        {
            SelectedDimTypeName = SelectedDimensionType?.Name,
            OffsetMm = Offset,
            TextClearanceMm = TextClearance,
            IncludeOverallGridChain = IncludeOverallGridChain,
            SkipColumnsWithoutNearbyGrid = SkipColumnsWithoutNearbyGrid,
            MaxGridSearchRadiusMm = MaxGridSearchRadius
        };

        private bool CanOk() =>
            SelectedDimensionType != null &&
            Offset > 0 &&
            TextClearance >= 0;
    }
}
