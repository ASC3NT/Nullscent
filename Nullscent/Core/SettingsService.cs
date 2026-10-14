#nullable enable

using System;
using System.ComponentModel;
using Nullscent.Config;
using Nullscent.Skin;

namespace Nullscent.Core
{
    /// <summary>
    /// Singleton wrapper service that provides runtime observabilidad for settings changes.
    /// Redirects all getters/setters to the unified GameSettings instance to prevent settings loss.
    /// </summary>
    public sealed class SettingsService : INotifyPropertyChanged
    {
        private static readonly Lazy<SettingsService> _instance = new(() => new SettingsService());
        public static SettingsService Instance => _instance.Value;

        public event PropertyChangedEventHandler? PropertyChanged;
        public event Action<string, object?>? SettingChanged;

        private GameSettings? _settings;

        private SettingsService() 
        {
        }

        /// <summary>
        /// Initializes the SettingsService with the active GameSettings instance.
        /// </summary>
        public void Initialize(GameSettings settings)
        {
            _settings = settings;
            Console.WriteLine("[SettingsService] Initialized with active GameSettings instance");
        }

        private GameSettings Settings
        {
            get
            {
                if (_settings == null)
                {
                    // Fallback to loading default settings if not initialized
                    _settings = GameSettings.Load();
                    _settings.Validate();
                }
                return _settings;
            }
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
            get => Settings.ScrollSpeed;
            set
            {
                int intVal = (int)Math.Round(value);
                if (Settings.ScrollSpeed != intVal)
                {
                    Settings.ScrollSpeed = intVal;
                    RaisePropertyChanged(nameof(ScrollSpeed));
                    RaiseSettingChanged(nameof(ScrollSpeed), (double)intVal);
                }
            }
        }

        public float ReceptorPosition
        {
            get => Settings.ReceptorPosition;
            set
            {
                if (Settings.ReceptorPosition != value)
                {
                    Settings.ReceptorPosition = value;
                    RaisePropertyChanged(nameof(ReceptorPosition));
                    RaiseSettingChanged(nameof(ReceptorPosition), value);
                }
            }
        }

        public bool UseSkinReceptorPosition
        {
            get => Settings.UseSkinReceptorPosition;
            set
            {
                if (Settings.UseSkinReceptorPosition != value)
                {
                    Settings.UseSkinReceptorPosition = value;
                    RaisePropertyChanged(nameof(UseSkinReceptorPosition));
                    RaiseSettingChanged(nameof(UseSkinReceptorPosition), value);
                }
            }
        }

        public bool UseSkinScrollDirection
        {
            get => Settings.UseSkinScrollDirection;
            set
            {
                if (Settings.UseSkinScrollDirection != value)
                {
                    Settings.UseSkinScrollDirection = value;
                    RaisePropertyChanged(nameof(UseSkinScrollDirection));
                    RaiseSettingChanged(nameof(UseSkinScrollDirection), value);
                }
            }
        }

        public bool DownScroll
        {
            get => Settings.DownScroll;
            set
            {
                if (Settings.DownScroll != value)
                {
                    Settings.DownScroll = value;
                    RaisePropertyChanged(nameof(DownScroll));
                    RaiseSettingChanged(nameof(DownScroll), value);
                }
            }
        }

        public bool ScaleScrollSpeedWithBPM
        {
            get => Settings.ScaleScrollSpeedWithBPM;
            set
            {
                if (Settings.ScaleScrollSpeedWithBPM != value)
                {
                    Settings.ScaleScrollSpeedWithBPM = value;
                    RaisePropertyChanged(nameof(ScaleScrollSpeedWithBPM));
                    RaiseSettingChanged(nameof(ScaleScrollSpeedWithBPM), value);
                }
            }
        }

        public int GlobalOffsetMs
        {
            get => (int)Settings.GlobalAudioOffset;
            set
            {
                if ((int)Settings.GlobalAudioOffset != value)
                {
                    Settings.GlobalAudioOffset = value;
                    RaisePropertyChanged(nameof(GlobalOffsetMs));
                    RaiseSettingChanged(nameof(GlobalOffsetMs), value);
                    RaiseSettingChanged("GlobalAudioOffset", (double)value); // Compatibility mapping
                }
            }
        }

        // Audio settings
        public float Volume
        {
            get => Settings.MasterVolume;
            set
            {
                if (Settings.MasterVolume != value)
                {
                    Settings.MasterVolume = value;
                    RaisePropertyChanged(nameof(Volume));
                    RaiseSettingChanged(nameof(Volume), value);
                }
            }
        }

        public float HitsoundVolume
        {
            get => Settings.HitsoundVolume;
            set
            {
                if (Settings.HitsoundVolume != value)
                {
                    Settings.HitsoundVolume = value;
                    RaisePropertyChanged(nameof(HitsoundVolume));
                    RaiseSettingChanged(nameof(HitsoundVolume), value);
                }
            }
        }

        public float MusicVolume
        {
            get => Settings.MusicVolume;
            set
            {
                if (Settings.MusicVolume != value)
                {
                    Settings.MusicVolume = value;
                    RaisePropertyChanged(nameof(MusicVolume));
                    RaiseSettingChanged(nameof(MusicVolume), value);
                }
            }
        }

        public float MasterVolume
        {
            get => Settings.MasterVolume;
            set
            {
                if (Settings.MasterVolume != value)
                {
                    Settings.MasterVolume = value;
                    RaisePropertyChanged(nameof(MasterVolume));
                    RaiseSettingChanged(nameof(MasterVolume), value);
                }
            }
        }

        // Visual settings
        public bool ShowHitErrorBar
        {
            get => Settings.ShowHitErrorBar;
            set
            {
                if (Settings.ShowHitErrorBar != value)
                {
                    Settings.ShowHitErrorBar = value;
                    RaisePropertyChanged(nameof(ShowHitErrorBar));
                    RaiseSettingChanged(nameof(ShowHitErrorBar), value);
                }
            }
        }

        public bool HighResolutionSkins
        {
            get => Settings.HighResolutionSkins;
            set
            {
                if (Settings.HighResolutionSkins != value)
                {
                    Settings.HighResolutionSkins = value;
                    RaisePropertyChanged(nameof(HighResolutionSkins));
                    RaiseSettingChanged(nameof(HighResolutionSkins), value);
                }
            }
        }

        public LegacySkinCompatibilityMode LegacySkinCompatibilityMode
        {
            get => Settings.LegacySkinCompatibilityMode;
            set
            {
                if (Settings.LegacySkinCompatibilityMode != value)
                {
                    Settings.LegacySkinCompatibilityMode = value;
                    RaisePropertyChanged(nameof(LegacySkinCompatibilityMode));
                    RaiseSettingChanged(nameof(LegacySkinCompatibilityMode), value);
                }
            }
        }

        public float SkinNoteHeightScale
        {
            get => Settings.SkinNoteHeightScale;
            set
            {
                if (Settings.SkinNoteHeightScale != value)
                {
                    Settings.SkinNoteHeightScale = value;
                    RaisePropertyChanged(nameof(SkinNoteHeightScale));
                    RaiseSettingChanged(nameof(SkinNoteHeightScale), value);
                }
            }
        }

        public float SkinReceptorHeightScale
        {
            get => Settings.SkinReceptorHeightScale;
            set
            {
                if (Settings.SkinReceptorHeightScale != value)
                {
                    Settings.SkinReceptorHeightScale = value;
                    RaisePropertyChanged(nameof(SkinReceptorHeightScale));
                    RaiseSettingChanged(nameof(SkinReceptorHeightScale), value);
                }
            }
        }

        public bool OverrideMapHitsounds
        {
            get => Settings.OverrideMapHitsounds;
            set
            {
                if (Settings.OverrideMapHitsounds != value)
                {
                    Settings.OverrideMapHitsounds = value;
                    RaisePropertyChanged(nameof(OverrideMapHitsounds));
                    RaiseSettingChanged(nameof(OverrideMapHitsounds), value);
                }
            }
        }

        public bool UseSkinHitsounds
        {
            get => Settings.UseSkinHitsounds;
            set
            {
                if (Settings.UseSkinHitsounds != value)
                {
                    Settings.UseSkinHitsounds = value;
                    RaisePropertyChanged(nameof(UseSkinHitsounds));
                    RaiseSettingChanged(nameof(UseSkinHitsounds), value);
                }
            }
        }

        public bool ShowHitLighting
        {
            get => Settings.ShowHitLighting;
            set
            {
                if (Settings.ShowHitLighting != value)
                {
                    Settings.ShowHitLighting = value;
                    RaisePropertyChanged(nameof(ShowHitLighting));
                    RaiseSettingChanged(nameof(ShowHitLighting), value);
                }
            }
        }

        /// <summary>
        /// Saves settings via the unified GameSettings.
        /// </summary>
        public void SaveSettings()
        {
            Settings.Save();
        }

        /// <summary>
        /// Loads settings via the unified GameSettings.
        /// </summary>
        public void LoadSettings()
        {
            _settings = GameSettings.Load();
            _settings.Validate();
            Console.WriteLine("[SettingsService] Settings loaded and validated via GameSettings");
        }
    }
}
