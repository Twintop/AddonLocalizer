using AddonLocalizer.PageModels;
using System.Diagnostics;
using Syncfusion.Maui.DataGrid;

namespace AddonLocalizer.Pages;

public partial class LocalizationGridPage : ContentPage
{
    private readonly LocalizationGridPageModel _viewModel;

    public LocalizationGridPage(LocalizationGridPageModel viewModel)
    {
        try
        {
            Debug.WriteLine("[LocalizationGridPage] Constructor started");
            InitializeComponent();
            Debug.WriteLine("[LocalizationGridPage] InitializeComponent completed");
            
            _viewModel = viewModel;
            BindingContext = viewModel;
            
            // Subscribe to grid events for better editing experience
            dataGrid.CurrentCellBeginEdit += DataGrid_CurrentCellBeginEdit;
            dataGrid.CurrentCellEndEdit += DataGrid_CurrentCellEndEdit;
            
            Debug.WriteLine("[LocalizationGridPage] BindingContext set");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[LocalizationGridPage] Error in constructor: {ex.Message}");
            Debug.WriteLine($"[LocalizationGridPage] Stack trace: {ex.StackTrace}");
            throw;
        }
    }

    private void DataGrid_CurrentCellBeginEdit(object? sender, DataGridCurrentCellBeginEditEventArgs e)
    {
        Debug.WriteLine($"[LocalizationGridPage] Cell edit started - Row: {e.RowColumnIndex.RowIndex}, Column: {e.RowColumnIndex.ColumnIndex}");
        
        // Try to apply styling to the editor after a short delay to ensure it's created
#if WINDOWS
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            await Task.Delay(50); // Small delay to let editor initialize
            try
            {
                ApplyEditorStyling();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[LocalizationGridPage] Error applying editor styling: {ex.Message}");
            }
        });
#endif
    }

#if WINDOWS
    private void ApplyEditorStyling()
    {
        // Find all TextBox controls in the visual tree
        var textBoxes = FindVisualChildren<Microsoft.UI.Xaml.Controls.TextBox>(dataGrid.Handler?.PlatformView as Microsoft.UI.Xaml.FrameworkElement);
        foreach (var textBox in textBoxes)
        {
            Debug.WriteLine("[LocalizationGridPage] Found TextBox, applying styling");
            textBox.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.White);
            textBox.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Black);
            textBox.BorderBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 0, 230, 118));
        }
    }

    private static IEnumerable<T> FindVisualChildren<T>(Microsoft.UI.Xaml.DependencyObject? depObj) where T : Microsoft.UI.Xaml.DependencyObject
    {
        if (depObj == null) yield break;

        for (int i = 0; i < Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChildrenCount(depObj); i++)
        {
            var child = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChild(depObj, i);
            
            if (child is T t)
            {
                yield return t;
            }

            foreach (var childOfChild in FindVisualChildren<T>(child))
            {
                yield return childOfChild;
            }
        }
    }
#endif

    private void DataGrid_CurrentCellEndEdit(object? sender, DataGridCurrentCellEndEditEventArgs e)
    {
        // Force the grid to commit the edit
        Debug.WriteLine($"[LocalizationGridPage] Cell edit completed - Row: {e.RowColumnIndex.RowIndex}, Column: {e.RowColumnIndex.ColumnIndex}");
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        Debug.WriteLine("[LocalizationGridPage] OnAppearing called");
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        Debug.WriteLine("[LocalizationGridPage] OnDisappearing called");
    }
}
