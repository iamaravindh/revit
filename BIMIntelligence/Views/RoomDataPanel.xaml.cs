using System.IO;
using System.Windows;
using BIMIntelligence.Models;
using Microsoft.Win32;
using Newtonsoft.Json;

namespace BIMIntelligence.Views;

public partial class RoomDataPanel : Window
{
    private List<RoomData> _roomData = new();

    public RoomDataPanel()
    {
        InitializeComponent();
    }

    public void LoadData(List<RoomData> rooms)
    {
        _roomData = rooms;
        RoomDataGrid.ItemsSource = _roomData;
        StatusText.Text = $"{_roomData.Count} rooms found";
    }

    private void OnExportClick(object sender, RoutedEventArgs e)
    {
        if (_roomData.Count == 0)
        {
            MessageBox.Show("No data to export.", "Export", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var saveDialog = new SaveFileDialog
        {
            Filter = "JSON Files (*.json)|*.json",
            FileName = "room_data.json",
            DefaultExt = ".json"
        };

        if (saveDialog.ShowDialog() == true)
        {
            var json = JsonConvert.SerializeObject(_roomData, Formatting.Indented);
            File.WriteAllText(saveDialog.FileName, json);
            MessageBox.Show($"Exported {_roomData.Count} rooms to:\n{saveDialog.FileName}",
                "Export Successful", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
