using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ConvertPDF.ViewModels;
using ComboBox = System.Windows.Controls.ComboBox;
using ListBox = System.Windows.Controls.ListBox;
using DragDropEffects = System.Windows.DragDropEffects;
using DataFormats = System.Windows.DataFormats;
using DragEventArgs = System.Windows.DragEventArgs;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using Point = System.Windows.Point;

namespace ConvertPDF.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        // Add this missing event handler for the drop zone click
        private void DropZone_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // Trigger the browse command from the ViewModel
            if (DataContext is ViewModels.MainWindowViewModel vm)
            {
                vm.PickFilesCommand.Execute(null);
            }
        }

        private void Window_Drop(object sender, System.Windows.DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files != null && files.Length > 0)
                {
                    if (DataContext is MainWindowViewModel vm)
                    {
                        var task = vm.OnFilesDropped(files);
                    }
                }
            }
        }

        // Drag & Drop Reordering Logic
        private System.Windows.Point _startPoint;
        private object _draggedItem;

        private void ListBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _startPoint = e.GetPosition(null);
        }

        private void ListBoxItem_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            // Get the current mouse position
            System.Windows.Point mousePos = e.GetPosition(null);
            Vector diff = _startPoint - mousePos;

            if (e.LeftButton == MouseButtonState.Pressed &&
                (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                 Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance))
            {
                // Get the dragged ListBoxItem
                ListBox listBox = sender as ListBox;
                ListBoxItem listBoxItem = FindAncestor<ListBoxItem>((DependencyObject)e.OriginalSource);

                if (listBoxItem == null) return;

                // Initialize the drag & drop operation
                _draggedItem = listBoxItem.DataContext;
                DragDrop.DoDragDrop(listBoxItem, _draggedItem, DragDropEffects.Move);
            }
        }

        private void ListBox_Drop(object sender, System.Windows.DragEventArgs e) { } // Handled by Item Drop

        private void ListBoxItem_Drop(object sender, System.Windows.DragEventArgs e)
        {
            if (_draggedItem == null) return;
            
            var droppedData = e.Data.GetData(e.Data.GetFormats()[0]);
            var target = ((ListBoxItem)(sender)).DataContext;

            if (droppedData == target) return; // Dropped on self

            var vm = DataContext as MainWindowViewModel;
            if (vm != null)
            {
                int removedIdx = vm.Files.IndexOf((FileItemViewModel)droppedData);
                int targetIdx = vm.Files.IndexOf((FileItemViewModel)target);

                if (removedIdx < 0 || targetIdx < 0) return;

                vm.Files.Move(removedIdx, targetIdx);
            }
        }

        private static T FindAncestor<T>(DependencyObject current) where T : DependencyObject
        {
            do
            {
                if (current is T)
                {
                    return (T)current;
                }
                current = VisualTreeHelper.GetParent(current);
            }
            while (current != null);
            return null;
        }

        private void CompressionComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (DataContext is MainWindowViewModel vm && sender is System.Windows.Controls.ComboBox cb)
            {
                vm.SelectedCompression = cb.SelectedIndex switch
                {
                    0 => Services.CompressionLevel.None,
                    1 => Services.CompressionLevel.Balanced,
                    2 => Services.CompressionLevel.Maximum,
                    _ => Services.CompressionLevel.None
                };
            }
        }
        private void CloseSettings_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is MainWindowViewModel vm)
            {
                vm.IsSettingsOpen = false;
            }
        }
    }
}