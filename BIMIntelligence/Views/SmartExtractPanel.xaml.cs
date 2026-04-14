using System.Dynamic;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Autodesk.Revit.DB;
using BIMIntelligence.Models;
using BIMIntelligence.Services;
using Microsoft.Win32;
using Newtonsoft.Json;
using WpfBinding = System.Windows.Data.Binding;

namespace BIMIntelligence.Views;

public partial class SmartExtractPanel : Window
{
    private readonly Document _doc;
    private readonly ModelSummary _summary;
    private readonly List<CategoryItem> _allCategories;
    private string _currentCategoryName = "";
    private List<string> _currentColumns = new();
    private List<ExpandoObject> _currentRows = new();

    public SmartExtractPanel(Document doc, ModelSummary summary)
    {
        InitializeComponent();
        _doc = doc;
        _summary = summary;

        ProjectInfoText.Text = $"{summary.ProjectName} - {summary.TotalElements:N0} total elements across {summary.CategoryCounts.Count} categories";

        // Build category items with bar widths
        var maxCount = summary.CategoryCounts.Values.Max();
        _allCategories = summary.CategoryCounts.Select(kv => new CategoryItem
        {
            Name = kv.Key,
            Count = kv.Value,
            BarWidth = maxCount > 0 ? (int)(300.0 * kv.Value / maxCount) : 0
        }).ToList();

        CategoryListView.ItemsSource = _allCategories;
        CategoryStatusText.Text = $"{_allCategories.Count} categories found";
        CategoryListView.SelectionChanged += (s, e) =>
        {
            ExtractButton.IsEnabled = CategoryListView.SelectedItem != null;
        };
    }

    private void OnSearchChanged(object sender, TextChangedEventArgs e)
    {
        var query = SearchBox.Text.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(query))
        {
            CategoryListView.ItemsSource = _allCategories;
        }
        else
        {
            CategoryListView.ItemsSource = _allCategories
                .Where(c => c.Name.ToLowerInvariant().Contains(query))
                .ToList();
        }
    }

    private void OnCategoryDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (CategoryListView.SelectedItem is CategoryItem item)
        {
            ExtractCategory(item.Name);
        }
    }

    private void OnExtractClick(object sender, RoutedEventArgs e)
    {
        if (CategoryListView.SelectedItem is CategoryItem item)
        {
            ExtractCategory(item.Name);
        }
    }

    private void ExtractCategory(string categoryName)
    {
        try
        {
            Mouse.OverrideCursor = Cursors.Wait;
            _currentCategoryName = categoryName;

            var (columns, rows) = RoomDataService.ExtractCategoryElements(_doc, categoryName);
            _currentColumns = columns;
            _currentRows = rows;

            // Build DataGrid columns dynamically
            ElementDataGrid.Columns.Clear();
            foreach (var col in columns)
            {
                var binding = new WpfBinding(col);
                ElementDataGrid.Columns.Add(new DataGridTextColumn
                {
                    Header = col,
                    Binding = binding,
                    Width = col == "Element Id" ? new DataGridLength(80) :
                            col == "Family" || col == "Type" ? new DataGridLength(150) :
                            new DataGridLength(120)
                });
            }

            ElementDataGrid.ItemsSource = rows;

            // Switch to Phase 2
            DataGridHeader.Text = categoryName;
            DataGridSubtitle.Text = $"{rows.Count} elements with {columns.Count} parameters";
            DataStatusText.Text = $"{rows.Count} elements extracted";

            CategoryPickerGrid.Visibility = System.Windows.Visibility.Collapsed;
            DataGridPanel.Visibility = System.Windows.Visibility.Visible;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error extracting {categoryName}: {ex.Message}", "Extraction Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            Mouse.OverrideCursor = null;
        }
    }

    private void OnBackClick(object sender, RoutedEventArgs e)
    {
        DataGridPanel.Visibility = System.Windows.Visibility.Collapsed;
        CategoryPickerGrid.Visibility = System.Windows.Visibility.Visible;
    }

    private void OnExportJsonClick(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            FileName = $"{_currentCategoryName.Replace(" ", "_")}_data.json",
            DefaultExt = ".json",
            Filter = "JSON files (*.json)|*.json"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                // Convert ExpandoObjects to dictionaries for clean JSON
                var exportData = _currentRows.Select(row =>
                    ((IDictionary<string, object?>)row).ToDictionary(kv => kv.Key, kv => kv.Value)
                ).ToList();

                var json = JsonConvert.SerializeObject(exportData, Formatting.Indented);
                File.WriteAllText(dialog.FileName, json);
                MessageBox.Show($"Exported {_currentRows.Count} elements to {dialog.FileName}",
                    "Export Successful", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Export failed: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void OnExportCsvClick(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            FileName = $"{_currentCategoryName.Replace(" ", "_")}_data.csv",
            DefaultExt = ".csv",
            Filter = "CSV files (*.csv)|*.csv"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                var sb = new StringBuilder();

                // Header row
                sb.AppendLine(string.Join(",", _currentColumns.Select(EscapeCsv)));

                // Data rows
                foreach (var row in _currentRows)
                {
                    var dict = (IDictionary<string, object?>)row;
                    var values = _currentColumns.Select(col =>
                        EscapeCsv(dict.ContainsKey(col) ? dict[col]?.ToString() ?? "" : ""));
                    sb.AppendLine(string.Join(",", values));
                }

                File.WriteAllText(dialog.FileName, sb.ToString());
                MessageBox.Show($"Exported {_currentRows.Count} elements to {dialog.FileName}",
                    "Export Successful", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Export failed: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private static string EscapeCsv(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }
}

public class CategoryItem
{
    public string Name { get; set; } = "";
    public int Count { get; set; }
    public int BarWidth { get; set; }
}
