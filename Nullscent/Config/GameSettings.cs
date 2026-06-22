#nullable enable

using System;
using System.IO;
using System.Text.Json;

namespace Nullscent.Config
{
    /// <summary>
    /// Configuración global del juego.
    /// Almacena preferencias del usuario, rutas, ajustes de audio/video/gameplay.
    /// Se serializa/deserializa desde un archivo JSON.
    /// </summary>
    public class GameSettings
    {
        // === RUTAS ===

        /// <summary>
        /// Directorio donde se encuentran las canciones/beatmaps.
        /// Default: ./Songs/
        /// </summary>
        public string SongsDirectory { get; set; } = "./Songs/";

        /// <summary>
        /// Directorio donde se encuentra la skin actual.
        /// Default: ./Skin/
        /// </summary>
        public string SkinDirectory { get; set; } = "./Skin/";

        // === AUDIO ===

        /// <summary>
        /// Volumen global del juego (0.0 - 1.0).
        /// </summary>
        public float MasterVolume { get; set; } = 1.0f;

        /// <summary>
        /// Volumen de la música (0.0 - 1.0).
        /// </summary>
        public float MusicVolume { get; set; } = 0.8f;

        /// <summary>
        /// Volumen de los hitsounds (0.0 - 1.0).
        /// </summary>
        public float HitsoundVolume { get; set; } = 0.6f;

        /// <summary>
        /// Offset global de audio en milisegundos.
        /// Positivo = audio adelantado (notas aparecen antes).
        /// Negativo = audio atrasado (notas aparecen después).
        /// </summary>
        public double GlobalAudioOffset { get; set; } = 0.0;

        // === GAMEPLAY ===

        /// <summary>
        /// Velocidad de scroll (1-40). Controla qué tan rápido bajan las notas.
        /// Más alto = notas más rápidas (menos tiempo visible).
        /// </summary>
        public int ScrollSpeed { get; set; } = 20;

        /// <summary>
        /// Posición del receptor (hit position) como porcentaje de la altura de pantalla (0.0 - 1.0).
        /// Default: 0.88 (88% desde arriba = cerca de la parte inferior).
        /// </summary>
        public float ReceptorPosition { get; set; } = 0.88f;

        /// <summary>
        /// Indica si el scroll es hacia abajo (true) o hacia arriba (false).
        /// Default: true (notas caen hacia abajo, estilo osu!mania).
        /// </summary>
        public bool DownScroll { get; set; } = true;

        /// <summary>
        /// Indica si el health drain está habilitado (modo hardcore).
        /// False = modo práctica (no puedes morir).
        /// </summary>
        public bool HealthDrainEnabled { get; set; } = false;

        // === VIDEO ===

        /// <summary>
        /// Ancho de ventana en píxeles.
        /// </summary>
        public int WindowWidth { get; set; } = 1280;

        /// <summary>
        /// Alto de ventana en píxeles.
        /// </summary>
        public int WindowHeight { get; set; } = 720;

        /// <summary>
        /// Indica si la ventana está en modo fullscreen.
        /// </summary>
        public bool Fullscreen { get; set; } = false;

        /// <summary>
        /// Indica si VSync está habilitado.
        /// </summary>
        public bool VSync { get; set; } = true;

        /// <summary>
        /// Límite de FPS (0 = sin límite).
        /// </summary>
        public int FPSLimit { get; set; } = 0;

        /// <summary>
        /// Escala de UI (0.5 - 2.0).
        /// </summary>
        public float UIScale { get; set; } = 1.0f;

        /// <summary>
        /// Mostrar contador de FPS.
        /// </summary>
        public bool ShowFPS { get; set; } = false;

        /// <summary>
        /// Dim del fondo (0.0 - 1.0, 0 = sin dim, 1 = totalmente oscuro).
        /// </summary>
        public float BackgroundDim { get; set; } = 0.7f;

        /// <summary>
        /// Mostrar approach circles.
        /// </summary>
        public bool ShowApproachCircles { get; set; } = true;

        /// <summary>
        /// Mostrar lighting de teclas.
        /// </summary>
        public bool ShowHitLighting { get; set; } = true;

        /// <summary>
        /// Mostrar divisores de lanes.
        /// </summary>
        public bool ShowLaneDividers { get; set; } = true;

        /// <summary>
        /// Multiplicador de velocidad de notas (0.5 - 2.0).
        /// </summary>
        public float NoteSpeedMultiplier { get; set; } = 1.0f;

        /// <summary>
        /// Ignorar samples del beatmap (usar skin siempre).
        /// </summary>
        public bool IgnoreBeatmapSamples { get; set; } = false;

        /// <summary>
        /// Preview de audio en song select.
        /// </summary>
        public bool AudioPreview { get; set; } = true;

        /// <summary>
        /// Sensibilidad del mouse.
        /// </summary>
        public float MouseSensitivity { get; set; } = 1.0f;

        /// <summary>
        /// Usar raw input.
        /// </summary>
        public bool RawInput { get; set; } = false;

        /// <summary>
        /// Deshabilitar scroll del mouse en gameplay.
        /// </summary>
        public bool DisableMouseWheelInGameplay { get; set; } = true;

        /// <summary>
        /// Usar hitsounds de la skin.
        /// </summary>
        public bool UseSkinHitsounds { get; set; } = true;

        /// <summary>
        /// Usar skin del beatmap si está disponible.
        /// </summary>
        public bool UseBeatmapSkin { get; set; } = false;

        /// <summary>
        /// Tamaño del cursor (0.5 - 2.0).
        /// </summary>
        public float CursorSize { get; set; } = 1.0f;

        // === MÉTODOS ===

        /// <summary>
        /// Ruta al archivo de configuración.
        /// </summary>
        private static readonly string ConfigFilePath = "settings.json";

        /// <summary>
        /// Guarda la configuración actual en un archivo JSON.
        /// </summary>
        public void Save()
        {
            try
            {
                var options = new JsonSerializerOptions 
                { 
                    WriteIndented = true 
                };

                string json = JsonSerializer.Serialize(this, options);
                File.WriteAllText(ConfigFilePath, json);

                Console.WriteLine($"[GameSettings] Settings saved to {ConfigFilePath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GameSettings] Failed to save settings: {ex.Message}");
            }
        }

        /// <summary>
        /// Carga la configuración desde un archivo JSON.
        /// Si no existe, crea uno nuevo con valores predeterminados.
        /// </summary>
        /// <returns>Instancia de GameSettings cargada o nueva</returns>
        public static GameSettings Load()
        {
            try
            {
                if (File.Exists(ConfigFilePath))
                {
                    string json = File.ReadAllText(ConfigFilePath);
                    var settings = JsonSerializer.Deserialize<GameSettings>(json);

                    if (settings != null)
                    {
                        Console.WriteLine($"[GameSettings] Settings loaded from {ConfigFilePath}");
                        return settings;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GameSettings] Failed to load settings: {ex.Message}");
            }

            // Si no existe o falla, crear configuración por defecto y guardarla
            Console.WriteLine("[GameSettings] Creating default settings");
            var defaultSettings = new GameSettings();
            defaultSettings.Save();
            return defaultSettings;
        }

        /// <summary>
        /// Valida y clampea valores de configuración a rangos válidos.
        /// </summary>
        public void Validate()
        {
            MasterVolume = Math.Clamp(MasterVolume, 0f, 1f);
            MusicVolume = Math.Clamp(MusicVolume, 0f, 1f);
            HitsoundVolume = Math.Clamp(HitsoundVolume, 0f, 1f);
            ScrollSpeed = Math.Clamp(ScrollSpeed, 1, 40);
            ReceptorPosition = Math.Clamp(ReceptorPosition, 0.1f, 0.95f);
            WindowWidth = Math.Max(WindowWidth, 800);
            WindowHeight = Math.Max(WindowHeight, 600);
            UIScale = Math.Clamp(UIScale, 0.5f, 2.0f);
            BackgroundDim = Math.Clamp(BackgroundDim, 0f, 1f);
            NoteSpeedMultiplier = Math.Clamp(NoteSpeedMultiplier, 0.5f, 2.0f);
            MouseSensitivity = Math.Clamp(MouseSensitivity, 0.1f, 3.0f);
            CursorSize = Math.Clamp(CursorSize, 0.5f, 2.0f);
        }

        /// <summary>
        /// Resetea todas las configuraciones a sus valores por defecto.
        /// </summary>
        public void ResetToDefaults()
        {
            var defaults = new GameSettings();

            // Copiar todos los valores desde defaults a esta instancia
            MasterVolume = defaults.MasterVolume;
            MusicVolume = defaults.MusicVolume;
            HitsoundVolume = defaults.HitsoundVolume;
            GlobalAudioOffset = defaults.GlobalAudioOffset;
            ScrollSpeed = defaults.ScrollSpeed;
            ReceptorPosition = defaults.ReceptorPosition;
            DownScroll = defaults.DownScroll;
            HealthDrainEnabled = defaults.HealthDrainEnabled;
            WindowWidth = defaults.WindowWidth;
            WindowHeight = defaults.WindowHeight;
            Fullscreen = defaults.Fullscreen;
            VSync = defaults.VSync;
            FPSLimit = defaults.FPSLimit;
            UIScale = defaults.UIScale;
            ShowFPS = defaults.ShowFPS;
            BackgroundDim = defaults.BackgroundDim;
            ShowApproachCircles = defaults.ShowApproachCircles;
            ShowHitLighting = defaults.ShowHitLighting;
            ShowLaneDividers = defaults.ShowLaneDividers;
            NoteSpeedMultiplier = defaults.NoteSpeedMultiplier;
            IgnoreBeatmapSamples = defaults.IgnoreBeatmapSamples;
            AudioPreview = defaults.AudioPreview;
            MouseSensitivity = defaults.MouseSensitivity;
            RawInput = defaults.RawInput;
            DisableMouseWheelInGameplay = defaults.DisableMouseWheelInGameplay;
            UseSkinHitsounds = defaults.UseSkinHitsounds;
            UseBeatmapSkin = defaults.UseBeatmapSkin;
            CursorSize = defaults.CursorSize;

            Console.WriteLine("[GameSettings] Reset to defaults");
        }
    }
}
