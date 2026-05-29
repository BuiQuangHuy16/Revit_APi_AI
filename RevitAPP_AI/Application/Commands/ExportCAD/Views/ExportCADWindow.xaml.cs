using System.Windows;
using Aplication.Commands.ExportCAD.ViewModels;

namespace Aplication.Commands.ExportCAD.Views
{
    public partial class ExportCADWindow : Window
    {
        private readonly ExportCADViewModel _viewModel;

        public ExportCADWindow(ExportCADViewModel viewModel)
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
