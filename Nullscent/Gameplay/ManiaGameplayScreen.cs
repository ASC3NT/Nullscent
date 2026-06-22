#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Nullscent.Audio;
using Nullscent.Beatmap;
using Nullscent.Config;
using Nullscent.Core;
using Nullscent.Rulesets.Mania.Beatmaps;
using Nullscent.Rulesets.Mania.Configuration;
using Nullscent.Rulesets.Mania.Judgements;
using Nullscent.Rulesets.Mania.Mods;
using Nullscent.Rulesets.Mania.Scoring;
using Nullscent.Rulesets.Mania.UI;
using Nullscent.UI;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Nullscent.Gameplay
{
    /// <summary>
    /// Modern mania gameplay screen.
    /// Completely rewritten using new architecture inspired by ppy/osu.
    /// </summary>
    public class ManiaGameplayScreen : IGameScreen
    {
        private readonly Game1 _game;
        private readonly GraphicsDevice _graphicsDevice;
        private readonly SpriteBatch _spriteBatch;
        private readonly GameStateManager _stateManager;
        private readonly AudioEngine _audioEngine;
        private readonly InputManager _inputManager;
        private readonly HitSoundPlayer _hitSoundPlayer;
        private readonly GameSettings _settings;
        private readonly ManiaConfig _maniaConfig;

        private Beatmap.Beatmap? _originalBeatmap;
        private ManiaBeatmap? _maniaBeatmap;
        private ManiaPlayfield? _playfield;
        private ManiaScoreProcessor? _scoreProcessor;
        private ManiaHUD? _hud;
        private ManiaPauseOverlay? _pauseOverlay;
        private TrueTypeFontRenderer? _fontRenderer;
        private HitErrorBar? _hitErrorBar;

        private readonly List<ManiaMod> _activeMods = new();
        private bool _isPaused;
        private bool _isInitialized;
        private bool _wasEscapePressed;
        private bool _hasStarted;
        private double _currentTime;

        // Input tracking
        private KeyboardState _previousKeyboardState;
        private readonly Dictionary<int, Keys> _columnBindings = new();

        public ManiaGameplayScreen(
            Game1 game,
            GraphicsDevice graphicsDevice,
            SpriteBatch spriteBatch,
            GameStateManager stateManager,
            AudioEngine audioEngine,
            InputManager inputManager,
            HitSoundPlayer hitSoundPlayer,
            GameSettings settings,
            Beatmap.Beatmap beatmap,
            List<ManiaMod>? mods = null)
        {
            _game = game;
            _graphicsDevice = graphicsDevice;
            _spriteBatch = spriteBatch;
            _stateManager = stateManager;
            _audioEngine = audioEngine;
            _inputManager = inputManager;
            _hitSoundPlayer = hitSoundPlayer;
            _settings = settings;
            _originalBeatmap = beatmap;

            // Load mania config
            _maniaConfig = LoadManiaConfig();

            // Set mods
            if (mods != null)
                _activeMods.AddRange(mods);

            Console.WriteLine($"[ManiaGameplayScreen] Initialized with {_activeMods.Count} mods");
        }

        public void Initialize()
        {
            if (_originalBeatmap == null)
            {
                Console.WriteLine("[ManiaGameplayScreen] Error: No beatmap loaded");
                return;
            }

            Console.WriteLine($"[ManiaGameplayScreen] Initializing: {_originalBeatmap.Metadata.Title} [{_originalBeatmap.Metadata.Version}]");
            Console.WriteLine($"[ManiaGameplayScreen] Original beatmap has {_originalBeatmap.HitObjects.Count} HitObjects");
            Console.WriteLine($"[ManiaGameplayScreen] KeyCount: {_originalBeatmap.KeyCount}");

            // Convert beatmap to mania format
            var converter = new ManiaBeatmapConverter(_originalBeatmap);
            _maniaBeatmap = converter.Convert();

            Console.WriteLine($"[ManiaGameplayScreen] Converted to {_maniaBeatmap.HitObjects.Count} mania objects");
            Console.WriteLine($"[ManiaGameplayScreen] Notes: {_maniaBeatmap.Notes.Count()}, HoldNotes: {_maniaBeatmap.HoldNotes.Count()}");

            // Apply mods
            ApplyMods();

            // Initialize score processor
            int totalNotes = _maniaBeatmap.Notes.Count();
            int totalLongNotes = _maniaBeatmap.HoldNotes.Count();
            _scoreProcessor = new ManiaScoreProcessor(
                totalNotes + totalLongNotes,
                totalLongNotes,
                _originalBeatmap.HPDrainRate,
                _settings.HealthDrainEnabled,
                isScoreV2: true // enable ScoreV2 semantics by default for LN parity
            );

            // Initialize playfield
            _playfield = new ManiaPlayfield(
                _graphicsDevice,
                _spriteBatch,
                _maniaBeatmap.KeyCount,
                _game.WindowWidth,
                _game.WindowHeight,
                _game.FontRenderer, // Pass font renderer for judgement text
                _originalBeatmap.OverallDifficulty // Pass OD explicitly for LN tail windows
            );

            // Add hit objects to playfield
            Console.WriteLine($"[ManiaGameplayScreen] Adding {_maniaBeatmap.HitObjects.Count} objects to playfield");
            int addedCount = 0;
            foreach (var hitObject in _maniaBeatmap.HitObjects)
            {
                _playfield.AddHitObject(hitObject);
                addedCount++;
            }
            Console.WriteLine($"[ManiaGameplayScreen] Successfully added {addedCount} objects to playfield");

            // Initialize HUD
            _fontRenderer = _game.FontRenderer;
            _hud = new ManiaHUD(
                _graphicsDevice,
                _spriteBatch,
                _fontRenderer,
                _maniaConfig
            );

            // Initialize pause overlay
            _pauseOverlay = new ManiaPauseOverlay(
                _graphicsDevice,
                _spriteBatch,
                _audioEngine,
                _fontRenderer
            );

            // Initialize HitErrorBar if enabled in settings
            if (Core.SettingsService.Instance.ShowHitErrorBar && _playfield?.JudgementDisplayManager != null)
            {
                // Create hit error bar with default OD
                _hitErrorBar = new HitErrorBar(
                    x: _game.WindowWidth / 2 - 150,
                    y: _game.WindowHeight / 2 + 100,
                    width: 300,
                    height: 50,
                    hitWindows: new Rulesets.Mania.Judgements.ManiaHitWindows(_originalBeatmap?.OverallDifficulty ?? 5.0)
                );

                // Subscribe to judgement events
                _playfield.JudgementDisplayManager.JudgementOccurred += OnJudgementOccurred;
            }

            // Setup column bindings
            SetupColumnBindings(_maniaBeatmap.KeyCount);

            // Setup audio - use the beatmap's directory, not root Songs directory
            string audioPath = System.IO.Path.Combine(_originalBeatmap.Directory, _originalBeatmap.Metadata.AudioFilename ?? "");
            Console.WriteLine($"[ManiaGameplayScreen] Looking for audio at: {audioPath}");

            if (System.IO.File.Exists(audioPath))
            {
                Console.WriteLine($"[ManiaGameplayScreen] Audio file found, loading...");
                _audioEngine.LoadTrack(audioPath);
                // Apply user-configurable global offset from SettingsService if present
                _audioEngine.GlobalOffsetMs = Core.SettingsService.Instance.GlobalOffsetMs;
                _audioEngine.Volume = Core.SettingsService.Instance.Volume;

                // Apply rate mods
                ApplyRateMods();
                Console.WriteLine($"[ManiaGameplayScreen] Audio loaded successfully");
            }
            else
            {
                Console.WriteLine($"[ManiaGameplayScreen] ERROR: Audio file not found: {audioPath}");
                Console.WriteLine($"[ManiaGameplayScreen] Beatmap directory: {_originalBeatmap.Directory}");
                Console.WriteLine($"[ManiaGameplayScreen] Audio filename from metadata: {_originalBeatmap.Metadata.AudioFilename}");
            }

            _previousKeyboardState = Keyboard.GetState();

            // Subscribe to runtime settings changes
            Core.SettingsService.Instance.SettingChanged += OnSettingChanged; // persisted subscription
            _isInitialized = true;

            Console.WriteLine($"[ManiaGameplayScreen] Initialization complete: {_maniaBeatmap.KeyCount}K, {_maniaBeatmap.TotalHitObjects} objects");
        }

        private void ApplyMods()
        {
            if (_maniaBeatmap == null) return;

            foreach (var mod in _activeMods)
            {
                mod.Apply();

                // Apply specific mod effects
                if (mod is ManiaModRandom)
                {
                    var converter = new ManiaBeatmapConverter(_originalBeatmap!);
                    converter.ApplyRandomization(_maniaBeatmap.HitObjects);
                    Console.WriteLine("[ManiaGameplayScreen] Applied Random mod");
                }
                else if (mod is ManiaModMirror)
                {
                    var converter = new ManiaBeatmapConverter(_originalBeatmap!);
                    converter.ApplyMirror(_maniaBeatmap.HitObjects);
                    Console.WriteLine("[ManiaGameplayScreen] Applied Mirror mod");
                }
            }
        }

        private void ApplyRateMods()
        {
            double rate = 1.0;

            var dtMod = _activeMods.OfType<ManiaModDoubleTime>().FirstOrDefault();
            var htMod = _activeMods.OfType<ManiaModHalfTime>().FirstOrDefault();

            if (dtMod != null)
                rate = dtMod.SpeedMultiplier;
            else if (htMod != null)
                rate = htMod.SpeedMultiplier;

            _audioEngine.RateMultiplier = rate;
            Console.WriteLine($"[ManiaGameplayScreen] Audio rate: {rate}x");
        }

        private void SetupColumnBindings(int keyCount)
        {
            // Default bindings based on key count
            // Similar to osu!mania defaults
            switch (keyCount)
            {
                case 1:
                    _columnBindings[0] = Keys.Space;
                    break;
                case 2:
                    _columnBindings[0] = Keys.D;
                    _columnBindings[1] = Keys.K;
                    break;
                case 3:
                    _columnBindings[0] = Keys.D;
                    _columnBindings[1] = Keys.Space;
                    _columnBindings[2] = Keys.K;
                    break;
                case 4:
                    _columnBindings[0] = Keys.D;
                    _columnBindings[1] = Keys.F;
                    _columnBindings[2] = Keys.J;
                    _columnBindings[3] = Keys.K;
                    break;
                case 5:
                    _columnBindings[0] = Keys.D;
                    _columnBindings[1] = Keys.F;
                    _columnBindings[2] = Keys.Space;
                    _columnBindings[3] = Keys.J;
                    _columnBindings[4] = Keys.K;
                    break;
                case 6:
                    _columnBindings[0] = Keys.S;
                    _columnBindings[1] = Keys.D;
                    _columnBindings[2] = Keys.F;
                    _columnBindings[3] = Keys.J;
                    _columnBindings[4] = Keys.K;
                    _columnBindings[5] = Keys.L;
                    break;
                case 7:
                    _columnBindings[0] = Keys.S;
                    _columnBindings[1] = Keys.D;
                    _columnBindings[2] = Keys.F;
                    _columnBindings[3] = Keys.Space;
                    _columnBindings[4] = Keys.J;
                    _columnBindings[5] = Keys.K;
                    _columnBindings[6] = Keys.L;
                    break;
                case 8:
                    _columnBindings[0] = Keys.A;
                    _columnBindings[1] = Keys.S;
                    _columnBindings[2] = Keys.D;
                    _columnBindings[3] = Keys.F;
                    _columnBindings[4] = Keys.J;
                    _columnBindings[5] = Keys.K;
                    _columnBindings[6] = Keys.L;
                    _columnBindings[7] = Keys.OemSemicolon;
                    break;
                default:
                    // For 9K, 10K etc, use generic bindings
                    for (int i = 0; i < Math.Min(keyCount, 10); i++)
                    {
                        _columnBindings[i] = (Keys)((int)Keys.D1 + i);
                    }
                    break;
            }

            Console.WriteLine($"[ManiaGameplayScreen] Column bindings set for {keyCount}K");
        }

        public void Update(GameTime gameTime)
        {
            if (!_isInitialized || _maniaBeatmap == null || _playfield == null || _scoreProcessor == null)
                return;

            // Update audio engine
            _audioEngine.Update();
            _currentTime = _audioEngine.CurrentPositionMs;

            // Apply any runtime-updated scroll speed from SettingsService
            // Note: SettingsService exposes ScrollSpeed as double
            double runtimeScrollSpeed = Core.SettingsService.Instance.ScrollSpeed;
            _maniaConfig.ScrollSpeed = runtimeScrollSpeed;

            // Handle pause
            var keyboardState = Keyboard.GetState();
            bool escapePressed = keyboardState.IsKeyDown(Keys.Escape);

            if (escapePressed && !_wasEscapePressed && !_isPaused)
            {
                Pause();
            }

            _wasEscapePressed = escapePressed;

            if (_isPaused && _pauseOverlay != null)
            {
                _pauseOverlay.Update(gameTime);

                // Check pause menu actions
                if (_pauseOverlay.ShouldResume)
                {
                    Resume();
                }
                else if (_pauseOverlay.ShouldRetry)
                {
                    Retry();
                    return;
                }
                else if (_pauseOverlay.ShouldQuit)
                {
                    QuitToSongSelect();
                    return;
                }

                _previousKeyboardState = keyboardState;
                return;
            }

            // Start audio if not started
            if (!_hasStarted && _currentTime < 100)
            {
                _audioEngine.Play();
                _hasStarted = true;
                Console.WriteLine("[ManiaGameplayScreen] Audio started");
            }

            // Handle input
            HandleInput(keyboardState);

            // Calculate scroll speed
            double scrollSpeed = (_game.WindowHeight / 1.5) * _maniaConfig.ScrollSpeed;

            // Update playfield with all necessary parameters
            _playfield.Update(_currentTime, _scoreProcessor, scrollSpeed, (float)_maniaConfig.ReceptorPosition, _maniaConfig.DownScroll);

            // Update health drain
            if (!_scoreProcessor.HasFailed)
            {
                _scoreProcessor.UpdateHealthDrain(gameTime.ElapsedGameTime.TotalSeconds);
            }

            // Update hit error bar
            _hitErrorBar?.Update(_currentTime);

            // Check for fail
            if (_scoreProcessor.HasFailed)
            {
                HandleFail();
            }

            // Check for completion
            bool allObjectsJudged = _scoreProcessor.TotalJudgements >= _maniaBeatmap.TotalHitObjects;
            bool audioFinished = !_audioEngine.IsPlaying && _currentTime >= _audioEngine.LengthMs - 1000; // -1s buffer

            if (audioFinished && allObjectsJudged && !_isPaused && _maniaBeatmap.TotalHitObjects > 0)
            {
                TransitionToResults();
            }

            _previousKeyboardState = keyboardState;
        }

        private void HandleInput(KeyboardState keyboardState)
        {
            if (_playfield == null || _scoreProcessor == null) return;

            // Check each column binding
            foreach (var (column, key) in _columnBindings)
            {
                bool wasPressed = _previousKeyboardState.IsKeyDown(key);
                bool isPressed = keyboardState.IsKeyDown(key);

                if (isPressed && !wasPressed)
                {
                    // Key pressed - use exact audio clock timestamp for replay-safe input
                    double timestamp = _audioEngine.CurrentPositionMs;
                    _playfield.HandleKeyPress(column, timestamp, _scoreProcessor);
                }
                else if (!isPressed && wasPressed)
                {
                    // Key released (needed for LN tail judgements) - use audio clock timestamp
                    double timestamp = _audioEngine.CurrentPositionMs;
                    _playfield.HandleKeyRelease(column, timestamp, _scoreProcessor);
                }
            }
        }

        public void Draw(GameTime gameTime)
        {
            if (!_isInitialized || _playfield == null || _scoreProcessor == null || _hud == null)
                return;

            _graphicsDevice.Clear(new Color(15, 15, 20));

            // Calculate scroll speed (pixels per second)
            // At 1.0x speed, notes travel screen height in ~1.5 seconds
            double scrollSpeed = (_game.WindowHeight / 1.5) * _maniaConfig.ScrollSpeed;

            // Draw playfield (columns, notes, receptors)
            _playfield.Draw(_currentTime, scrollSpeed, (float)_maniaConfig.ReceptorPosition, _maniaConfig.DownScroll);

            // Draw background dim overlay if needed
            if (_settings.BackgroundDim > 0)
            {
                DrawBackgroundDim();
            }

            // Draw HUD (score, accuracy, combo, health)
            _hud.Draw(gameTime, _scoreProcessor, _originalBeatmap!, _currentTime, _activeMods);

            // Draw hit error bar if enabled and visible
            if (_hitErrorBar != null && Core.SettingsService.Instance.ShowHitErrorBar)
            {
                // Create a temporary white pixel texture for drawing
                var pixel = new Texture2D(_graphicsDevice, 1, 1);
                pixel.SetData(new[] { Color.White });

                _spriteBatch.Begin();
                _hitErrorBar.Draw(_spriteBatch, pixel, _currentTime);
                _spriteBatch.End();

                pixel.Dispose();
            }

            // Draw pause overlay
            if (_isPaused && _pauseOverlay != null)
            {
                _pauseOverlay.Draw(gameTime);
            }
        }

        private void DrawBackgroundDim()
        {
            _spriteBatch.Begin();

            var pixel = new Texture2D(_graphicsDevice, 1, 1);
            pixel.SetData(new[] { Color.White });

            double dimAmount = Math.Min(_settings.BackgroundDim / 100.0, 0.7); // Max 70% dim to see gameplay
            var fullScreenRect = new Rectangle(0, 0, _game.WindowWidth, _game.WindowHeight);
            _spriteBatch.Draw(pixel, fullScreenRect, new Color(0, 0, 0, (float)dimAmount));

            pixel.Dispose();
            _spriteBatch.End();
        }

        private void Pause()
        {
            _isPaused = true;
            _audioEngine.Pause();
            _pauseOverlay?.Show();
            Console.WriteLine("[ManiaGameplayScreen] Paused");
        }

        private void Resume()
        {
            _isPaused = false;
            _audioEngine.Play(restart: false);
            _pauseOverlay?.Hide();
            _previousKeyboardState = Keyboard.GetState(); // Reset to prevent ghost input
            Console.WriteLine("[ManiaGameplayScreen] Resumed");
        }

        private void Retry()
        {
            Console.WriteLine("[ManiaGameplayScreen] Retrying...");

            var newGameplay = new ManiaGameplayScreen(
                _game,
                _graphicsDevice,
                _spriteBatch,
                _stateManager,
                _audioEngine,
                _inputManager,
                _hitSoundPlayer,
                _settings,
                _originalBeatmap!,
                _activeMods
            );

            _stateManager.ChangeState(GameState.Gameplay, newGameplay);
        }

        private void QuitToSongSelect()
        {
            Console.WriteLine("[ManiaGameplayScreen] Quitting to song select...");

            var songSelect = new SongSelectScreen(
                _game,
                _graphicsDevice,
                _spriteBatch,
                _stateManager,
                _audioEngine,
                _inputManager,
                _hitSoundPlayer,
                _settings
            );

            _stateManager.ChangeState(GameState.SongSelect, songSelect);
        }

        private void HandleFail()
        {
            // For now, just log
            // In future, could show fail animation
            Console.WriteLine("[ManiaGameplayScreen] Failed!");
            // Could implement No Fail mod check here
        }

        private void TransitionToResults()
        {
            Console.WriteLine("[ManiaGameplayScreen] Transitioning to results...");

            var resultsScreen = new ManiaResultsScreen(
                _game,
                _graphicsDevice,
                _spriteBatch,
                _stateManager,
                _audioEngine,
                _inputManager,
                _settings,
                _scoreProcessor!,
                _originalBeatmap!,
                _maniaBeatmap!,
                _activeMods
            );

            _stateManager.ChangeState(GameState.Results, resultsScreen);
        }

        private ManiaConfig LoadManiaConfig()
        {
            // For now, create from game settings
            // In future, could load from separate file
            return new ManiaConfig
            {
                ScrollSpeed = _settings.ScrollSpeed,
                DownScroll = _settings.DownScroll,
                ReceptorPosition = _settings.ReceptorPosition,
                ShowJudgementText = true,
                ShowCombo = true,
                ShowScore = true,
                ShowAccuracy = true,
                ShowHealthBar = true,
                ShowProgressBar = true,
                ShowHitLighting = true,
                ColumnLightBrightness = 1.0,
                BarlineVisibility = 0.5
            };
        }

        public void Cleanup()
        {
            Console.WriteLine("[ManiaGameplayScreen] Cleanup");

            _audioEngine.Stop();
            _inputManager.ClearEventListeners();

            // Unsubscribe from settings changes
            Core.SettingsService.Instance.SettingChanged -= OnSettingChanged;

            // Unsubscribe from judgement events
            if (_playfield?.JudgementDisplayManager != null)
            {
                _playfield.JudgementDisplayManager.JudgementOccurred -= OnJudgementOccurred;
            }

            _pauseOverlay?.Dispose();
            _playfield?.Dispose();
            _isInitialized = false;
        }

        private void OnJudgementOccurred(HitResult result, double offsetMs, double time)
        {
            // Record hit in error bar
            _hitErrorBar?.RecordHit(offsetMs, result, time);
        }

        private void OnSettingChanged(string key, object? value)
        {
            // Apply relevant runtime changes
            switch (key)
            {
                case nameof(Core.SettingsService.ScrollSpeed):
                    if (_maniaConfig != null)
                        _maniaConfig.ScrollSpeed = Core.SettingsService.Instance.ScrollSpeed;
                    break;
                case nameof(Core.SettingsService.ReceptorPosition):
                    if (_maniaConfig != null)
                        _maniaConfig.ReceptorPosition = Core.SettingsService.Instance.ReceptorPosition;
                    break;
                case nameof(Core.SettingsService.DownScroll):
                    if (_maniaConfig != null)
                        _maniaConfig.DownScroll = Core.SettingsService.Instance.DownScroll;
                    break;
                case nameof(Core.SettingsService.GlobalOffsetMs):
                    _audioEngine.GlobalOffsetMs = Core.SettingsService.Instance.GlobalOffsetMs;
                    break;
                case nameof(Core.SettingsService.Volume):
                    _audioEngine.Volume = Core.SettingsService.Instance.Volume;
                    break;
            }
        }
    }
}
