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
    /// Pantalla de juego principal para el modo Mania.
    /// Coordina el playfield, la lógica de entrada, el audio y la interfaz (HUD).
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
        private ManiaFailOverlay? _failOverlay;
        private bool _isFailed;
        private TrueTypeFontRenderer? _fontRenderer;
        private HitErrorBar? _hitErrorBar;
        private LoadingScreen? _loadingScreen;
        private GameplayClock? _gameplayClock;

        // Recurso optimizado para primitivas 2D
        private Texture2D? _whitePixel;
        private Texture2D? _bgTexture;

        private readonly List<ManiaMod> _activeMods = new();
        private bool _isPaused;
        private bool _isInitialized;
        private bool _wasEscapePressed;
        private double _currentTime;
        private double? _endDelayTimer;
        private string? _scrollNotificationText;
        private double _scrollNotificationTimer;

        // Registro de estado de teclas
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

            _maniaConfig = LoadManiaConfig();

            if (mods != null)
                _activeMods.AddRange(mods);

            Console.WriteLine($"[ManiaGameplayScreen] Inicializado con {_activeMods.Count} modificadores activos");
        }

        public void Initialize()
        {
            if (_originalBeatmap == null)
            {
                Console.WriteLine("[ManiaGameplayScreen] Error: Ningún beatmap cargado");
                return;
            }

            Console.WriteLine($"[ManiaGameplayScreen] Iniciando mapa: {_originalBeatmap.Metadata.Title} [{_originalBeatmap.Metadata.Version}]");

            // Convertir el formato del mapa base al formato nativo de Mania
            var converter = new ManiaBeatmapConverter(_originalBeatmap);
            _maniaBeatmap = converter.Convert();

            ApplyMods();

            // Configurar procesador de puntuaciones (Soporte ScoreV2 activo para LNs)
            int totalNotes = _maniaBeatmap.Notes.Count();
            int totalLongNotes = _maniaBeatmap.HoldNotes.Count();
            _scoreProcessor = new ManiaScoreProcessor(
                totalNotes,
                totalLongNotes,
                _originalBeatmap.HPDrainRate,
                _settings.HealthDrainEnabled,
                _activeMods,
                isScoreV2: true 
            );

            // Initialize loading screen
            _loadingScreen = new LoadingScreen(_graphicsDevice, _spriteBatch, _game.FontRenderer, 1200);
            _loadingScreen.SetScreenDimensions(_game.WindowWidth, _game.WindowHeight);

            // ==========================================
            // PREVENCIÓN DE FUGAS DE MEMORIA: Textura global
            // ==========================================
            _whitePixel = new Texture2D(_graphicsDevice, 1, 1);
            _whitePixel.SetData(new[] { Color.White });

            // Inicialización del entorno de juego (Playfield)
            _playfield = new ManiaPlayfield(
                _graphicsDevice,
                _spriteBatch,
                _maniaBeatmap.KeyCount,
                _game.WindowWidth,
                _game.WindowHeight,
                _game.FontRenderer, 
                _originalBeatmap.OverallDifficulty 
            );
            _playfield.Beatmap = _originalBeatmap;

            // Poblar las columnas con las notas del mapa
            foreach (var hitObject in _maniaBeatmap.HitObjects)
            {
                _playfield.AddHitObject(hitObject);
            }

            // Componentes de interfaz (HUD, Pausa y Fallo)
            _fontRenderer = _game.FontRenderer;
            _hud = new ManiaHUD(_graphicsDevice, _spriteBatch, _fontRenderer, _maniaConfig);
            _pauseOverlay = new ManiaPauseOverlay(_graphicsDevice, _spriteBatch, _fontRenderer);
            _failOverlay = new ManiaFailOverlay(_graphicsDevice, _spriteBatch, _fontRenderer);

            // ==========================================
            // CONFIGURACIÓN DEL HIT ERROR BAR
            // ==========================================
            if (Core.SettingsService.Instance.ShowHitErrorBar && _playfield?.JudgementDisplayManager != null)
            {
                _hitErrorBar = new HitErrorBar(
                    x: _game.WindowWidth / 2 - 150,
                    y: _game.WindowHeight / 2 + 100,
                    width: 300,
                    height: 50,
                    hitWindows: new Rulesets.Mania.Judgements.ManiaHitWindows(_originalBeatmap?.OverallDifficulty ?? 5.0)
                );

                // Suscripción al evento para capturar los desvíos temporales
                _playfield.JudgementDisplayManager.JudgementOccurred += OnJudgementOccurred;
            }

            SetupColumnBindings(_maniaBeatmap.KeyCount);

            // Configuración del track de audio relativo a la ruta del mapa
            string audioPath = System.IO.Path.Combine(_originalBeatmap!.Directory ?? "", _originalBeatmap.Metadata.AudioFilename ?? "");
            
            if (System.IO.File.Exists(audioPath))
            {
                _audioEngine.LoadTrack(audioPath);
                _audioEngine.GlobalOffsetMs = Core.SettingsService.Instance.GlobalOffsetMs;
                _audioEngine.Volume = Core.SettingsService.Instance.MasterVolume;
                ApplyRateMods();
            }
            else
            {
                Console.WriteLine($"[ManiaGameplayScreen] ERROR CRÍTICO: Archivo de audio no encontrado en la ruta {audioPath}");
            }

            // Setup gameplay clock with lead-in (defaulting to AudioLeadIn, fallback to 1500ms)
            double leadIn = _originalBeatmap?.Metadata?.AudioLeadIn ?? 1500;
            _gameplayClock = new GameplayClock(_audioEngine, leadIn);
            _gameplayClock.Start();

            _previousKeyboardState = Keyboard.GetState();
            Core.SettingsService.Instance.SettingChanged += OnSettingChanged; 

            // Cargar fondo de pantalla del mapa
            LoadBackgroundTexture();
            
            // Ocultar el cursor del mouse durante el gameplay
            _game.IsMouseVisible = false;
            
            _isInitialized = true;
        }

        private void ApplyMods()
        {
            if (_maniaBeatmap == null) return;

            foreach (var mod in _activeMods)
            {
                mod.Apply();

                // Lógica especial para mods que alteran la disposición de las notas
                if (mod is ManiaModRandom)
                {
                    var converter = new ManiaBeatmapConverter(_originalBeatmap!);
                    converter.ApplyRandomization(_maniaBeatmap.HitObjects);
                }
                else if (mod is ManiaModMirror)
                {
                    var converter = new ManiaBeatmapConverter(_originalBeatmap!);
                    converter.ApplyMirror(_maniaBeatmap.HitObjects);
                }
            }
        }

        private void ApplyRateMods()
        {
            double rate = 1.0;

            var dtMod = _activeMods.OfType<ManiaModDoubleTime>().FirstOrDefault();
            var htMod = _activeMods.OfType<ManiaModHalfTime>().FirstOrDefault();

            if (dtMod != null) rate = dtMod.SpeedMultiplier;
            else if (htMod != null) rate = htMod.SpeedMultiplier;

            _audioEngine.RateMultiplier = rate;
        }

        private void SetupColumnBindings(int keyCount)
        {
            // Intentar cargar keybinds personalizados desde el InputManager
            _inputManager.ActiveKeyCount = keyCount;
            if (_inputManager.Keybinds.TryGetValue(keyCount, out Keys[]? customKeys))
            {
                for (int i = 0; i < customKeys.Length && i < keyCount; i++)
                {
                    _columnBindings[i] = customKeys[i];
                }
                Console.WriteLine($"[ManiaGameplayScreen] Loaded custom keybinds for {keyCount}K from InputManager: {string.Join(", ", _columnBindings.Values)}");
                return;
            }

            // Mapeos de teclas estándar basados en la disposición física estilo osu!mania
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
                    for (int i = 0; i < Math.Min(keyCount, 10); i++)
                    {
                        _columnBindings[i] = (Keys)((int)Keys.D1 + i);
                    }
                    break;
            }
        }

        public void Update(GameTime gameTime)
        {
            if (!_isInitialized || _maniaBeatmap == null || _playfield == null || _scoreProcessor == null)
                return;

            var keyboardState = Keyboard.GetState();

            // Permitir cambio de scroll speed (F3/F4) en cualquier momento, incluso cargando
            HandleScrollSpeedInput(keyboardState);

            if (_loadingScreen != null && _loadingScreen.IsActive)
            {
                _loadingScreen.Update(gameTime);
                if (!_loadingScreen.IsFadingOut)
                {
                    _previousKeyboardState = keyboardState;
                    return;
                }
            }

            _gameplayClock?.Update(gameTime.ElapsedGameTime.TotalMilliseconds);
            _currentTime = _gameplayClock?.CurrentTime ?? 0.0;

            double runtimeScrollSpeed = Core.SettingsService.Instance.ScrollSpeed;
            _maniaConfig.ScrollSpeed = runtimeScrollSpeed;

            bool escapePressed = keyboardState.IsKeyDown(Keys.Escape);

            if (escapePressed && !_wasEscapePressed && !_isPaused)
            {
                Pause();
            }

            _wasEscapePressed = escapePressed;

            if (_isPaused && _pauseOverlay != null)
            {
                _pauseOverlay.Update(gameTime);

                if (_pauseOverlay.ShouldResume) Resume();
                else if (_pauseOverlay.ShouldRetry) { Retry(); return; }
                else if (_pauseOverlay.ShouldQuit) { QuitToSongSelect(); return; }

                _previousKeyboardState = keyboardState;
                return;
            }

            if (_isFailed && _failOverlay != null)
            {
                _failOverlay.Update(gameTime);

                if (_failOverlay.ShouldRetry) { Retry(); return; }
                else if (_failOverlay.ShouldQuit) { QuitToSongSelect(); return; }

                _previousKeyboardState = keyboardState;
                return;
            }

            HandleInput(keyboardState);

            _playfield.Update(_currentTime, _scoreProcessor, _maniaConfig.ScrollSpeed, (float)_maniaConfig.ReceptorPosition, _maniaConfig.DownScroll);



            _hitErrorBar?.Update(_currentTime);

            if (_scrollNotificationTimer > 0)
            {
                _scrollNotificationTimer -= gameTime.ElapsedGameTime.TotalMilliseconds;
            }

            if (_scoreProcessor.HasFailed)
            {
                HandleFail();
            }

            // Evaluar condición de fin de nivel: audio terminado y todas las notas juzgadas
            bool allObjectsJudged = _scoreProcessor.TotalJudgements >= _scoreProcessor.TotalHitObjects;
            bool audioFinished = !_audioEngine.IsPlaying && _currentTime >= _audioEngine.LengthMs - 300;

            if (audioFinished && allObjectsJudged && !_isPaused && _scoreProcessor.TotalHitObjects > 0)
            {
                if (_endDelayTimer == null)
                {
                    _endDelayTimer = 1500.0; // 1.5 segundos de retraso tras la última nota
                }
                else
                {
                    _endDelayTimer -= gameTime.ElapsedGameTime.TotalMilliseconds;
                    if (_endDelayTimer <= 0)
                    {
                        TransitionToResults();
                    }
                }
            }

            _previousKeyboardState = keyboardState;
        }

        private void HandleInput(KeyboardState keyboardState)
        {
            if (_playfield == null || _scoreProcessor == null) return;

            foreach (var (column, key) in _columnBindings)
            {
                bool wasPressed = _previousKeyboardState.IsKeyDown(key);
                bool isPressed = keyboardState.IsKeyDown(key);

                if (isPressed && !wasPressed)
                {
                    // Sincronizar el input exactamente con el reloj de audio
                    _playfield.HandleKeyPress(column, _currentTime, _scoreProcessor);
                }
                else if (!isPressed && wasPressed)
                {
                    _playfield.HandleKeyRelease(column, _currentTime, _scoreProcessor);
                }
            }
        }

        private void HandleScrollSpeedInput(KeyboardState keyboardState)
        {
            // Ajuste de scroll speed en tiempo real (F3: disminuir, F4: aumentar)
            if (keyboardState.IsKeyDown(Keys.F3) && !_previousKeyboardState.IsKeyDown(Keys.F3))
            {
                double currentSpeed = Core.SettingsService.Instance.ScrollSpeed;
                double newSpeed = Math.Clamp(currentSpeed - 1.0, 1.0, 40.0);
                if (newSpeed != currentSpeed)
                {
                    Core.SettingsService.Instance.ScrollSpeed = newSpeed;
                    Core.SettingsService.Instance.SaveSettings();
                    _scrollNotificationText = $"Velocidad: {newSpeed:F0}";
                    _scrollNotificationTimer = 1200.0;
                    Console.WriteLine($"[ManiaGameplayScreen] Scroll speed decreased to {newSpeed}");
                }
            }
            if (keyboardState.IsKeyDown(Keys.F4) && !_previousKeyboardState.IsKeyDown(Keys.F4))
            {
                double currentSpeed = Core.SettingsService.Instance.ScrollSpeed;
                double newSpeed = Math.Clamp(currentSpeed + 1.0, 1.0, 40.0);
                if (newSpeed != currentSpeed)
                {
                    Core.SettingsService.Instance.ScrollSpeed = newSpeed;
                    Core.SettingsService.Instance.SaveSettings();
                    _scrollNotificationText = $"Velocidad: {newSpeed:F0}";
                    _scrollNotificationTimer = 1200.0;
                    Console.WriteLine($"[ManiaGameplayScreen] Scroll speed increased to {newSpeed}");
                }
            }
        }

        public void Draw(GameTime gameTime)
        {
            if (!_isInitialized || _playfield == null || _scoreProcessor == null || _hud == null)
                return;

            _graphicsDevice.Clear(Color.Black);

            _spriteBatch.Begin();
            if (_bgTexture != null)
            {
                // Dibujar el fondo del beatmap
                _spriteBatch.Draw(_bgTexture, new Rectangle(0, 0, _game.WindowWidth, _game.WindowHeight), Color.White);
            }
            // Dibujar capa de oscurecimiento (dim)
            double dimAmount = Math.Clamp((double)_settings.BackgroundDim, 0.0, 1.0);
            if (_bgTexture == null) dimAmount = 1.0; // Negro absoluto si no hay fondo
            _spriteBatch.Draw(_whitePixel!, new Rectangle(0, 0, _game.WindowWidth, _game.WindowHeight), Color.Black * (float)dimAmount);
            _spriteBatch.End();

            _playfield.Draw(_currentTime, _maniaConfig.ScrollSpeed, (float)_maniaConfig.ReceptorPosition, _maniaConfig.DownScroll);

            _hud.Draw(_scoreProcessor, _originalBeatmap!, _currentTime, _activeMods);

            // Draw hit error bar if enabled (only when loading screen is not active)
            if (!(_loadingScreen != null && _loadingScreen.IsActive) && _hitErrorBar != null && Core.SettingsService.Instance.ShowHitErrorBar && _whitePixel != null)
            {
                _spriteBatch.Begin();
                _hitErrorBar.Draw(_spriteBatch, _whitePixel, _currentTime, _fontRenderer);
                _spriteBatch.End();
            }

            // Draw loading screen overlay on top of everything if active
            if (_loadingScreen != null && _loadingScreen.IsActive)
            {
                _loadingScreen.Draw();
            }

            // Draw scroll speed notification (ALWAYS draw on top of everything!)
            if (_scrollNotificationTimer > 0 && !string.IsNullOrEmpty(_scrollNotificationText) && _fontRenderer != null)
            {
                double progress = _scrollNotificationTimer / 1200.0;
                float alpha = (float)Math.Min(1.0, progress * 4.0);
                
                int panelW = 280;
                int panelH = 50;
                int panelX = (_game.WindowWidth - panelW) / 2;
                int panelY = (_game.WindowHeight - panelH) / 2 - 50;

                _spriteBatch.Begin();
                _fontRenderer.SetFontSize(24);
                _fontRenderer.DrawBox(new Rectangle(panelX, panelY, panelW, panelH), new Color(15, 15, 20, 220) * alpha);
                _fontRenderer.DrawBoxBorder(new Rectangle(panelX, panelY, panelW, panelH), Color.HotPink * alpha, 2);
                _fontRenderer.DrawTextCentered(_scrollNotificationText, panelX + panelW / 2, panelY + panelH / 2, Color.White * alpha);
                _spriteBatch.End();
            }

            if (_isPaused && _pauseOverlay != null)
            {
                _pauseOverlay.Draw(gameTime);
            }

            if (_isFailed && _failOverlay != null)
            {
                _failOverlay.Draw(gameTime);
            }
        }

        private void Pause()
        {
            _isPaused = true;
            _gameplayClock?.Pause();
            _pauseOverlay?.Show();
        }

        private void Resume()
        {
            _isPaused = false;
            _gameplayClock?.Resume();
            _pauseOverlay?.Hide();
            _previousKeyboardState = Keyboard.GetState(); 
        }

        private void Retry()
        {
            var newGameplay = new ManiaGameplayScreen(
                _game, _graphicsDevice, _spriteBatch, _stateManager, _audioEngine, _inputManager,
                _hitSoundPlayer, _settings, _originalBeatmap!, _activeMods
            );
            _stateManager.ChangeState(GameState.Gameplay, newGameplay);
        }

        private void QuitToSongSelect()
        {
            var songSelect = new SongSelectScreen(
                _game, _graphicsDevice, _spriteBatch, _stateManager, _audioEngine, _inputManager,
                _hitSoundPlayer, _settings
            );
            _stateManager.ChangeState(GameState.SongSelect, songSelect);
        }

        private void HandleFail()
        {
            if (_isFailed) return;

            _isFailed = true;
            _failOverlay?.Show();
            _audioEngine.Pause(); // Pause the music on failure
            Console.WriteLine("[ManiaGameplayScreen] Jugador eliminado (Failed)");
        }

        private void TransitionToResults()
        {
            var resultsScreen = new ManiaResultsScreen(
                _game, _graphicsDevice, _spriteBatch, _stateManager, _audioEngine, _inputManager,
                _settings, _scoreProcessor!, _originalBeatmap!, _maniaBeatmap!, _activeMods
            );
            _stateManager.ChangeState(GameState.Results, resultsScreen);
        }

        private ManiaConfig LoadManiaConfig()
        {
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
            _game.IsMouseVisible = true;
            _audioEngine.RateMultiplier = 1.0;
            _audioEngine.Stop();
            _inputManager.ClearEventListeners();

            // Desvincular eventos para evitar fugas de memoria
            Core.SettingsService.Instance.SettingChanged -= OnSettingChanged;

            if (_playfield?.JudgementDisplayManager != null)
            {
                _playfield.JudgementDisplayManager.JudgementOccurred -= OnJudgementOccurred;
            }

            // Liberación de recursos no administrados (Textura cacheada)
            _whitePixel?.Dispose();
            _bgTexture?.Dispose();
            _bgTexture = null;
            _pauseOverlay?.Dispose();
            _playfield?.Dispose();
            _loadingScreen?.Dispose();
            _isInitialized = false;
        }

        private void LoadBackgroundTexture()
        {
            if (string.IsNullOrEmpty(_originalBeatmap!.Directory) || !System.IO.Directory.Exists(_originalBeatmap.Directory))
                return;

            var extensions = new[] { "*.jpg", "*.jpeg", "*.png" };
            string? bgPath = null;
            foreach (var ext in extensions)
            {
                try
                {
                    var files = System.IO.Directory.GetFiles(_originalBeatmap.Directory, ext);
                    if (files.Length > 0)
                    {
                        bgPath = files[0];
                        break;
                    }
                }
                catch { }
            }

            if (bgPath != null && System.IO.File.Exists(bgPath))
            {
                try
                {
                    using (var stream = new System.IO.FileStream(bgPath, System.IO.FileMode.Open, System.IO.FileAccess.Read))
                    {
                        _bgTexture = Texture2D.FromStream(_graphicsDevice, stream);
                    }
                    Console.WriteLine($"[ManiaGameplayScreen] Loaded gameplay background: {System.IO.Path.GetFileName(bgPath)}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ManiaGameplayScreen] Failed to load gameplay bg: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Delegado suscrito a los juicios del Playfield para mapearlos hacia el HitErrorBar.
        /// </summary>
        private void OnJudgementOccurred(HitResult result, double offsetMs, double time)
        {
            _hitErrorBar?.RecordHit(offsetMs, result, time);
        }

        private void OnSettingChanged(string key, object? value)
        {
            switch (key)
            {
                case nameof(Core.SettingsService.ScrollSpeed):
                    if (_maniaConfig != null) _maniaConfig.ScrollSpeed = Core.SettingsService.Instance.ScrollSpeed;
                    break;
                case nameof(Core.SettingsService.ReceptorPosition):
                    if (_maniaConfig != null) _maniaConfig.ReceptorPosition = Core.SettingsService.Instance.ReceptorPosition;
                    break;
                case nameof(Core.SettingsService.DownScroll):
                    if (_maniaConfig != null) _maniaConfig.DownScroll = Core.SettingsService.Instance.DownScroll;
                    break;
                case nameof(Core.SettingsService.GlobalOffsetMs):
                    _audioEngine.GlobalOffsetMs = Core.SettingsService.Instance.GlobalOffsetMs;
                    break;
                case nameof(Core.SettingsService.MasterVolume):
                    _audioEngine.Volume = Core.SettingsService.Instance.MasterVolume;
                    break;
            }
        }
    }
}