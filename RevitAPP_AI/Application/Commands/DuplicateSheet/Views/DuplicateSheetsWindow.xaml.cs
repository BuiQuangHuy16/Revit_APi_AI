using System.Windows;
using Aplication.Commands.DuplicateSheet.ViewModels;

namespace Aplication.Commands.DuplicateSheet.Views
{
    public partial class DuplicateSheetsWindow : Window
    {
        private readonly DuplicateSheetsViewModel _viewModel;

        public DuplicateSheetsWindow(DuplicateSheetsViewModel viewModel)
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
