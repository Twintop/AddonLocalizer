using AddonLocalizer.PageModels;
using System.Diagnostics;
using Syncfusion.Maui.DataGrid;
using Microsoft.Maui.Controls;

namespace AddonLocalizer.Pages;

public partial class LocalizationGridPage : ContentPage
{
    private readonly LocalizationGridPageModel _viewModel;
    private SfDataGrid? dataGrid;

    public LocalizationGridPage(LocalizationGridPageModel viewModel)
    {
        try
        {
            Debug.WriteLine("[LocalizationGridPage] Constructor started");
            InitializeComponent();
            Debug.WriteLine("[LocalizationGridPage] InitializeComponent completed");
            
            _viewModel = viewModel;
            BindingContext = viewModel;
            
            Debug.WriteLine("[LocalizationGridPage] BindingContext set");
            
            // Subscribe to property changes to handle data loading
            _viewModel.PropertyChanged += ViewModel_PropertyChanged;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[LocalizationGridPage] Error in constructor: {ex.Message}");
            Debug.WriteLine($"[LocalizationGridPage] Stack trace: {ex.StackTrace}");
            throw;
        }
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        Debug.WriteLine("[LocalizationGridPage] OnAppearing called");
        
        // Create the grid AFTER the page has appeared
        Task.Run(async () =>
        {
            await Task.Delay(200);
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                try
                {
                    Debug.WriteLine("[LocalizationGridPage] Creating Syncfusion DataGrid programmatically");
                    
                    dataGrid = new SfDataGrid
                    {
                        SelectionMode = DataGridSelectionMode.None,
                        AutoGenerateColumnsMode = AutoGenerateColumnsMode.None,
                        HeaderRowHeight = 48,
                        RowHeight = 44,
                        IsVisible = false,
                        HorizontalOptions = LayoutOptions.Fill,
                        VerticalOptions = LayoutOptions.Fill,
                        GridLinesVisibility = GridLinesVisibility.Both,
                        HeaderGridLinesVisibility = GridLinesVisibility.Both,
                        Background = new SolidColorBrush(Color.FromArgb("#1E1E1E")),
                        DefaultStyle = new DataGridStyle()
                        {
                            HeaderRowTextColor = Color.FromArgb("#FFFFFF"),
                            RowTextColor = Color.FromArgb("#FFFFFF")
                        }
                    };
                    
                    // Add columns - remove invalid CellStyle property
                    dataGrid.Columns.Add(new DataGridTextColumn { HeaderText = "Glue String", MappingName = "GlueString", Width = 300 });
                    dataGrid.Columns.Add(new DataGridTextColumn { HeaderText = "Count", MappingName = "OccurrenceCount", Width = 80 });
                    dataGrid.Columns.Add(new DataGridTextColumn { HeaderText = "Concat", MappingName = "ConcatenationIcon", Width = 80 });
                    dataGrid.Columns.Add(new DataGridTextColumn { HeaderText = "Format", MappingName = "StringFormatIcon", Width = 80 });
                    dataGrid.Columns.Add(new DataGridTextColumn { HeaderText = "Params", MappingName = "ParameterCount", Width = 80 });
                    dataGrid.Columns.Add(new DataGridTextColumn { HeaderText = "Files", MappingName = "FileLocations", Width = 200 });
                    
                    gridContainer.Add(dataGrid);
                    
                    Debug.WriteLine("[LocalizationGridPage] DataGrid created and added to container");
                    
                    // If data is already loaded, bind it now
                    if (_viewModel.HasData && _viewModel.FilteredEntries.Count > 0)
                    {
                        Debug.WriteLine($"[LocalizationGridPage] Data already loaded, binding {_viewModel.FilteredEntries.Count} items");
                        dataGrid.ItemsSource = _viewModel.FilteredEntries;
                        dataGrid.IsVisible = true;
                        Debug.WriteLine("[LocalizationGridPage] DataGrid made visible with existing data");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[LocalizationGridPage] Error creating DataGrid: {ex.Message}");
                    Debug.WriteLine($"[LocalizationGridPage] Stack trace: {ex.StackTrace}");
                }
            });
        });
    }

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        try
        {
            if (e.PropertyName == nameof(LocalizationGridPageModel.HasData))
            {
                Debug.WriteLine($"[LocalizationGridPage] HasData changed to: {_viewModel.HasData}");
                
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    try
                    {
                        if (dataGrid != null)
                        {
                            if (_viewModel.HasData)
                            {
                                // Bind data first
                                dataGrid.ItemsSource = _viewModel.FilteredEntries;
                                Debug.WriteLine($"[LocalizationGridPage] DataGrid ItemsSource bound with {_viewModel.FilteredEntries.Count} items");
                                
                                // Make visible and force layout update
                                Task.Delay(100).ContinueWith(_ =>
                                {
                                    MainThread.BeginInvokeOnMainThread(() =>
                                    {
                                        try
                                        {
                                            dataGrid.IsVisible = true;
                                            dataGrid.Opacity = 1.0;
                                            
                                            // Force layout updates
                                            dataGrid.InvalidateMeasure();
                                            this.InvalidateMeasure();
                                            
                                            // Additional delay for layout to complete
                                            Task.Delay(100).ContinueWith(__ =>
                                            {
                                                MainThread.BeginInvokeOnMainThread(() =>
                                                {
                                                    dataGrid.InvalidateMeasure();
                                                    
                                                    Debug.WriteLine("[LocalizationGridPage] DataGrid made visible");
                                                    Debug.WriteLine($"[LocalizationGridPage] Grid Width: {dataGrid.Width}, Height: {dataGrid.Height}");
                                                    Debug.WriteLine($"[LocalizationGridPage] Grid IsVisible: {dataGrid.IsVisible}, Opacity: {dataGrid.Opacity}");
                                                    Debug.WriteLine($"[LocalizationGridPage] Grid Bounds: {dataGrid.Bounds}");
                                                    Debug.WriteLine($"[LocalizationGridPage] Container Width: {gridContainer?.Width}, Height: {gridContainer?.Height}");
                                                    Debug.WriteLine($"[LocalizationGridPage] Container Bounds: {gridContainer?.Bounds}");
                                                });
                                            });
                                        }
                                        catch (System.Runtime.InteropServices.COMException comEx)
                                        {
                                            Debug.WriteLine($"[LocalizationGridPage] COM Exception when making visible (non-fatal): {comEx.Message}");
                                            Debug.WriteLine($"[LocalizationGridPage] HRESULT: 0x{comEx.HResult:X8}");
                                        }
                                        catch (Exception ex)
                                        {
                                            Debug.WriteLine($"[LocalizationGridPage] Error making grid visible: {ex.Message}");
                                        }
                                    });
                                });
                            }
                            else
                            {
                                dataGrid.IsVisible = false;
                                dataGrid.ItemsSource = null;
                                Debug.WriteLine("[LocalizationGridPage] DataGrid hidden and cleared");
                            }
                        }
                        else
                        {
                            Debug.WriteLine("[LocalizationGridPage] DataGrid is null, waiting for creation");
                        }
                    }
                    catch (System.Runtime.InteropServices.COMException comEx)
                    {
                        Debug.WriteLine($"[LocalizationGridPage] COM Exception in HasData handler: {comEx.Message}");
                        Debug.WriteLine($"[LocalizationGridPage] HRESULT: 0x{comEx.HResult:X8}");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[LocalizationGridPage] Error in HasData handler: {ex.Message}");
                    }
                });
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[LocalizationGridPage] Error in PropertyChanged handler: {ex.Message}");
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        Debug.WriteLine("[LocalizationGridPage] OnDisappearing called");
        
        if (_viewModel != null)
        {
            _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
        }
    }
}
