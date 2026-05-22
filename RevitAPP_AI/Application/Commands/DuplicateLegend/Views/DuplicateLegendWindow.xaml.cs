using System.Windows;
using Aplication.Commands.DuplicateLegend.Models;

namespace Aplication.Commands.DuplicateLegend.Views
{
    public partial class DuplicateLegendWindow : Window
    {
        public PlacementMode SelectedMode { get; private set; } = PlacementMode.PickPoint;

        public DuplicateLegendWindow(int selectedCount)
        {
            InitializeComponent();
            LabelCount.Inlines.Clear();
            LabelCount.Inlines.Add(new System.Windows.Documents.Run("Đã chọn: "));
            LabelCount.Inlines.Add(new System.Windows.Documents.Run(selectedCount.ToString()) { FontWeight = FontWeights.Bold });
            LabelCount.Inlines.Add(new System.Windows.Documents.Run(" legend"));
        }

        private void OnDuplicateClick(object sender, RoutedEventArgs e)
        {
            SelectedMode = PlacementMode.PickPoint;
            DialogResult = true;
        }

        private void OnReplaceClick(object sender, RoutedEventArgs e)
        {
            SelectedMode = PlacementMode.Replace;
            DialogResult = true;
        }
    }
}
