using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace SmartTravelPlanner.Models
{
    public static class CityName
    {
        public static string Normalize(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return name;
            }

            return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(name.Trim().ToLowerInvariant());
        }
    }

    public class CityGraph
    {
        public class Edge
        {
            public string From { get; set; }
            public string To { get; set; }
            public int Distance { get; set; }

            public Edge(string from, string to, int distance)
            {
                From = from;
                To = to;
                Distance = distance;
            }

            public override string ToString()
            {
                return $"{From}-{To},{Distance}";
            }
        }

        private readonly Dictionary<string, List<Edge>> adjacencyList = new(StringComparer.OrdinalIgnoreCase);

        public void AddEdge(string from, string to, int distance)
        {
            if (distance <= 0)
            {
                throw new ArgumentException("Distance must be greater than zero.", nameof(distance));
            }

            from = CityName.Normalize(from);
            to = CityName.Normalize(to);

            if (string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(to))
            {
                throw new ArgumentException("City names cannot be empty.");
            }

            if (string.Equals(from, to, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("Start and destination cities cannot be identical.");
            }

            if (!adjacencyList.TryGetValue(from, out List<Edge> fromEdges))
            {
                fromEdges = new List<Edge>();
                adjacencyList[from] = fromEdges;
            }

            if (!adjacencyList.TryGetValue(to, out List<Edge> toEdges))
            {
                toEdges = new List<Edge>();
                adjacencyList[to] = toEdges;
            }

            fromEdges.RemoveAll(e => string.Equals(e.To, to, StringComparison.OrdinalIgnoreCase));
            toEdges.RemoveAll(e => string.Equals(e.To, from, StringComparison.OrdinalIgnoreCase));

            fromEdges.Add(new Edge(from, to, distance));
            toEdges.Add(new Edge(to, from, distance));
        }

        public bool RemoveEdge(string from, string to)
        {
            from = CityName.Normalize(from);
            to = CityName.Normalize(to);

            bool removedDirect = false;
            bool removedReverse = false;

            if (adjacencyList.TryGetValue(from, out var fromEdges))
            {
                removedDirect = fromEdges.RemoveAll(e => string.Equals(e.To, to, StringComparison.OrdinalIgnoreCase)) > 0;
                if (fromEdges.Count == 0)
                {
                    adjacencyList.Remove(from);
                }
            }

            if (adjacencyList.TryGetValue(to, out var toEdges))
            {
                removedReverse = toEdges.RemoveAll(e => string.Equals(e.To, from, StringComparison.OrdinalIgnoreCase)) > 0;
                if (toEdges.Count == 0)
                {
                    adjacencyList.Remove(to);
                }
            }

            return removedDirect || removedReverse;
        }

        public bool ContainsCity(string city)
        {
            if (string.IsNullOrWhiteSpace(city)) return false;
            return adjacencyList.ContainsKey(CityName.Normalize(city));
        }

        public static CityGraph LoadFromFile(string filePath)
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("Map file not found.");
            }

            var graph = new CityGraph();
            string[] lines = File.ReadAllLines(filePath);

            for (int i = 0; i < lines.Length; i++)
            {
                int lineNumber = i + 1;
                string line = lines[i];

                if (string.IsNullOrWhiteSpace(line)) continue;

                string[] parts = line.Split(',');
                if (parts.Length != 2)
                {
                    throw new FormatException($"Line {lineNumber}: invalid format (expected 'CityA-CityB,distance').");
                }

                string[] cities = parts[0].Split('-');
                if (cities.Length != 2)
                {
                    throw new FormatException($"Line {lineNumber}: invalid city pair connection '{parts[0]}'.");
                }

                string cityA = cities[0].Trim();
                string cityB = cities[1].Trim();

                if (string.IsNullOrWhiteSpace(cityA) || string.IsNullOrWhiteSpace(cityB))
                {
                    throw new FormatException($"Line {lineNumber}: city name cannot be empty.");
                }

                if (string.Equals(cityA, cityB, StringComparison.OrdinalIgnoreCase))
                {
                    throw new FormatException($"Line {lineNumber}: loop detected ('{cityA}' to itself).");
                }

                if (!int.TryParse(parts[1].Trim(), out int distance) || distance <= 0)
                {
                    throw new FormatException($"Line {lineNumber}: distance must be a positive integer, got '{parts[1].Trim()}'.");
                }

                try
                {
                    graph.AddEdge(cityA, cityB, distance);
                }
                catch (Exception ex)
                {
                    throw new FormatException($"Line {lineNumber}: {ex.Message}", ex);
                }
            }

            return graph;
        }

        public List<string> FindShortestPath(string from, string to)
        {
            to = CityName.Normalize(to);
            from = CityName.Normalize(from);

            if (string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(to))
            {
                return new List<string>();
            }

            if (!adjacencyList.ContainsKey(from) || !adjacencyList.ContainsKey(to))
            {
                return new List<string>();
            }

            if (from == to)
            {
                return new List<string> { from };
            }

            Dictionary<string, int> distances = new(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, string> previous = new(StringComparer.OrdinalIgnoreCase);
            HashSet<string> unvisited = new(StringComparer.OrdinalIgnoreCase);

            foreach (var city in adjacencyList.Keys)
            {
                distances[city] = int.MaxValue;
                previous[city] = null;
                unvisited.Add(city);
            }

            distances[from] = 0;

            while (unvisited.Count > 0)
            {
                string current = null;
                int minDistance = int.MaxValue;

                foreach (var city in unvisited)
                {
                    if (distances[city] < minDistance)
                    {
                        minDistance = distances[city];
                        current = city;
                    }
                }

                if (current == null || minDistance == int.MaxValue)
                {
                    break;
                }

                unvisited.Remove(current);

                if (current == to)
                {
                    break;
                }

                if (adjacencyList.TryGetValue(current, out List<Edge> edges))
                {
                    foreach (var edge in edges)
                    {
                        if (!unvisited.Contains(edge.To))
                        {
                            continue;
                        }

                        int alt = distances[current] + edge.Distance;
                        if (alt < distances[edge.To])
                        {
                            distances[edge.To] = alt;
                            previous[edge.To] = current;
                        }
                    }
                }
            }

            if (distances[to] == int.MaxValue)
            {
                return new List<string>();
            }

            List<string> path = new();
            string step = to;

            while (step != null)
            {
                path.Add(step);
                previous.TryGetValue(step, out step);
            }

            path.Reverse();
            return path;
        }

        public int GetPathDistance(List<string> path)
        {
            if (path == null || path.Count < 2)
            {
                return 0;
            }

            int totalDistance = 0;

            for (int i = 0; i < path.Count - 1; i++)
            {
                string from = path[i];
                string to = path[i + 1];

                if (!adjacencyList.TryGetValue(from, out List<Edge> edges))
                {
                    return -1;
                }

                bool found = false;
                foreach (var edge in edges)
                {
                    if (string.Equals(edge.To, to, StringComparison.OrdinalIgnoreCase))
                    {
                        totalDistance += edge.Distance;
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    return -1;
                }
            }

            return totalDistance;
        }
    }
}