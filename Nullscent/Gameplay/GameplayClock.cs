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
        }

        public void Start()
        {
            _isPlaying = true;
            _rateMultiplier = _audioEngine.RateMultiplier;
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

            if (_currentTimeMs < 0)
            {
                // Virtual Lead-In phase: advance using frame delta scaled by playback rate
                _currentTimeMs += deltaMs * _rateMultiplier;

                if (_currentTimeMs >= 0)
                {
                    // Transition to authoritative audio playback
                    _audioEngine.Play();
                    _audioEngine.Seek(0);
                    _hasStartedAudio = true;
                    _currentTimeMs = _audioEngine.CurrentPositionMs;
                }
            }
            else
            {
                // Authoritative audio engine playback position
                _audioEngine.Update();
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
