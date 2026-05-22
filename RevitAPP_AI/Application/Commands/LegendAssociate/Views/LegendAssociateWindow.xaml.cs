using System;
using System.Globalization;
using System.Windows.Data;
using Aplication.Commands.LegendAssociate.ViewModels;

namespace Aplication.Commands.LegendAssociate.Views
{
    public partial class LegendAssociateWindow : System.Windows.Window
    {
        public LegendAssociateWindow(LegendAssociateViewModel vm)
        {
            InitializeComponent();
            DataContext = vm;
            vm.RequestClose += () =>
            {
                try { Close(); } catch { /* already closed */ }
            };
        }
    }

    public class NullToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value == null ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }

    public class NotNullToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value != null ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
