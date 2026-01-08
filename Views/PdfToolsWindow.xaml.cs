using ConvertPDF.ViewModels;
using System.Windows;
using System.Windows.Controls;
using RadioButton = System.Windows.Controls.RadioButton;
using ComboBox = System.Windows.Controls.ComboBox;

namespace ConvertPDF.Views
{
    public partial class PdfToolsWindow : Window
    {
        public PdfToolsWindow()
        {
            InitializeComponent();
        }

        private void Operation_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton rb && rb.Tag != null)
            {
                int operation = int.Parse(rb.Tag.ToString());
                
                if (DataContext is PdfToolsViewModel vm)
                {
                    vm.SelectedOperation = operation;
                }

                // Show/hide appropriate panels
                SplitPanel.Visibility = operation == 0 ? Visibility.Visible : Visibility.Collapsed;
                ExtractPanel.Visibility = operation == 1 ? Visibility.Visible : Visibility.Collapsed;
                RotatePanel.Visibility = operation == 2 ? Visibility.Visible : Visibility.Collapsed;
                DeletePanel.Visibility = operation == 3 ? Visibility.Visible : Visibility.Collapsed;
                MergePanel.Visibility = operation == 4 ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private void PageSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox cb && DataContext is PdfToolsViewModel vm)
            {
                vm.SelectedPage = cb.SelectedIndex + 1;
            }
        }

        private void Rotation_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox cb && DataContext is PdfToolsViewModel vm)
            {
                vm.RotationDegrees = cb.SelectedIndex switch
                {
                    0 => 90,
                    1 => 180,
                    2 => 270,
                    _ => 90
                };
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
