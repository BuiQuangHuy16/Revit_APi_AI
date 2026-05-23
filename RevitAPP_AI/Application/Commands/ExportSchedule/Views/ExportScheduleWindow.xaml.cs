using System.Windows;
using Aplication.Commands.ExportSchedule.ViewModels;

namespace Aplication.Commands.ExportSchedule.Views
{
    public partial class ExportScheduleWindow : Window
    {
        private readonly ExportScheduleViewModel _viewModel;

        public ExportScheduleWindow(ExportScheduleViewModel viewModel)
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
