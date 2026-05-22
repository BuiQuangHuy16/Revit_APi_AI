using System;
using Aplication.Commands.DuplicateLegend.Models;
using Aplication.Common.Mvvm;

namespace Aplication.Commands.DuplicateLegend.ViewModels
{
    public class DuplicateLegendViewModel : ObservableObject
    {
        public int SelectedCount { get; }
        public PlacementMode SelectedMode { get; private set; } = PlacementMode.PickPoint;

        public RelayCommand DuplicateCommand { get; }
        public RelayCommand ReplaceCommand { get; }

        public event Action<bool> RequestClose;

        public DuplicateLegendViewModel(int selectedCount)
        {
            SelectedCount = selectedCount;

            DuplicateCommand = new RelayCommand(() =>
            {
                SelectedMode = PlacementMode.PickPoint;
                RequestClose?.Invoke(true);
            });

            ReplaceCommand = new RelayCommand(() =>
            {
                SelectedMode = PlacementMode.Replace;
                RequestClose?.Invoke(true);
            });
        }

        public DuplicateLegendOptions GetOptions() => new DuplicateLegendOptions
        {
            Mode = SelectedMode,
            HorizontalSpacingMm = 10.0
        };
    }
}
