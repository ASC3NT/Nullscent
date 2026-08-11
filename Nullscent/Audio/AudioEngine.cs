

#nullable enable

using System;
using System.IO;
using ManagedBass;

namespace Nullscent.Audio
{
    /// <summary>
    /// Motor de audio usando BASS.NET (ManagedBass) para reproducción sample-accurate.
    /// Proporciona el reloj maestro del juego basado en la posición del audio.
    /// Soporta cambio de velocidad de reproducción (rate) para práctica (0.5x - 1.5x).
    /// </summary>
    public class AudioEngine : IDisposable
    {
        private int _currentStream;
        private bool _isInitialized;
        private bool _isPlaying;
        private double _globalOffsetMs;
        private double _rateMultiplier = 1.0;
        private bool _settingsSubscribed = false;

        /// <summary>
        /// Posición actual de reproducción en milisegundos (master clock).
        /// Esta es la fuente autoritativa de tiempo para el gameplay.
        /// </summary>
        public double CurrentPositionMs { get; private set; }

        /// <summary>
        /// Offset global de audio en milisegundos (ajuste de calibración).
        /// Positivo = audio adelantado, negativo = audio atrasado.
        /// </summary>
        public double GlobalOffsetMs
        {
            get => _globalOffsetMs;
            set => _globalOffsetMs = value;
        }

        /// <summary>
        /// Multiplicador de velocidad de reproducción actual (0.5x - 1.5x).
        /// </summary>
        public double RateMultiplier
        {
            get => _rateMultiplier;
            set
            {
                _rateMultiplier = Math.Clamp(value, 0.5, 1.5);
                if (_currentStream != 0 && _isInitialized)
                    ApplyRate();
            }
        }

        /// <summary>
        /// Indica si el audio está actualmente reproduciéndose.
        /// </summary>
        public bool IsPlaying => _isPlaying && Bass.ChannelIsActive(_currentStream) == PlaybackState.Playing;

        /// <summary>
        /// Longitud total del stream actual en milisegundos.
        /// </summary>
        public double LengthMs
        {
            get
            {
                if (_currentStream == 0 || !_isInitialized) return 0.0;
                long lengthBytes = Bass.ChannelGetLength(_currentStream);
                return Bass.ChannelBytes2Seconds(_currentStream, lengthBytes) * 1000.0;
            }
        }

        /// <summary>
        /// Volumen global (0.0 - 1.0).
        /// </summary>
        public float Volume
        {
            get => Bass.GlobalStreamVolume / 10000f;
            set
            {
                int val = (int)(Math.Clamp(value, 0f, 1f) * 10000);
                Bass.GlobalStreamVolume = val;
                Bass.GlobalSampleVolume = val;
            }
        }

        ///<summary>
        ///Volumen del stream de musica actual (0.0 - 1.0).
        ///Solo afecta el track actual, no el volumen global.
        ///</summary>
        public float StreamVolume
        {
            get
            {
                if (_currentStream == 0) return 1.0f;
                //Bass.ChannelGetAttribute retorna bool (exito), el valor va en 'out float volume'
                if (Bass.ChannelGetAttribute(_currentStream, ChannelAttribute.Volume, out float volume))
                {
                    return volume;
                }
                return 1.0f;
            }
            set
            {
                if(_currentStream != 0 && _isInitialized)
                {
                    float clampedVolume = Math.Clamp(value, 0f, 1f);
                    //Bass.ChannelSetAttribute retorna bool (exito)
                    bool success = Bass.ChannelSetAttribute(_currentStream, ChannelAttribute.Volume, clampedVolume);
                    if (success)
                    {
                        Console.WriteLine($"[AudioEngine] Stream volume set to {clampedVolume:F2}");
                    }
                    else
                    {
                        Console.WriteLine($"[AudioEngine] Failed to set stream volume");
                    }
                }
            }
        }

        /// <summary>
        /// Inicializa BASS con el dispositivo de audio predeterminado.
        /// </summary>
        /// <param name="sampleRate">Tasa de muestreo (típicamente 44100)</param>
        /// <returns>True si la inicialización fue exitosa</returns>
        public bool Initialize(int sampleRate = 44100)
        {
            if (_isInitialized)
                return true;

            // Inicializar BASS con dispositivo predeterminado (-1)
            _isInitialized = Bass.Init(-1, sampleRate, DeviceInitFlags.Default, IntPtr.Zero);

            if (!_isInitialized)
            {
                var error = Bass.LastError;
                Console.WriteLine($"[AudioEngine] Failed to initialize BASS: {error}");
                return false;
            }

            // Volumen al 100% por defecto
            Bass.GlobalStreamVolume = 10000;
            Bass.GlobalSampleVolume = 10000;

            Console.WriteLine($"[AudioEngine] Initialized successfully at {sampleRate}Hz");

            // Subscribe to runtime settings changes
            if (!_settingsSubscribed)
            {
                Core.SettingsService.Instance.SettingChanged += OnSettingChanged;
                _settingsSubscribed = true;
            }
            return true;
        }

        /// <summary>
        /// Carga un archivo de audio para reproducción.
        /// </summary>
        /// <param name="audioPath">Ruta completa al archivo de audio</param>
        /// <returns>True si la carga fue exitosa</returns>
        public bool LoadTrack(string audioPath)
        {
            if (!_isInitialized)
            {
                Console.WriteLine("[AudioEngine] Cannot load track: BASS not initialized");
                return false;
            }

            if (!File.Exists(audioPath))
            {
                Console.WriteLine($"[AudioEngine] Audio file not found: {audioPath}");
                return false;
            }

            // Liberar stream anterior si existe
            if (_currentStream != 0)
            {
                Bass.StreamFree(_currentStream);
                _currentStream = 0;
            }

            // Crear stream con prescan para seeking preciso y float para mejor calidad
            _currentStream = Bass.CreateStream(audioPath, 0, 0, BassFlags.Prescan | BassFlags.Float);

            if (_currentStream == 0)
            {
                var error = Bass.LastError;
                Console.WriteLine($"[AudioEngine] Failed to load track: {error}");
                return false;
            }

            // Aplicar rate si no es 1.0x
            if (Math.Abs(_rateMultiplier - 1.0) > 0.001)
                ApplyRate();

            // Aplicar el volumen de música guardado al nuevo stream
            float musicVolume = Core.SettingsService.Instance.MusicVolume;
            StreamVolume = musicVolume;
            Console.WriteLine($"[AudioEngine] Applied music volume: {musicVolume:F2} to new stream");

            Console.WriteLine($"[AudioEngine] Loaded track: {Path.GetFileName(audioPath)} ({LengthMs:F0}ms)");
            return true;
        }

        /// <summary>
        /// Inicia la reproducción del audio actual.
        /// </summary>
        /// <param name="restart">Si es true, reinicia desde el inicio</param>
        /// <returns>True si la reproducción inició correctamente</returns>
        public bool Play(bool restart = false)
        {
            if (_currentStream == 0 || !_isInitialized)
                return false;

            bool success = Bass.ChannelPlay(_currentStream, restart);
            _isPlaying = success;

            if (!success)
            {
                var error = Bass.LastError;
                Console.WriteLine($"[AudioEngine] Failed to play: {error}");
            }

            return success;
        }

        /// <summary>
        /// Pausa la reproducción del audio.
        /// </summary>
        public void Pause()
        {
            if (_currentStream != 0 && _isInitialized)
            {
                Bass.ChannelPause(_currentStream);
                _isPlaying = false;
            }
        }

        /// <summary>
        /// Detiene completamente la reproducción y resetea la posición.
        /// </summary>
        public void Stop()
        {
            if (_currentStream != 0 && _isInitialized)
            {
                Bass.ChannelStop(_currentStream);
                _isPlaying = false;
                CurrentPositionMs = 0.0;
            }
        }

        /// <summary>
        /// Busca a una posición específica en el audio.
        /// </summary>
        /// <param name="positionMs">Posición en milisegundos</param>
        public void Seek(double positionMs)
        {
            if (_currentStream == 0 || !_isInitialized)
                return;

            double positionSeconds = positionMs / 1000.0;
            long positionBytes = Bass.ChannelSeconds2Bytes(_currentStream, positionSeconds);
            Bass.ChannelSetPosition(_currentStream, positionBytes);
            CurrentPositionMs = positionMs;
        }

        /// <summary>
        /// Actualiza la posición del audio (debe llamarse cada frame).
        /// Esta es la función que proporciona el master clock.
        /// </summary>
        /// <param name="audioLeadIn">Lead-in del beatmap en ms (típicamente 0)</param>
        public void Update(int audioLeadIn = 0)
        {
            if (_currentStream == 0 || !_isInitialized)
            {
                CurrentPositionMs = 0.0;
                return;
            }

            // Obtener posición exacta desde BASS
            long posBytes = Bass.ChannelGetPosition(_currentStream, PositionFlags.Bytes);
            double posSeconds = Bass.ChannelBytes2Seconds(_currentStream, posBytes);
            double posMs = posSeconds * 1000.0;

            // Aplicar offsets
            CurrentPositionMs = posMs + _globalOffsetMs + audioLeadIn;

            // Actualizar estado de reproducción
            _isPlaying = Bass.ChannelIsActive(_currentStream) == PlaybackState.Playing;
        }

        /// <summary>
        /// Aplica el multiplicador de rate actual al stream.
        /// Usa tempo stretching para mantener el pitch.
        /// </summary>
        private void ApplyRate()
        {
            if (_currentStream == 0 || !_isInitialized)
                return;

            // Al no disponer de bass_fx.dll ni del paquete ManagedBass.Fx en el cliente,
            // usamos ChannelAttribute.Frequency para alterar la velocidad de reproducción.
            // Esto variará el pitch (tono) de la música, igual que en osu! classic al activar DoubleTime/HalfTime.
            ChannelInfo info;
            if (Bass.ChannelGetInfo(_currentStream, out info))
            {
                float targetFrequency = (float)(info.Frequency * _rateMultiplier);
                bool success = Bass.ChannelSetAttribute(_currentStream, ChannelAttribute.Frequency, targetFrequency);
                if (success)
                {
                    Console.WriteLine($"[AudioEngine] Playback rate set to {_rateMultiplier:F2}x via Frequency ({targetFrequency:F0}Hz)");
                }
                else
                {
                    var error = Bass.LastError;
                    Console.WriteLine($"[AudioEngine] Failed to set frequency: {error}");
                }
            }
        }

        /// <summary>
        /// Libera todos los recursos de BASS.
        /// </summary>
        public void Dispose()
        {
            if (_currentStream != 0)
            {
                Bass.StreamFree(_currentStream);
                _currentStream = 0;
            }

            if (_isInitialized)
            {
                Bass.Free();
                _isInitialized = false;
            }

            _isPlaying = false;
            if (_settingsSubscribed)
            {
                Core.SettingsService.Instance.SettingChanged -= OnSettingChanged;
                _settingsSubscribed = false;
            }
            GC.SuppressFinalize(this);
        }

        ~AudioEngine()
        {
            Dispose();
        }

        private void OnSettingChanged(string key, object? value)
        {
            // React to settings changes that affect audio
            if (key == nameof(Core.SettingsService.GlobalOffsetMs) && value is int off)
            {
                GlobalOffsetMs = off;
            }

            // Volumen maestro
            if (key == nameof(Core.SettingsService.MasterVolume) && value is float masterVol)
            {
                Volume = masterVol;
                Console.WriteLine($"[AudioEngine] Master volume changed to {masterVol:F2} (0-1 scale)");
            }

            // Volumen de música - aplicar al stream actual
            if (key == nameof(Core.SettingsService.MusicVolume) && value is float musicVol)
            {
                StreamVolume = musicVol;
                Console.WriteLine($"[AudioEngine] Music volume changed to {musicVol:F2} (0-1 scale)");
            }

            // Volumen de hitsounds - se aplicará en HitSoundPlayer
            if (key == nameof(Core.SettingsService.HitsoundVolume) && value is float hitVol)
            {
                Console.WriteLine($"[AudioEngine] Hitsound volume changed to {hitVol:F2} (0-1 scale)");
            }

            // Offset de audio (backwards compatibility)
            if (key == nameof(Core.SettingsService.Volume) && value is float vol)
            {
                Volume = vol;
            }
        }
    }
}
