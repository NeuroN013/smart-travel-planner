using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace SmartTravelPlanner.Models
{
    public class Traveler : ICloneable
    {
        private List<string> _route = new();
        private string _name;
        private string _location;

        public Traveler(string name)
        {
            _name = name;
        }

        public string GetName() => _name;

        public void SetLocation(string location)
        {
            _location = CityName.Normalize(location);
        }

        public string GetLocation() => _location;

        public void AddCity(string city)
        {
            if (string.IsNullOrWhiteSpace(city))
            {
                throw new ArgumentException("Invalid city");
            }

            _route.Add(CityName.Normalize(city));
        }

        public string GetRoute() => string.Join(" -> ", _route);

        public IReadOnlyList<string> GetRouteList() => _route.AsReadOnly();

        public void ClearRoute() => _route.Clear();

        public int GetStopCount() => _route.Count;

        public object Clone()
        {
            Traveler clone = new Traveler(_name)
            {
                _location = _location,
                _route = new List<string>(_route)
            };
            return clone;
        }

        public void SaveToFile(string filePath)
        {
            var data = new TravelerData
            {
                name = _name,
                currentLocation = _location,
                route = _route
            };

            var options = new JsonSerializerOptions { WriteIndented = true };
            string json = JsonSerializer.Serialize(data, options);
            File.WriteAllText(filePath, json);
        }

        private class TravelerData
        {
            public string name { get; set; }
            public string currentLocation { get; set; }
            public List<string> route { get; set; }
        }

        public static Traveler LoadFromFile(string filePath)
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("Traveler file not found.");
            }

            try
            {
                string jsonString = File.ReadAllText(filePath);

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                var data = JsonSerializer.Deserialize<TravelerData>(jsonString, options);

                if (data == null || string.IsNullOrWhiteSpace(data.name))
                {
                    throw new FileLoadException("Invalid travel data");
                }

                Traveler traveler = new Traveler(data.name);

                if (!string.IsNullOrWhiteSpace(data.currentLocation))
                {
                    traveler.SetLocation(data.currentLocation);
                }

                if (data.route != null)
                {
                    foreach (string city in data.route)
                    {
                        traveler.AddCity(city);
                    }
                }

                return traveler;
            }
            catch (FileLoadException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new FileLoadException("Invalid travel data", ex);
            }
        }
    }
}