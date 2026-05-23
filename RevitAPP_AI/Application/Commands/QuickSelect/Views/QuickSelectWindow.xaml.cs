using System.Windows;
using Aplication.Commands.QuickSelect.ViewModels;

namespace Aplication.Commands.QuickSelect.Views
{
    public partial class QuickSelectWindow : Window
    {
        private readonly QuickSelectViewModel _viewModel;

        public QuickSelectWindow(QuickSelectViewModel viewModel)
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
