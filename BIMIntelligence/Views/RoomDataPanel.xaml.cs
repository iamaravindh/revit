using System.IO;
using System.Text;
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

    private void OnExportCsvClick(object sender, RoutedEventArgs e)
    {
        if (_roomData.Count == 0)
        {
            MessageBox.Show("No data to export.", "Export", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var saveDialog = new SaveFileDialog
        {
            Filter = "CSV Files (*.csv)|*.csv",
            FileName = "room_data.csv",
            DefaultExt = ".csv"
        };

        if (saveDialog.ShowDialog() == true)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Room Name,Room Number,Level,Area (m²),Doors,Windows");

            foreach (var room in _roomData)
            {
                sb.AppendLine($"{EscapeCsv(room.RoomName)},{EscapeCsv(room.RoomNumber)},{EscapeCsv(room.Level)},{room.AreaSqM},{room.DoorCount},{room.WindowCount}");
            }

            File.WriteAllText(saveDialog.FileName, sb.ToString());
            MessageBox.Show($"Exported {_roomData.Count} rooms to:\n{saveDialog.FileName}",
                "Export Successful", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private static string EscapeCsv(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }
}
