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
        private const string SCORES_FILE = "scores.json";
        private static Dictionary<string, List<LocalScore>> _scoreDb = new();

        static LocalScoreManager()
        {
            LoadScores();
        }

        private static void LoadScores()
        {
            try
            {
                if (File.Exists(SCORES_FILE))
                {
                    string json = File.ReadAllText(SCORES_FILE);
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
                string json = JsonSerializer.Serialize(_scoreDb, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(SCORES_FILE, json);
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

        public static List<LocalScore> GetScores(string beatmapPath)
        {
            string key = Path.GetFileName(beatmapPath).ToLowerInvariant();
            if (_scoreDb.TryGetValue(key, out var scores))
            {
                return scores;
            }
            return new List<LocalScore>();
        }
    }
}
