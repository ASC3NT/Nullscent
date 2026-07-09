#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Nullscent.Core
{
    public class LocalScore
    {
        public string PlayerName { get; set; } = "Guest";
        public int Score { get; set; }
        public int MaxCombo { get; set; }
        public double Accuracy { get; set; }
        public string Grade { get; set; } = "D";
        public string Mods { get; set; } = "None";
        public DateTime Date { get; set; } = DateTime.Now;
    }

    public static class LocalScoreManager
    {
        private static string GetScoresPath(string fileName)
        {
            // 1. Intentar usar la ruta local (directorio del ejecutable) para mantener portabilidad
            string localPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, fileName);
            try
            {
                // Verificar si tenemos permisos de escritura en la carpeta del ejecutable
                string testFile = localPath + ".tmp";
                File.WriteAllText(testFile, "test");
                File.Delete(testFile);
                return localPath;
            }
            catch
            {
                // 2. Si no hay permisos (ej. instalado en Program Files), usar AppData
                string appDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Nullscent");
                return Path.Combine(appDataFolder, fileName);
            }
        }

        private static readonly string ScoresFilePath = GetScoresPath("scores.json");
        private static Dictionary<string, List<LocalScore>> _scoreDb = new();

        static LocalScoreManager()
        {
            LoadScores();
        }

        private static void LoadScores()
        {
            try
            {
                if (File.Exists(ScoresFilePath))
                {
                    string json = File.ReadAllText(ScoresFilePath);
                    var db = JsonSerializer.Deserialize<Dictionary<string, List<LocalScore>>>(json);
                    if (db != null)
                    {
                        _scoreDb = db;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LocalScoreManager] Error loading scores: {ex.Message}");
            }
        }

        private static void SaveScores()
        {
            try
            {
                // Asegurar que el directorio de destino exista (especialmente para AppData)
                string? directory = Path.GetDirectoryName(ScoresFilePath);
                if (directory != null)
                {
                    Directory.CreateDirectory(directory);
                }

                string json = JsonSerializer.Serialize(_scoreDb, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(ScoresFilePath, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LocalScoreManager] Error saving scores: {ex.Message}");
            }
        }

        public static void AddScore(string beatmapPath, LocalScore score)
        {
            string key = Path.GetFileName(beatmapPath).ToLowerInvariant();
            if (!_scoreDb.ContainsKey(key))
            {
                _scoreDb[key] = new List<LocalScore>();
            }

            _scoreDb[key].Add(score);
            
            // Sort by Score descending, then by Accuracy descending, limit to top 50
            _scoreDb[key] = _scoreDb[key]
                .OrderByDescending(s => s.Score)
                .ThenByDescending(s => s.Accuracy)
                .Take(50)
                .ToList();

            SaveScores();
            Console.WriteLine($"[LocalScoreManager] Added score for {key}: {score.Score} (Rank: {score.Grade})");
        }

        private static readonly List<LocalScore> _emptyList = new();

        public static List<LocalScore> GetScores(string beatmapPath)
        {
            if (string.IsNullOrEmpty(beatmapPath)) return _emptyList;
            string key = Path.GetFileName(beatmapPath).ToLowerInvariant();
            if (_scoreDb.TryGetValue(key, out var scores))
            {
                return scores;
            }
            return _emptyList;
        }
    }
}
