using System.Windows;
using Aplication.Commands.AutoDimColumns.ViewModels;

namespace Aplication.Commands.AutoDimColumns.Views
{
    public partial class AutoDimColumnsWindow : Window
    {
        private readonly AutoDimColumnsViewModel _viewModel;

        public AutoDimColumnsWindow(AutoDimColumnsViewModel viewModel)
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
