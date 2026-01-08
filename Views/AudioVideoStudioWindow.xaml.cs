using System.Windows;
using ConvertPDF.ViewModels;

namespace ConvertPDF.Views
{
    public partial class AudioVideoStudioWindow : Window
    {
        public AudioVideoStudioWindow(AudioVideoStudioViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}
