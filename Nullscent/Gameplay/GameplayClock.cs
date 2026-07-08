#nullable enable

using Nullscent.Audio;

namespace Nullscent.Gameplay
{
    /// <summary>
    /// Master clock for Nullscent gameplay.
    /// Encapsulates the transition between the virtual CPU-based lead-in phase (negative time)
    /// and the authoritative, BASS.NET audio engine-based phase (positive time).
    /// </summary>
    public class GameplayClock
    {
        private readonly AudioEngine _audioEngine;
        private double _currentTimeMs;
        private double _rateMultiplier = 1.0;
        private bool _isPlaying;
        private bool _hasStartedAudio;
        private bool _allowBackwardSeek;
        private int _updateCount = 0;

        public double CurrentTime => _currentTimeMs;
        public double Rate => _rateMultiplier;
        public bool IsPlaying => _isPlaying;

        public GameplayClock(AudioEngine audioEngine, double leadInTimeMs)
        {
            _audioEngine = audioEngine;
            _currentTimeMs = -leadInTimeMs;
            _isPlaying = false;
            _hasStartedAudio = false;
            _allowBackwardSeek = false;
            System.Console.WriteLine($"[GameplayClock] Created. LeadIn={leadInTimeMs}");
        }

        public void Start()
        {
            _isPlaying = true;
            _rateMultiplier = _audioEngine.RateMultiplier;
            System.Console.WriteLine($"[GameplayClock] Start() LeadIn={_currentTimeMs}");
        }

        public void Pause()
        {
            _isPlaying = false;
            if (_hasStartedAudio)
            {
                _audioEngine.Pause();
            }
        }

        public void Resume()
        {
            _isPlaying = true;
            if (_hasStartedAudio)
            {
                _audioEngine.Play();
            }
        }

        public void Seek(double positionMs)
        {
            _currentTimeMs = positionMs;
            _allowBackwardSeek = true;
            if (positionMs < 0)
            {
                _hasStartedAudio = false;
                _audioEngine.Seek(0);
                _audioEngine.Pause();
            }
            else
            {
                _hasStartedAudio = true;
                _audioEngine.Seek(positionMs);
                if (_isPlaying)
                {
                    _audioEngine.Play();
                }
            }
        }

        public void Update(double deltaMs)
        {
            if (!_isPlaying)
                return;

            _rateMultiplier = _audioEngine.RateMultiplier;
            _updateCount++;

            if (_updateCount % 120 == 0)
            {
                System.Console.WriteLine(
                    $"[GameplayClock] " +
                    $"CurrentTime={_currentTimeMs:F1} | " +
                    $"IsPlaying={_isPlaying} | " +
                    $"HasStartedAudio={_hasStartedAudio} | " +
                    $"Rate={_rateMultiplier:F2} | " +
                    $"AudioPlaying={_audioEngine.IsPlaying} | " +
                    $"AudioPosition={_audioEngine.CurrentPositionMs:F1}");
            }

            // 1. Virtual Lead-In phase: advance using frame delta scaled by playback rate
            if (_currentTimeMs < 0)
            {
                _currentTimeMs += deltaMs * _rateMultiplier;
            }

            // 2. Transition or authoritative audio engine phase (unified condition)
            if (_currentTimeMs >= 0)
            {
                if (!_hasStartedAudio)
                {
                    System.Console.WriteLine("[GameplayClock] Transition -> Audio phase");
                    System.Console.WriteLine("[GameplayClock] Calling AudioEngine.Play()");
                    _audioEngine.Play();
                    System.Console.WriteLine($"[GameplayClock] After Play() IsPlaying={_audioEngine.IsPlaying}");
                    _audioEngine.Seek(_currentTimeMs);
                    _hasStartedAudio = true;
                }
                else
                {
                    _audioEngine.Update();
                }

                // Check if the audio has ended or is not playing near the end of the song (generous threshold of 2 seconds to avoid BASS buffer congelation)
                bool audioFinished = _hasStartedAudio && !_audioEngine.IsPlaying && _currentTimeMs >= _audioEngine.LengthMs - 2000;

                if (audioFinished)
                {
                    // CPU-based fallback timing at the end of the song so the clock keeps ticking
                    _currentTimeMs += deltaMs * _rateMultiplier;
                }
                else
                {
                    double newTime = _audioEngine.CurrentPositionMs;

                    // Enforce clock monotonicity (prevent backward time jitter during normal playback)
                    if (_allowBackwardSeek || newTime >= _currentTimeMs)
                    {
                        _currentTimeMs = newTime;
                        _allowBackwardSeek = false;
                    }
                }
            }
        }
    }
}
