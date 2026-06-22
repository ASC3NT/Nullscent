#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Nullscent.Audio;
using Nullscent.Beatmap;
using Nullscent.Config;
using Nullscent.Core;
using Nullscent.UI;
using System;
using System.IO;
using System.Linq;

namespace Nullscent.Gameplay
{
    /// <summary>
    /// Pantalla principal de gameplay donde se reproduce un beatmap.
    /// Integra: AudioEngine, InputManager, NoteRenderer, HitJudge, ScoreEngine, HealthBar, PauseMenu.
    /// </summary>
    public class GameplayScreen : IGameScreen
    {
        private readonly Game1 _game;
        private readonly GraphicsDevice _graphicsDevice;
        private readonly SpriteBatch _spriteBatch;
        private readonly GameStateManager _stateManager;
        private readonly AudioEngine _audioEngine;
        private readonly InputManager _inputManager;
        private readonly HitSoundPlayer _hitSoundPlayer;
        private readonly GameSettings _settings;

        private Beatmap.Beatmap? _beatmap;
        private Column[]? _columns;
        private NoteRenderer? _noteRenderer;
        private TrueTypeFontRenderer? _fontRenderer;
        private HitJudge? _hitJudge;
        private ScoreEngine? _scoreEngine;
        private HealthBar? _healthBar;
        private PauseMenu? _pauseMenu;

        private float _receptorY;
        private bool _isPaused;
        private bool _isInitialized;
        private bool _wasEscapePressed;

        // Colors
        private readonly Color _accentColor = new(255, 102, 171);
        private readonly Color _textColor = Color.White;
        private readonly Color _dimTextColor = new(180, 180, 200);

        public GameplayScreen(
            Game game,
            GraphicsDevice graphicsDevice,
            SpriteBatch spriteBatch,
            GameStateManager stateManager,
            AudioEngine audioEngine,
            InputManager inputManager,
            HitSoundPlayer hitSoundPlayer,
            GameSettings settings,
            Beatmap.Beatmap beatmap)
        {
            _game = (Game1)game;
            _graphicsDevice = graphicsDevice;
            _spriteBatch = spriteBatch;
            _stateManager = stateManager;
            _audioEngine = audioEngine;
            _inputManager = inputManager;
            _hitSoundPlayer = hitSoundPlayer;
            _settings = settings;
            _beatmap = beatmap;
        }

        public void Initialize()
        {
            if (_beatmap == null)
            {
                Console.WriteLine("[GameplayScreen] Error: No beatmap loaded");
                return;
            }

            Console.WriteLine($"[GameplayScreen] Initializing: {_beatmap.Metadata.Title} [{_beatmap.Metadata.Version}]");

            // Inicializar sistemas de juicio
            _hitJudge = new HitJudge(_beatmap.OverallDifficulty);
            _scoreEngine = new ScoreEngine();
            _healthBar = new HealthBar(_beatmap.HPDrainRate, _settings.HealthDrainEnabled);

            // Contar notas y LN para score engine
            int totalNotes = _beatmap.HitObjects.Count;
            int totalLN = _beatmap.HitObjects.Count(ho => ho.IsLongNote);
            _scoreEngine.Initialize(totalNotes, totalLN);

            // Inicializar columnas
            InitializeColumns();

            // Inicializar renderers
            _noteRenderer = new NoteRenderer(_graphicsDevice, _spriteBatch);
            _fontRenderer = _game.FontRenderer;

            // Inicializar pause menu
            _pauseMenu = new PauseMenu(_graphicsDevice, _spriteBatch, _audioEngine, _fontRenderer);

            // Calcular receptor Y
            int screenHeight = _graphicsDevice.Viewport.Height;
            _receptorY = screenHeight * _settings.ReceptorPosition;

            // Configurar input
            _inputManager.ActiveKeyCount = _beatmap.KeyCount;
            _inputManager.ClearEventListeners();
            _inputManager.OnKeyDown += OnColumnKeyDown;
            _inputManager.OnKeyUp += OnColumnKeyUp;

            // Cargar y reproducir audio
            string audioPath = Path.Combine(_beatmap.Directory, _beatmap.Metadata.AudioFilename);
            if (_audioEngine.LoadTrack(audioPath))
            {
                _audioEngine.GlobalOffsetMs = _settings.GlobalAudioOffset;
                _audioEngine.Play(restart: true);
            }
            else
            {
                Console.WriteLine("[GameplayScreen] Failed to load audio");
            }

            // Configurar hitsounds
            _hitSoundPlayer.BaseDirectory = _beatmap.Directory;
            _hitSoundPlayer.HitsoundVolume = _settings.HitsoundVolume;

            _isInitialized = true;
            _isPaused = false;

            Console.WriteLine($"[GameplayScreen] Initialized - {_beatmap.KeyCount}K, OD{_beatmap.OverallDifficulty}");
        }

        private void InitializeColumns()
        {
            if (_beatmap == null || _hitJudge == null || _scoreEngine == null || _healthBar == null)
                return;

            int keyCount = _beatmap.KeyCount;
            _columns = new Column[keyCount];

            // Calcular layout de columnas
            int screenWidth = _graphicsDevice.Viewport.Width;
            float maxColumnWidth = 120f;
            float totalWidth = Math.Min(screenWidth * 0.6f, keyCount * maxColumnWidth);
            float columnWidth = totalWidth / keyCount;
            float startX = (screenWidth - totalWidth) / 2f;

            // Crear columnas y asignar notas
            for (int i = 0; i < keyCount; i++)
            {
                var column = new Column(i, _hitJudge, _scoreEngine, _healthBar)
                {
                    X = startX + i * columnWidth,
                    Width = columnWidth
                };

                // Asignar notas a esta columna
                column.Notes = _beatmap.HitObjects
                    .Where(ho => ho.Column == i)
                    .OrderBy(ho => ho.Time)
                    .ToList();

                // Suscribir a eventos de juicio
                column.OnNoteJudged += OnNoteJudged;

                _columns[i] = column;
            }

            Console.WriteLine($"[GameplayScreen] Initialized {keyCount} columns with {_beatmap.HitObjects.Count} total notes");
        }

        public void Update(GameTime gameTime)
        {
            if (!_isInitialized || _beatmap == null)
                return;

            // Check pause (solo detectar flanco de subida)
            var keyboardState = Keyboard.GetState();
            bool escapePressed = keyboardState.IsKeyDown(Keys.Escape);

            if (escapePressed && !_wasEscapePressed && !_isPaused)
            {
                // Pausar (solo si no estaba pausado antes)
                _isPaused = true;
                _audioEngine.Pause();
                _pauseMenu?.Show();
                Console.WriteLine("[GameplayScreen] Paused");
            }

            _wasEscapePressed = escapePressed;

            // Si está pausado, actualizar solo el pause menu
            if (_isPaused && _pauseMenu != null)
            {
                _pauseMenu.Update(gameTime);

                // Verificar acciones del menú
                switch (_pauseMenu.SelectedAction)
                {
                    case PauseMenu.MenuAction.Resume:
                        _isPaused = false;
                        _audioEngine.Play(restart: false);
                        _pauseMenu.Hide();
                        Console.WriteLine("[GameplayScreen] Resumed");
                        break;

                    case PauseMenu.MenuAction.Retry:
                        RetryBeatmap();
                        return;

                    case PauseMenu.MenuAction.Quit:
                        QuitToSongSelect();
                        return;
                }

                return;
            }

            // Actualizar audio (master clock)
            _audioEngine.Update(_beatmap.Metadata.AudioLeadIn);
            double currentTimeMs = _audioEngine.CurrentPositionMs;

            // Actualizar input
            _inputManager.Update(currentTimeMs);

            // Actualizar columnas (auto-miss)
            if (_columns != null)
            {
                foreach (var column in _columns)
                    column.Update(currentTimeMs);
            }

            // Actualizar health
            _healthBar?.Update(gameTime.ElapsedGameTime.TotalSeconds);

            // Verificar fin de la canción
            if (_scoreEngine != null && _scoreEngine.IsComplete)
            {
                Console.WriteLine("[GameplayScreen] Song complete!");
                TransitionToResults();
            }

            // Verificar fallo
            if (_healthBar != null && _healthBar.IsFailed)
            {
                Console.WriteLine("[GameplayScreen] Failed!");
                TransitionToResults();
            }
        }

        public void Draw(GameTime gameTime)
        {
            if (!_isInitialized)
                return;

            _graphicsDevice.Clear(Color.Black);

            // Dibujar columnas y notas
            if (_columns != null && _noteRenderer != null)
            {
                double currentTimeMs = _audioEngine.CurrentPositionMs;
                int screenHeight = _graphicsDevice.Viewport.Height;
                _noteRenderer.DrawColumns(_columns, currentTimeMs, _settings.ScrollSpeed, _receptorY, screenHeight);
            }

            // Dibujar HUD
            DrawHUD(gameTime);

            // Dibujar pause menu si está pausado
            if (_isPaused)
            {
                _pauseMenu?.Draw(gameTime);
            }
        }

        private void DrawHUD(GameTime gameTime)
        {
            if (_fontRenderer == null || _scoreEngine == null || _healthBar == null)
                return;

            _spriteBatch.Begin();

            int screenWidth = _graphicsDevice.Viewport.Width;
            int screenHeight = _graphicsDevice.Viewport.Height;

            // Score (top-left)
            _fontRenderer.DrawText($"{_scoreEngine.Score:N0}", 20, 20, _textColor);

            // Accuracy y Combo (top-right)
            string accuracyText = $"{_scoreEngine.AccuracyPercent:F2}%";
            _fontRenderer.DrawTextRight(accuracyText, screenWidth - 20, 20, _accentColor);

            string comboText = $"{_scoreEngine.Combo}x";
            _fontRenderer.DrawTextRight(comboText, screenWidth - 20, 50, _dimTextColor);

            // FPS Counter (si está habilitado)
            if (_settings.ShowFPS && gameTime.ElapsedGameTime.TotalSeconds > 0)
            {
                int fps = (int)(1.0 / gameTime.ElapsedGameTime.TotalSeconds);
                _fontRenderer.DrawText($"FPS: {fps}", 20, 50, new Color(100, 200, 100));
            }

            // Beatmap info (top-center)
            if (_beatmap != null)
            {
                string songInfo = $"{_beatmap.Metadata.Title} - {_beatmap.Metadata.Artist}";
                _fontRenderer.DrawTextCentered(songInfo, screenWidth / 2, 20, _dimTextColor * 0.7f);

                string diffInfo = $"[{_beatmap.Metadata.Version}] {_beatmap.KeyCount}K";
                _fontRenderer.DrawTextCentered(diffInfo, screenWidth / 2, 40, _dimTextColor * 0.6f);
            }

            // Health bar (bottom)
            DrawHealthBar(screenWidth, screenHeight);

            _spriteBatch.End();
        }

        private void DrawHealthBar(int screenWidth, int screenHeight)
        {
            if (_fontRenderer == null || _healthBar == null)
                return;

            int barWidth = screenWidth - 40;
            int barHeight = 12;
            int barX = 20;
            int barY = screenHeight - 30;

            // Fondo
            var bgRect = new Rectangle(barX, barY, barWidth, barHeight);
            _fontRenderer.DrawBox(bgRect, new Color(50, 50, 50));

            // Barra de HP
            int fillWidth = (int)(barWidth * _healthBar.Health);
            var fillRect = new Rectangle(barX, barY, fillWidth, barHeight);

            var (r, g, b) = _healthBar.GetHealthBarColor();
            _fontRenderer.DrawBox(fillRect, new Color(r, g, b));

            // Borde
            _fontRenderer.DrawBoxBorder(bgRect, _textColor, 2);
        }

        private void OnColumnKeyDown(int columnIndex, double currentTimeMs)
        {
            if (_columns != null && columnIndex >= 0 && columnIndex < _columns.Length)
                _columns[columnIndex].OnKeyDown(currentTimeMs);
        }

        private void OnColumnKeyUp(int columnIndex, double currentTimeMs)
        {
            if (_columns != null && columnIndex >= 0 && columnIndex < _columns.Length)
                _columns[columnIndex].OnKeyUp(currentTimeMs);
        }

        private void OnNoteJudged(Judgement judgement, Beatmap.HitObject hitObject)
        {
            // Reproducir hitsound
            _hitSoundPlayer.PlayHitsound(hitObject.HitSound, hitObject.SampleFileName);

            // TODO: Mostrar popup de juicio visual (animación)
        }

        private void RetryBeatmap()
        {
            if (_beatmap == null)
                return;

            Console.WriteLine("[GameplayScreen] Retrying...");

            Cleanup();

            var newGameplayScreen = new GameplayScreen(
                _game,
                _graphicsDevice,
                _spriteBatch,
                _stateManager,
                _audioEngine,
                _inputManager,
                _hitSoundPlayer,
                _settings,
                _beatmap
            );

            _stateManager.ChangeState(GameState.Gameplay, newGameplayScreen);
        }

        private void QuitToSongSelect()
        {
            Console.WriteLine("[GameplayScreen] Quitting to song select...");

            var songSelectScreen = new SongSelectScreen(
                _game,
                _graphicsDevice,
                _spriteBatch,
                _stateManager,
                _audioEngine,
                _inputManager,
                _hitSoundPlayer,
                _settings
            );

            _stateManager.ChangeState(GameState.SongSelect, songSelectScreen);
        }

        private void TransitionToResults()
        {
            Console.WriteLine("[GameplayScreen] Transitioning to results...");

            var resultsScreen = new ResultsScreen(
                _game,
                _graphicsDevice,
                _spriteBatch,
                _stateManager,
                _audioEngine,
                _inputManager,
                _hitSoundPlayer,
                _settings,
                _scoreEngine,
                _beatmap
            );

            _stateManager.ChangeState(GameState.Results, resultsScreen);
        }

        public void Cleanup()
        {
            Console.WriteLine("[GameplayScreen] Cleanup");

            _audioEngine.Stop();
            _inputManager.ClearEventListeners();
            _noteRenderer?.Dispose();
            _pauseMenu?.Dispose();

            if (_columns != null)
            {
                foreach (var column in _columns)
                    column.OnNoteJudged -= OnNoteJudged;
            }

            _isInitialized = false;
        }
    }
}
