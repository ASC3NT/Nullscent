#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Nullscent.Audio;
using Nullscent.Beatmap;
using Nullscent.Config;
using Nullscent.Core;
using Nullscent.Gameplay;
using Nullscent.IO;
using Nullscent.Skin;
using Nullscent.UI;
using System;
using System.IO;

namespace Nullscent
{
    /// <summary>
    /// Clase principal del juego (punto de entrada MonoGame).
    /// Inicializa todos los sistemas: AudioEngine, InputManager, GameStateManager, SkinManager, FileDropManager.
    /// Implementa el game loop con fixed timestep a 1000Hz para máxima precisión de input.
    /// Inspirado en osu!lazer y McOsu - practice client con drag-and-drop de .osz y .osk.
    /// </summary>
    public class Game1 : Game
    {
        private readonly GraphicsDeviceManager _graphics;
        private SpriteBatch? _spriteBatch;

        // Sistemas core
        private AudioEngine? _audioEngine;
        private HitSoundPlayer? _hitSoundPlayer;
        private InputManager? _inputManager;
        private GameStateManager? _stateManager;
        private SkinManager? _skinManager;
        private GameSettings? _settings;
        private FileDropManager? _fileDropManager;
        private TrueTypeFontRenderer? _fontRenderer;

        // Control de volumen con scroll
        private int _previousMouseScrollValue = 0;
        private KeyboardState _previousKeyboardState;
        private double _volumeOverlayTimer = 0.0;

        // Transición de fundido de pantalla suave
        private double _transitionAlpha = 1.0;
        private Texture2D? _fadeTexture;

        // Variables para contador global de FPS (ShowFPS)
        private double _fpsTimer = 0;
        private int _fpsCounter = 0;
        private int _currentFps = 0;

        // Propiedades públicas para acceso desde screens
        public SpriteBatch SpriteBatch => _spriteBatch!;
        public GameStateManager StateManager => _stateManager!;
        public TrueTypeFontRenderer FontRenderer => _fontRenderer!;
        public AudioEngine AudioEngine => _audioEngine!;
        public HitSoundPlayer HitSoundPlayer => _hitSoundPlayer!;
        public GameSettings Settings => _settings!;
        public FileDropManager FileDropManager => _fileDropManager!;
        public GraphicsDeviceManager Graphics => _graphics;
        public int WindowWidth => _graphics.PreferredBackBufferWidth;
        public int WindowHeight => _graphics.PreferredBackBufferHeight;

        public Game1()
        {
            _graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
            IsMouseVisible = true;

            // Fixed timestep a 1000Hz (1ms) para máxima precisión de input
            IsFixedTimeStep = true;
            TargetElapsedTime = TimeSpan.FromMilliseconds(1);

            Window.AllowUserResizing = true;
            Window.ClientSizeChanged += OnWindowSizeChanged;
        }

        protected override void Initialize()
        {
            Console.WriteLine("==============================================");
            Console.WriteLine("  Nullscent - osu!mania Practice Client");
            Console.WriteLine("  Inspired by osu!lazer & McOsu");
            Console.WriteLine("==============================================");
            Console.WriteLine("[Game1] Initializing...");

            // Cargar configuración
            _settings = GameSettings.Load();
            _settings.Validate();

            // Sincronizar SettingsService con la configuración cargada
            SettingsService.Instance.Initialize(_settings);

            // Aplicar configuración de ventana
            _graphics.PreferredBackBufferWidth = _settings.WindowWidth;
            _graphics.PreferredBackBufferHeight = _settings.WindowHeight;
            _graphics.IsFullScreen = _settings.Fullscreen;
            _graphics.SynchronizeWithVerticalRetrace = _settings.VSync;
            _graphics.ApplyChanges();

            Console.WriteLine($"[Game1] Window: {_settings.WindowWidth}x{_settings.WindowHeight} (Fullscreen: {_settings.Fullscreen})");

            // Crear directorios necesarios
            Directory.CreateDirectory(_settings.SongsDirectory);
            Directory.CreateDirectory(_settings.SkinDirectory);
            Console.WriteLine($"[Game1] Songs: {Path.GetFullPath(_settings.SongsDirectory)}");
            Console.WriteLine($"[Game1] Skins: {Path.GetFullPath(_settings.SkinDirectory)}");

            // Inicializar AudioEngine (BASS)
            _audioEngine = new AudioEngine();
            if (!_audioEngine.Initialize())
            {
                Console.WriteLine("[Game1] FATAL: Failed to initialize audio engine");
                Exit();
                return;
            }
            _audioEngine.Volume = _settings.MasterVolume;
            Console.WriteLine("[Game1] Audio engine initialized");

            // Inicializar HitSoundPlayer
            _hitSoundPlayer = new HitSoundPlayer();
            _hitSoundPlayer.Initialize();
            Console.WriteLine("[Game1] Hitsound player initialized");

            // Inicializar InputManager
            _inputManager = new InputManager();
            if (!string.IsNullOrEmpty(_settings.CustomKeybinds))
            {
                _inputManager.LoadCustomKeybinds(_settings.CustomKeybinds);
                Console.WriteLine("[Game1] Loaded custom keybinds");
            }
            Console.WriteLine("[Game1] Input manager initialized");

            // Inicializar GameStateManager
            _stateManager = new GameStateManager();
            Console.WriteLine("[Game1] Game state manager initialized");

            base.Initialize();

            Console.WriteLine("[Game1] Initialization complete");
        }

        protected override void LoadContent()
        {
            Console.WriteLine("[Game1] Loading content...");

            _spriteBatch = new SpriteBatch(GraphicsDevice);

            // Inicializar TrueTypeFontRenderer con fuente.otf
            _fontRenderer = new TrueTypeFontRenderer(GraphicsDevice, _spriteBatch);
            string fontPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fuente.otf");

            // Textura y suscripción para transiciones suaves
            _fadeTexture = new Texture2D(GraphicsDevice, 1, 1);
            _fadeTexture.SetData(new[] { Color.Black });
            _stateManager!.OnStateChanged += (prev, next) =>
            {
                _transitionAlpha = 1.0;
            };

            if (_fontRenderer.LoadFont(fontPath, 24))
            {
                Console.WriteLine($"[Game1] Font loaded successfully from {fontPath}");
            }
            else
            {
                Console.WriteLine($"[Game1] Failed to load font from {fontPath}");
            }

            // Inicializar SkinManager
            _skinManager = new SkinManager(GraphicsDevice);

            // Cargar skin por defecto
            if (Directory.Exists(_settings!.SkinDirectory))
            {
                var skinIniPath = Path.Combine(_settings.SkinDirectory, "skin.ini");
                if (File.Exists(skinIniPath))
                {
                    _skinManager.LoadSkin(_settings.SkinDirectory);
                    Console.WriteLine($"[Game1] Loaded default skin from {_settings.SkinDirectory}");
                }
                else
                {
                    Console.WriteLine("[Game1] No skin.ini found in Skin directory, using fallback rendering");
                }
            }

            // Inicializar FileDropManager para drag-and-drop de .osz y .osk
            _fileDropManager = new FileDropManager(Window, _settings.SongsDirectory, _settings.SkinDirectory);
            _fileDropManager.OnBeatmapImported += path =>
            {
                Console.WriteLine($"[FileDropManager] Beatmap imported: {path}");
                // Trigger rescan en SongSelect si está activo
                if (_stateManager != null && _stateManager.CurrentState == GameState.SongSelect && _stateManager.CurrentScreen is SongSelectScreen songSelect)
                {
                    songSelect.TriggerRescan(path);
                }
            };
            _fileDropManager.OnSkinImported += path =>
            {
                Console.WriteLine($"[FileDropManager] Skin imported: {path}");
                // Auto-load si el usuario lo desea
                _skinManager.LoadSkin(path);
                _settings.SkinDirectory = path;
                _settings.Save();
            };
            _fileDropManager.OnImportError += error =>
            {
                Console.WriteLine($"[FileDropManager] ERROR: {error}");
            };

            Console.WriteLine("[Game1] File drop manager initialized (drag .osz or .osk files to import!)");

            // Iniciar en el Menú Principal
            StartMainMenu();

            Console.WriteLine("[Game1] Content loaded");
        }

        /// <summary>
        /// Inicia el Menú Principal.
        /// </summary>
        private void StartMainMenu()
        {
            var mainMenuScreen = new Nullscent.Screens.MainMenuScreen(
                this,
                _inputManager!,
                _fontRenderer!
            );

            _stateManager!.ChangeState(GameState.MainMenu, mainMenuScreen);
            Console.WriteLine("[Game1] Started Main Menu");
        }

        /// <summary>
        /// Inicia la pantalla de Song Select.
        /// </summary>
        private void StartSongSelect()
        {
            var songSelectScreen = new SongSelectScreen(
                this,
                GraphicsDevice,
                _spriteBatch!,
                _stateManager!,
                _audioEngine!,
                _inputManager!,
                _hitSoundPlayer!,
                _settings!
            );

            _stateManager!.PushState(songSelectScreen);
            Console.WriteLine("[Game1] Started Song Select screen");
        }

        protected override void Update(GameTime gameTime)
        {
            try
            {
                // Exit con Alt+F4 (no con Escape, usado para pause)
                var keyboardState = Keyboard.GetState();
                if (keyboardState.IsKeyDown(Keys.LeftAlt) && keyboardState.IsKeyDown(Keys.F4))
                {
                    Console.WriteLine("[Game1] Exiting via Alt+F4...");
                    Exit();
                    return;
                }

                // Control de volumen con Alt + Rueda del mouse (estilo osu!)
                var mouseState = Mouse.GetState();
                if (IsActive && (keyboardState.IsKeyDown(Keys.LeftAlt) || keyboardState.IsKeyDown(Keys.RightAlt)))
                {
                    if (mouseState.ScrollWheelValue != _previousMouseScrollValue)
                    {
                        int scrollDelta = mouseState.ScrollWheelValue - _previousMouseScrollValue;
                        float volumeChange = scrollDelta > 0 ? 0.05f : -0.05f;

                        float newVolume = Math.Clamp(_settings!.MasterVolume + volumeChange, 0.0f, 1.0f);
                        _settings.MasterVolume = newVolume;
                        _settings.Save();

                        _audioEngine!.Volume = newVolume;
                        Core.SettingsService.Instance.MasterVolume = newVolume;
                        _volumeOverlayTimer = 2.0;

                        Console.WriteLine($"[Game1] Volume changed to {(int)(newVolume * 100)}% (Alt+Scroll)");
                    }
                }
                _previousMouseScrollValue = mouseState.ScrollWheelValue;

                // Toggle Fullscreen with Alt+Enter
                if (IsActive && (keyboardState.IsKeyDown(Keys.LeftAlt) || keyboardState.IsKeyDown(Keys.RightAlt)) &&
                    keyboardState.IsKeyDown(Keys.Enter) && !_previousKeyboardState.IsKeyDown(Keys.Enter))
                {
                    ToggleFullscreen();
                }
                _previousKeyboardState = keyboardState;

                // Actualizar contador de FPS
                _fpsTimer += gameTime.ElapsedGameTime.TotalSeconds;
                _fpsCounter++;
                if (_fpsTimer >= 1.0)
                {
                    _currentFps = _fpsCounter;
                    _fpsCounter = 0;
                    _fpsTimer -= 1.0;
                }

                // Actualizar temporizador de overlay de volumen
                if (_volumeOverlayTimer > 0)
                {
                    _volumeOverlayTimer = Math.Max(0.0, _volumeOverlayTimer - gameTime.ElapsedGameTime.TotalSeconds);
                }

                // Actualizar transición de fundido
                if (_transitionAlpha > 0)
                {
                    _transitionAlpha = Math.Max(0.0, _transitionAlpha - gameTime.ElapsedGameTime.TotalSeconds * 4.0); // 250ms de fundido
                }

                // Actualizar estado actual del juego
                _stateManager?.Update(gameTime);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Game1] CRITICAL ERROR in Update: {ex.GetType().Name}: {ex.Message}");
                Console.WriteLine($"[Game1] Stack trace: {ex.StackTrace}");
                Exit();
                return;
            }

            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            try
            {
                GraphicsDevice.Clear(Color.Black);

                // Dibujar pantalla actual
                _stateManager?.Draw(gameTime);

                // Dibujar transición de fundido
                if (_transitionAlpha > 0 && _fadeTexture != null && _spriteBatch != null)
                {
                    _spriteBatch.Begin();
                    _spriteBatch.Draw(_fadeTexture, new Rectangle(0, 0, WindowWidth, WindowHeight), Color.Black * (float)_transitionAlpha);
                    _spriteBatch.End();
                }

                // Dibujar FPS si está activo
                if (_settings != null && _settings.ShowFPS && _fontRenderer != null && _spriteBatch != null)
                {
                    _spriteBatch.Begin();
                    _fontRenderer.SetFontSize(14);
                    string fpsText = $"{_currentFps} FPS";
                    var textSize = _fontRenderer.MeasureText(fpsText, 1.0f);
                    _fontRenderer.DrawText(fpsText, new Vector2(WindowWidth - textSize.X - 10, WindowHeight - textSize.Y - 10), Color.LimeGreen, 1.0f);
                    _spriteBatch.End();
                }

                // Dibujar volumen si está activo el overlay
                if (_volumeOverlayTimer > 0 && _fontRenderer != null && _spriteBatch != null && _settings != null)
                {
                    float alpha = Math.Min(1.0f, (float)(_volumeOverlayTimer * 2.0));
                    
                    int width = 240;
                    int height = 45;
                    int x = WindowWidth - width - 20;
                    int y = 20;
                    
                    _spriteBatch.Begin();
                    
                    var bgRect = new Rectangle(x, y, width, height);
                    _fontRenderer.DrawBox(bgRect, new Color(20, 20, 25, 200) * alpha);
                    _fontRenderer.DrawBoxBorder(bgRect, Color.HotPink * alpha, 2);
                    
                    _fontRenderer.SetFontSize(14);
                    string volText = $"VOL: {(int)(_settings.MasterVolume * 100)}%";
                    _fontRenderer.DrawText(volText, new Vector2(x + 12, y + 14), Color.White * alpha);
                    
                    int barX = x + 90;
                    int barY = y + 18;
                    int barW = 130;
                    int barH = 8;
                    
                    var barBgRect = new Rectangle(barX, barY, barW, barH);
                    _fontRenderer.DrawBox(barBgRect, new Color(50, 50, 60, 255) * alpha);
                    
                    int fillW = (int)(_settings.MasterVolume * barW);
                    if (fillW > 0)
                    {
                        var barFillRect = new Rectangle(barX, barY, fillW, barH);
                        _fontRenderer.DrawBox(barFillRect, Color.HotPink * alpha);
                    }
                    
                    _spriteBatch.End();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Game1] CRITICAL ERROR in Draw: {ex.GetType().Name}: {ex.Message}");
                Console.WriteLine($"[Game1] Stack trace: {ex.StackTrace}");
                Exit();
                return;
            }

            base.Draw(gameTime);
        }

        public void ToggleFullscreen()
        {
            _settings!.Fullscreen = !_settings.Fullscreen;
            _graphics.IsFullScreen = _settings.Fullscreen;
            
            if (_settings.Fullscreen)
            {
                _graphics.PreferredBackBufferWidth = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Width;
                _graphics.PreferredBackBufferHeight = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Height;
            }
            else
            {
                _graphics.PreferredBackBufferWidth = 1280;
                _graphics.PreferredBackBufferHeight = 720;
            }
            
            _graphics.ApplyChanges();
            _settings.WindowWidth = _graphics.PreferredBackBufferWidth;
            _settings.WindowHeight = _graphics.PreferredBackBufferHeight;
            _settings.Save();
            
            Console.WriteLine($"[Game1] Fullscreen toggled to {_settings.Fullscreen} ({_graphics.PreferredBackBufferWidth}x{_graphics.PreferredBackBufferHeight})");
        }

        private void OnWindowSizeChanged(object? sender, EventArgs e)
        {
            // Evitar recursión infinita
            if (_graphics.PreferredBackBufferWidth == Window.ClientBounds.Width &&
                _graphics.PreferredBackBufferHeight == Window.ClientBounds.Height)
                return;

            _graphics.PreferredBackBufferWidth = Window.ClientBounds.Width;
            _graphics.PreferredBackBufferHeight = Window.ClientBounds.Height;
            _graphics.ApplyChanges();

            _settings!.WindowWidth = _graphics.PreferredBackBufferWidth;
            _settings.WindowHeight = _graphics.PreferredBackBufferHeight;
            _settings.Save();

            Console.WriteLine($"[Game1] Window size changed to {_graphics.PreferredBackBufferWidth}x{_graphics.PreferredBackBufferHeight}");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Console.WriteLine("[Game1] Disposing resources...");

                _stateManager?.Cleanup();
                _audioEngine?.Dispose();
                _hitSoundPlayer?.Dispose();
                _skinManager?.Dispose();
                _spriteBatch?.Dispose();

                // Guardar configuración al salir
                _settings?.Save();

                Console.WriteLine("[Game1] Goodbye!");
            }

            base.Dispose(disposing);
        }
    }
}
