using System.Diagnostics;
using Syncfusion.Maui.DataGrid;

namespace AddonLocalizer.Pages;

public partial class LocalizationGridPage : ContentPage
{
    private readonly LocalizationGridPageModel _viewModel;
    private string? _pendingCopyValue;
    private const int MaxClipboardRetries = 3;
    private const int ClipboardRetryDelayMs = 100;
    
    // Track the current cell for copy operations
    private int _currentRowIndex = -1;
    private int _currentColumnIndex = -1;
    
    // Track whether we're in edit mode
    private bool _isInEditMode;

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
            dataGrid.CellRightTapped += DataGrid_CellRightTapped;
            dataGrid.CellTapped += DataGrid_CellTapped;
            dataGrid.SelectionChanging += DataGrid_SelectionChanging;
            
#if WINDOWS
            // Handle keyboard events for copy functionality with proper error handling
            dataGrid.Loaded += DataGrid_Loaded;
#endif
            
            Debug.WriteLine("[LocalizationGridPage] BindingContext set");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[LocalizationGridPage] Error in constructor: {ex.Message}");
            Debug.WriteLine($"[LocalizationGridPage] Stack trace: {ex.StackTrace}");
            throw;
        }
    }

    private void DataGrid_SelectionChanging(object? sender, DataGridSelectionChangingEventArgs e)
    {
        // Track selection changes for keyboard navigation
        // When NavigationMode is Cell, this fires when the current cell changes
        Debug.WriteLine($"[LocalizationGridPage] Selection changing");
    }

    private void DataGrid_CellTapped(object? sender, DataGridCellTappedEventArgs e)
    {
        _currentRowIndex = e.RowColumnIndex.RowIndex;
        _currentColumnIndex = e.RowColumnIndex.ColumnIndex;
        Debug.WriteLine($"[LocalizationGridPage] Cell tapped - Row: {_currentRowIndex}, Column: {_currentColumnIndex}");
    }

#if WINDOWS
    private void DataGrid_Loaded(object? sender, EventArgs e)
    {
        if (dataGrid.Handler?.PlatformView is Microsoft.UI.Xaml.FrameworkElement platformView)
        {
            // Use PreviewKeyDown to intercept before child controls handle it
            platformView.PreviewKeyDown += PlatformView_PreviewKeyDown;
        }
    }

    private async void PlatformView_PreviewKeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        // Check for Ctrl+C
        var ctrlPressed = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Control)
            .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
        
        if (ctrlPressed && e.Key == Windows.System.VirtualKey.C)
        {
            // If in edit mode, let the TextBox handle Ctrl+C natively for selected text copy
            if (_isInEditMode)
            {
                Debug.WriteLine("[LocalizationGridPage] In edit mode, allowing native TextBox copy");
                // Don't set e.Handled - let the TextBox process the copy
                return;
            }
            
            // Not in edit mode - handle cell copy ourselves with retry logic
            Debug.WriteLine($"[LocalizationGridPage] Ctrl+C pressed, attempting to copy cell at Row: {_currentRowIndex}, Column: {_currentColumnIndex}");
            e.Handled = true; // Prevent default handling that causes the crash
            await HandleCopyAsync();
            return;
        }
        
        // Handle Enter key to start editing the current cell
        if (e.Key == Windows.System.VirtualKey.Enter && !_isInEditMode)
        {
            if (_currentRowIndex >= 1 && _currentColumnIndex >= 0)
            {
                // Check if the column is editable
                var column = dataGrid.Columns[_currentColumnIndex];
                if (column.AllowEditing)
                {
                    Debug.WriteLine($"[LocalizationGridPage] Enter pressed - Starting edit at Row: {_currentRowIndex}, Column: {_currentColumnIndex}");
                    e.Handled = true; // Prevent default behavior (moving to next row)
                    
                    // Scroll to and select the cell first to ensure grid state is synced
                    dataGrid.ScrollToRowIndex(_currentRowIndex);
                    dataGrid.SelectedIndex = _currentRowIndex - 1; // SelectedIndex is 0-based data index
                    
                    // Begin editing the current cell
                    dataGrid.BeginEdit(_currentRowIndex, _currentColumnIndex);
                    return;
                }
                else
                {
                    Debug.WriteLine($"[LocalizationGridPage] Enter pressed - Column {_currentColumnIndex} is not editable");
                }
            }
            return;
        }
        
        // Track arrow key navigation to update current cell position
        if (!_isInEditMode)
        {
            switch (e.Key)
            {
                case Windows.System.VirtualKey.Up:
                    if (_currentRowIndex > 1) // Don't go into header (row 0)
                        _currentRowIndex--;
                    Debug.WriteLine($"[LocalizationGridPage] Arrow Up - New Row: {_currentRowIndex}");
                    break;
                case Windows.System.VirtualKey.Down:
                    if (_currentRowIndex < _viewModel.FilteredEntries.Count) // Stay within data bounds
                        _currentRowIndex++;
                    Debug.WriteLine($"[LocalizationGridPage] Arrow Down - New Row: {_currentRowIndex}");
                    break;
                case Windows.System.VirtualKey.Left:
                    if (_currentColumnIndex > 0)
                        _currentColumnIndex--;
                    Debug.WriteLine($"[LocalizationGridPage] Arrow Left - New Column: {_currentColumnIndex}");
                    break;
                case Windows.System.VirtualKey.Right:
                    if (_currentColumnIndex < dataGrid.Columns.Count - 1)
                        _currentColumnIndex++;
                    Debug.WriteLine($"[LocalizationGridPage] Arrow Right - New Column: {_currentColumnIndex}");
                    break;
            }
        }
    }

    private async Task HandleCopyAsync()
    {
        try
        {
            // Use the tracked current cell
            if (_currentRowIndex < 1 || _currentColumnIndex < 0) // Row 0 is header
            {
                Debug.WriteLine("[LocalizationGridPage] No valid cell selection for copy");
                return;
            }

            var cellValue = GetCellValue(_currentRowIndex, _currentColumnIndex);
            if (!string.IsNullOrEmpty(cellValue))
            {
                await CopyToClipboardWithRetry(cellValue);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[LocalizationGridPage] Error in HandleCopyAsync: {ex.Message}");
        }
    }

    private async Task CopyToClipboardWithRetry(string text)
    {
        for (int attempt = 0; attempt < MaxClipboardRetries; attempt++)
        {
            try
            {
                await Clipboard.Default.SetTextAsync(text);
                Debug.WriteLine($"[LocalizationGridPage] Copied to clipboard: {text}");
                return;
            }
            catch (Exception ex) when (attempt < MaxClipboardRetries - 1)
            {
                Debug.WriteLine($"[LocalizationGridPage] Clipboard attempt {attempt + 1} failed: {ex.Message}. Retrying...");
                await Task.Delay(ClipboardRetryDelayMs);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[LocalizationGridPage] All clipboard attempts failed: {ex.Message}");
                // Don't show alert for every failed copy - just log it
            }
        }
    }
#endif

    private async void DataGrid_CellRightTapped(object? sender, DataGridCellRightTappedEventArgs e)
    {
        Debug.WriteLine($"[LocalizationGridPage] Cell right-tapped - Row: {e.RowColumnIndex.RowIndex}, Column: {e.RowColumnIndex.ColumnIndex}");
        
        // Get the cell value
        var cellValue = GetCellValue(e.RowColumnIndex.RowIndex, e.RowColumnIndex.ColumnIndex);
        if (string.IsNullOrEmpty(cellValue))
        {
            Debug.WriteLine("[LocalizationGridPage] Cell value is empty, skipping context menu");
            return;
        }

        _pendingCopyValue = cellValue;

#if WINDOWS
        ShowWindowsContextMenu();
#else
        // Fallback for other platforms
        var action = await DisplayActionSheet("Cell Options", "Cancel", null, "Copy");
        if (action == "Copy")
        {
            await CopyToClipboard(cellValue);
        }
#endif
    }

#if WINDOWS
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool ScreenToClient(IntPtr hWnd, ref POINT lpPoint);

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    private void ShowWindowsContextMenu()
    {
        if (dataGrid.Handler?.PlatformView is not Microsoft.UI.Xaml.FrameworkElement platformView)
            return;

        var menu = new Microsoft.UI.Xaml.Controls.MenuFlyout();
        
        var copyItem = new Microsoft.UI.Xaml.Controls.MenuFlyoutItem
        {
            Text = "Copy",
            Icon = new Microsoft.UI.Xaml.Controls.SymbolIcon(Microsoft.UI.Xaml.Controls.Symbol.Copy)
        };
        copyItem.Click += async (s, args) =>
        {
            if (_pendingCopyValue != null)
            {
                await CopyToClipboard(_pendingCopyValue);
            }
        };
        menu.Items.Add(copyItem);

        // Get cursor position in screen coordinates and convert to client coordinates
        if (GetCursorPos(out POINT cursorPos))
        {
            var window = Application.Current?.Windows[0].Handler?.PlatformView as Microsoft.UI.Xaml.Window;
            if (window != null)
            {
                var windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(window);
                
                // Convert screen coordinates to window client coordinates
                ScreenToClient(windowHandle, ref cursorPos);
                
                // Account for DPI scaling
                var scale = platformView.XamlRoot?.RasterizationScale ?? 1.0;
                var windowRelativeX = cursorPos.X / scale;
                var windowRelativeY = cursorPos.Y / scale;
                
                // Get the dataGrid's position within the window using TransformToVisual
                var transform = platformView.TransformToVisual(null);
                var gridPositionInWindow = transform.TransformPoint(new Windows.Foundation.Point(0, 0));
                
                // Calculate position relative to the dataGrid element
                var relativeX = windowRelativeX - gridPositionInWindow.X;
                var relativeY = windowRelativeY - gridPositionInWindow.Y;
                
                menu.ShowAt(platformView, new Windows.Foundation.Point(relativeX, relativeY));
                return;
            }
        }
        
        // Fallback to showing at element center
        menu.ShowAt(platformView);
    }
#endif

    private string? GetCellValue(int rowIndex, int columnIndex)
    {
        try
        {
            // Row index 0 is the header, so data starts at index 1
            if (rowIndex < 1 || columnIndex < 0)
                return null;

            var dataRowIndex = rowIndex - 1; // Adjust for header row
            if (dataRowIndex >= _viewModel.FilteredEntries.Count)
                return null;

            var entry = _viewModel.FilteredEntries[dataRowIndex];
            var column = dataGrid.Columns[columnIndex];
            var mappingName = column.MappingName;

            // Use reflection to get the property value
            var property = entry.GetType().GetProperty(mappingName);
            var value = property?.GetValue(entry);
            
            return value?.ToString();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[LocalizationGridPage] Error getting cell value: {ex.Message}");
            return null;
        }
    }

    private async Task CopyToClipboard(string text)
    {
        for (int attempt = 0; attempt < MaxClipboardRetries; attempt++)
        {
            try
            {
                await Clipboard.Default.SetTextAsync(text);
                Debug.WriteLine($"[LocalizationGridPage] Copied to clipboard: {text}");
                return;
            }
            catch (Exception ex) when (attempt < MaxClipboardRetries - 1)
            {
                Debug.WriteLine($"[LocalizationGridPage] Clipboard attempt {attempt + 1} failed: {ex.Message}. Retrying...");
                await Task.Delay(ClipboardRetryDelayMs);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[LocalizationGridPage] All clipboard attempts failed: {ex.Message}");
                await DisplayAlert("Error", "Failed to copy to clipboard. The clipboard may be in use by another application.", "OK");
            }
        }
    }

    // ReSharper disable once MemberCanBeMadeStatic.Local
    private void DataGrid_CurrentCellBeginEdit(object? sender, DataGridCurrentCellBeginEditEventArgs e)
    {
        Debug.WriteLine($"[LocalizationGridPage] Cell edit started - Row: {e.RowColumnIndex.RowIndex}, Column: {e.RowColumnIndex.ColumnIndex}");
        _isInEditMode = true; // Set edit mode flag
        
        // Update tracked position to match the cell being edited
        _currentRowIndex = e.RowColumnIndex.RowIndex;
        _currentColumnIndex = e.RowColumnIndex.ColumnIndex;
        
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
        _isInEditMode = false; // Clear edit mode flag
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
