using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using SmartTravelPlanner.Models;

namespace SmartTravelPlanner;

public partial class MainWindow : Window
{
    private CityGraph? _graph;
    private Traveler? _traveler;

    public MainWindow()
    {
        InitializeComponent();
    }

    private async System.Threading.Tasks.Task ShowMessage(string title, string message)
    {
        var dialog = new Window
        {
            Title = title,
            Width = 360,
            Height = 160,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false
        };

        var panel = new StackPanel
        {
            Margin = new Avalonia.Thickness(16),
            Spacing = 16,
            VerticalAlignment = VerticalAlignment.Center
        };

        var text = new TextBlock
        {
            Text = message,
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            HorizontalAlignment = HorizontalAlignment.Center
        };

        var button = new Button
        {
            Content = "OK",
            HorizontalAlignment = HorizontalAlignment.Center,
            Width = 80
        };

        button.Click += (_, _) => dialog.Close();

        panel.Children.Add(text);
        panel.Children.Add(button);
        dialog.Content = panel;

        await dialog.ShowDialog(this);
    }

    private async void OnCreateTravelerClick(object? sender, RoutedEventArgs e)
    {
        string name = TxtTravelerName.Text?.Trim() ?? string.Empty;
        string location = TxtLocation.Text?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(location))
        {
            await ShowMessage("Input Error", "Traveler name and location cannot be empty.");
            return;
        }

        _traveler = new Traveler(name);
        _traveler.SetLocation(location);

        LblStatus.Text = $"Status: Traveler '{_traveler.GetName()}' created at '{_traveler.GetLocation()}'.";
    }

    private async void OnLoadMapClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Open Map File",
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("Text files (*.txt)") { Patterns = new[] { "*.txt" } },
                    new FilePickerFileType("All files") { Patterns = new[] { "*.*" } }
                }
            });

            if (files.Count == 0) return;

            string filePath = files[0].Path.LocalPath;
            _graph = CityGraph.LoadFromFile(filePath);

            LblStatus.Text = $"Status: Map loaded successfully from '{files[0].Name}'.";
        }
        catch (Exception ex)
        {
            await ShowMessage("Map Error", $"Failed to load map: {ex.Message}");
        }
    }

    private async void OnPlanRouteClick(object? sender, RoutedEventArgs e)
    {
        if (_traveler == null)
        {
            await ShowMessage("Validation Error", "Please create or load a traveler first.");
            return;
        }

        if (_graph == null)
        {
            await ShowMessage("Validation Error", "Please load a map before planning a route.");
            return;
        }

        string destination = TxtDestination.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(destination))
        {
            await ShowMessage("Validation Error", "Destination field cannot be empty.");
            return;
        }

        string start = _traveler.GetLocation();
        destination = CityName.Normalize(destination);

        if (!_graph.ContainsCity(start))
        {
            await ShowMessage("Route Error", $"Current location '{start}' is not found in the map.");
            return;
        }

        if (!_graph.ContainsCity(destination))
        {
            await ShowMessage("Route Error", $"Destination '{destination}' is not found in the map.");
            return;
        }

        List<string> path = _graph.FindShortestPath(start, destination);
        if (path.Count == 0)
        {
            await ShowMessage("Route Error", $"Destination '{destination}' is not reachable from '{start}'.");
            return;
        }

        _traveler.ClearRoute();
        foreach (string city in path)
        {
            _traveler.AddCity(city);
        }

        int distance = _graph.GetPathDistance(path);
        if (distance < 0)
        {
            await ShowMessage("Route Error", "Route contains disconnected road segments.");
            return;
        }

        TxtRouteOutput.Text = _traveler.GetRoute();
        LblTotalDistance.Text = $"Total distance: {distance} km";
        LblStatus.Text = $"Status: Route planned to {destination}.";
    }

    private async void OnAddEdgeClick(object? sender, RoutedEventArgs e)
    {
        if (_graph == null)
        {
            _graph = new CityGraph();
        }

        string cityA = TxtCityA.Text?.Trim() ?? string.Empty;
        string cityB = TxtCityB.Text?.Trim() ?? string.Empty;
        string distText = TxtDistance.Text?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(cityA) || string.IsNullOrWhiteSpace(cityB))
        {
            await ShowMessage("Input Error", "Both city names must be provided.");
            return;
        }

        if (!int.TryParse(distText, out int dist) || dist <= 0)
        {
            await ShowMessage("Input Error", "Distance must be a positive integer.");
            return;
        }

        try
        {
            _graph.AddEdge(cityA, cityB, dist);
            LblStatus.Text = $"Status: Edge {CityName.Normalize(cityA)} - {CityName.Normalize(cityB)} ({dist} km) added.";
            TxtCityA.Text = string.Empty;
            TxtCityB.Text = string.Empty;
            TxtDistance.Text = string.Empty;
        }
        catch (Exception ex)
        {
            await ShowMessage("Edge Error", ex.Message);
        }
    }

    private async void OnRemoveEdgeClick(object? sender, RoutedEventArgs e)
    {
        if (_graph == null)
        {
            await ShowMessage("Action Error", "Map is empty.");
            return;
        }

        string cityA = TxtCityA.Text?.Trim() ?? string.Empty;
        string cityB = TxtCityB.Text?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(cityA) || string.IsNullOrWhiteSpace(cityB))
        {
            await ShowMessage("Input Error", "Specify City A and City B to remove an edge.");
            return;
        }

        bool removed = _graph.RemoveEdge(cityA, cityB);
        if (removed)
        {
            LblStatus.Text = $"Status: Edge {CityName.Normalize(cityA)} - {CityName.Normalize(cityB)} removed.";
        }
        else
        {
            await ShowMessage("Action Error", "Edge between the specified cities was not found.");
        }
    }

    private void OnClearRouteClick(object? sender, RoutedEventArgs e)
    {
        _traveler?.ClearRoute();
        TxtRouteOutput.Text = string.Empty;
        LblTotalDistance.Text = "Total distance: -";
        LblStatus.Text = "Status: Route cleared.";
    }

    private async void OnSaveClick(object? sender, RoutedEventArgs e)
    {
        if (_traveler == null)
        {
            await ShowMessage("Save Error", "No traveler data to save.");
            return;
        }

        try
        {
            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Save Traveler Data",
                DefaultExtension = "json",
                SuggestedFileName = "traveler.json",
                FileTypeChoices = new[]
                {
                    new FilePickerFileType("JSON files (*.json)") { Patterns = new[] { "*.json" } }
                }
            });

            if (file == null) return;

            string filePath = file.Path.LocalPath;
            _traveler.SaveToFile(filePath);
            LblStatus.Text = $"Status: Traveler saved to '{file.Name}'.";
        }
        catch (Exception ex)
        {
            await ShowMessage("Save Error", $"Failed to save traveler: {ex.Message}");
        }
    }

    private async void OnLoadClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Load Traveler Data",
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("JSON files (*.json)") { Patterns = new[] { "*.json" } },
                    new FilePickerFileType("All files") { Patterns = new[] { "*.*" } }
                }
            });

            if (files.Count == 0) return;

            string filePath = files[0].Path.LocalPath;
            _traveler = Traveler.LoadFromFile(filePath);

            TxtTravelerName.Text = _traveler.GetName();
            TxtLocation.Text = _traveler.GetLocation();
            TxtRouteOutput.Text = _traveler.GetRoute();

            if (_graph != null && _traveler.GetStopCount() > 1)
            {
                int dist = _graph.GetPathDistance(new List<string>(_traveler.GetRouteList()));
                LblTotalDistance.Text = dist >= 0 ? $"Total distance: {dist} km" : "Total distance: (disconnected)";
            }
            else
            {
                LblTotalDistance.Text = "Total distance: -";
            }

            LblStatus.Text = $"Status: Loaded traveler '{_traveler.GetName()}'.";
        }
        catch (Exception ex)
        {
            await ShowMessage("Load Error", $"Failed to load traveler file: {ex.Message}");
        }
    }

    private void OnExitClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}