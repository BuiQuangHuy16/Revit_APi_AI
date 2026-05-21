using System.Windows;
using Aplication.Commands.DuplicateLegend.ViewModels;

namespace Aplication.Commands.DuplicateLegend.Views
{
    public partial class DuplicateLegendWindow : Window
    {
        private readonly DuplicateLegendViewModel _viewModel;

        public DuplicateLegendWindow(DuplicateLegendViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            DataContext = viewModel;
            _viewModel.RequestClose += OnRequestClose;
        }

        private void OnRequestClose(bool result)
        {
            DialogResult = result;
            Close();
        }
    }
}
