#nullable enable

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Text.Json;

namespace Nullscent.Core
{
    /// <summary>
    /// Simple settings service for runtime configuration.
    /// Exposes observables and a generic setting-changed event.
    /// Supports persistence to JSON file.
    /// Singleton pattern for ease of access across the app.
    /// </summary>
    public sealed class SettingsService : INotifyPropertyChanged
    {
        private static readonly Lazy<SettingsService> _instance = new(() => new SettingsService());
        public static SettingsService Instance => _instance.Value;

        private const string SETTINGS_FILE = "settings.json";

        public event PropertyChangedEventHandler? PropertyChanged;

        public event Action<string, object?>? SettingChanged;

        private readonly Dictionary<string, object?> _store = new();

        private SettingsService() 
        {
            LoadSettings();

            // Apply fallback defaults if not loaded from file
            EnsureDefault(nameof(ScrollSpeed), 1.0);
            EnsureDefault(nameof(ReceptorPosition), 0.8f);
            EnsureDefault(nameof(DownScroll), false);
            EnsureDefault(nameof(GlobalOffsetMs), 0);
            EnsureDefault(nameof(Volume), 1.0f);
            EnsureDefault(nameof(HitsoundVolume), 1.0f);
            EnsureDefault(nameof(MusicVolume), 1.0f);
            EnsureDefault(nameof(MasterVolume), 1.0f);
            EnsureDefault(nameof(ShowHitErrorBar), true);
        }

        private void EnsureDefault<T>(string key, T defaultValue)
        {
            if (!_store.ContainsKey(key))
                _store[key] = defaultValue;
        }

        private void RaisePropertyChanged(string propName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
        }

        private void RaiseSettingChanged(string key, object? value)
        {
            SettingChanged?.Invoke(key, value);
        }

        // Gameplay settings
        public double ScrollSpeed
        {
            get => Get<double>(nameof(ScrollSpeed));
            set => Set(nameof(ScrollSpeed), value);
        }

        public float ReceptorPosition
        {
            get => Get<float>(nameof(ReceptorPosition));
            set => Set(nameof(ReceptorPosition), value);
        }

        public bool DownScroll
        {
            get => Get<bool>(nameof(DownScroll));
            set => Set(nameof(DownScroll), value);
        }

        public int GlobalOffsetMs
        {
            get => Get<int>(nameof(GlobalOffsetMs));
            set => Set(nameof(GlobalOffsetMs), value);
        }

        // Audio settings
        public float Volume
        {
            get => Get<float>(nameof(Volume));
            set => Set(nameof(Volume), value);
        }

        public float HitsoundVolume
        {
            get => Get<float>(nameof(HitsoundVolume));
            set => Set(nameof(HitsoundVolume), value);
        }

        public float MusicVolume
        {
            get => Get<float>(nameof(MusicVolume));
            set => Set(nameof(MusicVolume), value);
        }

        public float MasterVolume
        {
            get => Get<float>(nameof(MasterVolume));
            set => Set(nameof(MasterVolume), value);
        }

        // Visual settings
        public bool ShowHitErrorBar
        {
            get => Get<bool>(nameof(ShowHitErrorBar));
            set => Set(nameof(ShowHitErrorBar), value);
        }

        // Generic getter/setter backed by dictionary
        private T Get<T>(string key)
        {
            if (_store.TryGetValue(key, out var o) && o is T t) return t;

            // fallback defaults for simple types
            return typeof(T) == typeof(bool) ? (T)(object)false : default!;
        }

        private void Set<T>(string key, T value)
        {
            _store[key] = value;
            RaisePropertyChanged(key);
            RaiseSettingChanged(key, value);
        }

        /// <summary>
        /// Load settings from JSON file.
        /// </summary>
        public void LoadSettings()
        {
            try
            {
                if (File.Exists(SETTINGS_FILE))
                {
                    string json = File.ReadAllText(SETTINGS_FILE);
                    var options = new JsonSerializerOptions { WriteIndented = true };
                    var data = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json, options);

                    if (data != null)
                    {
                        foreach (var kvp in data)
                        {
                            _store[kvp.Key] = DeserializeJsonElement(kvp.Value);
                        }
                    }

                    Console.WriteLine($"[SettingsService] Loaded settings from {SETTINGS_FILE}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SettingsService] Error loading settings: {ex.Message}");
            }
        }

        /// <summary>
        /// Save settings to JSON file.
        /// </summary>
        public void SaveSettings()
        {
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(_store, options);
                File.WriteAllText(SETTINGS_FILE, json);
                Console.WriteLine($"[SettingsService] Saved settings to {SETTINGS_FILE}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SettingsService] Error saving settings: {ex.Message}");
            }
        }

        private object? DeserializeJsonElement(JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.String => element.GetString(),
                JsonValueKind.Number => element.GetDouble(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Null => null,
                _ => element.GetRawText()
            };
        }
    }
}

