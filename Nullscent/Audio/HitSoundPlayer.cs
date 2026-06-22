#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using ManagedBass;

namespace Nullscent.Audio
{
    /// <summary>
    /// Reproduce hitsounds (sonidos de golpe) con latencia mínima usando BASS samples.
    /// Cachea samples en memoria para reproducción instantánea durante el gameplay.
    /// </summary>
    public class HitSoundPlayer : IDisposable
    {
        private readonly Dictionary<string, int> _sampleCache = new();
        private bool _isInitialized;
        private float _hitsoundVolume = 1.0f;

        /// <summary>
        /// Volumen de los hitsounds (0.0 - 1.0).
        /// </summary>
        public float HitsoundVolume
        {
            get => _hitsoundVolume;
            set => _hitsoundVolume = Math.Clamp(value, 0f, 1f);
        }

        /// <summary>
        /// Directorio base donde buscar archivos de hitsound (típicamente la carpeta del beatmap).
        /// </summary>
        public string BaseDirectory { get; set; } = string.Empty;

        /// <summary>
        /// Inicializa el reproductor de hitsounds.
        /// Debe llamarse después de que BASS esté inicializado.
        /// </summary>
        public void Initialize()
        {
            if (_isInitialized)
                return;

            _isInitialized = true;
            Console.WriteLine("[HitSoundPlayer] Initialized");

            // Apply volume from settings
            _hitsoundVolume = Core.SettingsService.Instance.Volume;

            // Subscribe to settings changes
            Core.SettingsService.Instance.SettingChanged += OnSettingChanged;
        }

        /// <summary>
        /// Carga un sample de hitsound en memoria.
        /// </summary>
        /// <param name="fileName">Nombre del archivo (sin ruta, se busca en BaseDirectory)</param>
        /// <returns>Handle del sample, o 0 si falla</returns>
        public int LoadSample(string fileName)
        {
            if (!_isInitialized)
            {
                Console.WriteLine("[HitSoundPlayer] Cannot load sample: not initialized");
                return 0;
            }

            // Usar caché si ya está cargado
            if (_sampleCache.TryGetValue(fileName, out int existingSample))
                return existingSample;

            // Construir ruta completa
            string fullPath = Path.Combine(BaseDirectory, fileName);

            if (!File.Exists(fullPath))
            {
                // Intentar con extensión .wav si no se especificó
                if (!fullPath.EndsWith(".wav", StringComparison.OrdinalIgnoreCase))
                {
                    fullPath += ".wav";
                    if (!File.Exists(fullPath))
                    {
                        Console.WriteLine($"[HitSoundPlayer] Sample not found: {fileName}");
                        return 0;
                    }
                }
                else
                {
                    Console.WriteLine($"[HitSoundPlayer] Sample not found: {fileName}");
                    return 0;
                }
            }

            // Cargar sample en memoria para playback de baja latencia
            int sample = Bass.SampleLoad(fullPath, 0, 0, 16, BassFlags.SampleOverrideLongestPlaying);

            if (sample == 0)
            {
                var error = Bass.LastError;
                Console.WriteLine($"[HitSoundPlayer] Failed to load sample {fileName}: {error}");
                return 0;
            }

            _sampleCache[fileName] = sample;
            Console.WriteLine($"[HitSoundPlayer] Loaded sample: {fileName}");
            return sample;
        }

        /// <summary>
        /// Reproduce un hitsound por nombre de archivo.
        /// </summary>
        /// <param name="fileName">Nombre del archivo de hitsound</param>
        /// <param name="volume">Volumen override (0.0-1.0), o null para usar HitsoundVolume</param>
        public void Play(string fileName, float? volume = null)
        {
            if (!_isInitialized)
                return;

            int sample = LoadSample(fileName);
            if (sample == 0)
                return;

            PlaySample(sample, volume);
        }

        /// <summary>
        /// Reproduce un hitsound basado en el tipo de hitsound de osu!.
        /// </summary>
        /// <param name="hitsoundType">Tipo de hitsound (bit flags): 0=Normal, 2=Whistle, 4=Finish, 8=Clap</param>
        /// <param name="customSampleFile">Archivo de sample customizado (opcional)</param>
        /// <param name="volume">Volumen override (0.0-1.0), o null para usar HitsoundVolume</param>
        public void PlayHitsound(int hitsoundType, string? customSampleFile = null, float? volume = null)
        {
            if (!_isInitialized)
                return;

            // Si hay custom sample, usarlo
            if (!string.IsNullOrWhiteSpace(customSampleFile))
            {
                Play(customSampleFile, volume);
                return;
            }

            // Determinar qué hitsound(s) reproducir según los flags
            // 0 o 1 = normal (sin flag adicional)
            // 2 = whistle
            // 4 = finish
            // 8 = clap
            // Pueden combinarse (ej: 6 = whistle + finish)

            bool hasWhistle = (hitsoundType & 2) != 0;
            bool hasFinish = (hitsoundType & 4) != 0;
            bool hasClap = (hitsoundType & 8) != 0;

            // Reproducir normal siempre (o si no hay flags)
            Play("normal-hitnormal", volume);

            // Reproducir adicionales si están presentes
            if (hasWhistle)
                Play("normal-hitwhistle", volume);

            if (hasFinish)
                Play("normal-hitfinish", volume);

            if (hasClap)
                Play("normal-hitclap", volume);
        }

        /// <summary>
        /// Reproduce un sample ya cargado por su handle.
        /// </summary>
        /// <param name="sample">Handle del sample</param>
        /// <param name="volume">Volumen override (0.0-1.0), o null para usar HitsoundVolume</param>
        private void PlaySample(int sample, float? volume = null)
        {
            if (sample == 0)
                return;

            // Obtener channel del sample
            int channel = Bass.SampleGetChannel(sample, BassFlags.SampleChannelStream);
            if (channel == 0)
            {
                var error = Bass.LastError;
                Console.WriteLine($"[HitSoundPlayer] Failed to get sample channel: {error}");
                return;
            }

            // Aplicar volumen
            float finalVolume = volume ?? _hitsoundVolume;
            Bass.ChannelSetAttribute(channel, ChannelAttribute.Volume, finalVolume);

            // Reproducir
            Bass.ChannelPlay(channel, false);
        }

        /// <summary>
        /// Precarga los hitsounds estándar de osu!mania.
        /// </summary>
        public void PreloadDefaultHitsounds()
        {
            LoadSample("normal-hitnormal.wav");
            LoadSample("normal-hitwhistle.wav");
            LoadSample("normal-hitfinish.wav");
            LoadSample("normal-hitclap.wav");
        }

        /// <summary>
        /// Limpia todos los samples cargados.
        /// </summary>
        public void ClearCache()
        {
            foreach (var sample in _sampleCache.Values)
            {
                if (sample != 0)
                    Bass.SampleFree(sample);
            }

            _sampleCache.Clear();
            Console.WriteLine("[HitSoundPlayer] Cache cleared");
        }

        /// <summary>
        /// Libera todos los recursos.
        /// </summary>
        public void Dispose()
        {
            ClearCache();
            _isInitialized = false;
            Core.SettingsService.Instance.SettingChanged -= OnSettingChanged;
            GC.SuppressFinalize(this);
        }

        ~HitSoundPlayer()
        {
            Dispose();
        }

        private void OnSettingChanged(string key, object? value)
        {
            if (key == nameof(Core.SettingsService.Volume) && value is float vol)
            {
                HitsoundVolume = vol;
            }
        }
    }
}
