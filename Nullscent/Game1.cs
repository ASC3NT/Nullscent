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
using Nullscent.Screens;
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
        private SettingsOverlay? _settingsOverlay;

        // Control de volumen con scroll
        private int _previousMouseScrollValue = 0;
        private KeyboardState _previousKeyboardState;
        private double _volumeOverlayTimer = 0.0;

        // Último estado de pantalla válido aplicado con éxito
        private int _lastValidWidth;
        private int _lastValidHeight;
        private bool _lastValidFullscreen;

        // Transición de fundido de pantalla suave
        private double _transitionAlpha = 1.0;
        private Texture2D? _fadeTexture;

        // Variables para contador global de FPS (ShowFPS)
        private double _fpsTimer = 0;
        private int _fpsCounter = 0;
        private int _currentFps = 0;
        private bool _takeScreenshotRequested = false;

        // Propiedades públicas para acceso desde screens
        public SpriteBatch SpriteBatch => _spriteBatch!;
        public GameStateManager StateManager => _stateManager!;
        public TrueTypeFontRenderer FontRenderer => _fontRenderer!;
        public AudioEngine AudioEngine => _audioEngine!;
        public HitSoundPlayer HitSoundPlayer => _hitSoundPlayer!;
        public GameSettings Settings => _settings!;
        public FileDropManager FileDropManager => _fileDropManager!;
        public SkinManager SkinManager => _skinManager!;
        public GraphicsDeviceManager Graphics => _graphics;
        public int WindowWidth => _graphics.PreferredBackBufferWidth;
        public int WindowHeight => _graphics.PreferredBackBufferHeight;
        public SettingsOverlay SettingsOverlay => _settingsOverlay!;
        public void ToggleSettingsOverlay() => _settingsOverlay?.Toggle();

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
            _graphics.HardwareModeSwitch = true; // Activar hardware mode switch para cambio de resolución físico y máximo rendimiento

            try
            {
                _graphics.ApplyChanges();
                _lastValidWidth = _graphics.PreferredBackBufferWidth;
                _lastValidHeight = _graphics.PreferredBackBufferHeight;
                _lastValidFullscreen = _graphics.IsFullScreen;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Game1] Failed to apply initial graphic settings: {ex.Message}. Resetting to default 1280x720 windowed.");
                _graphics.PreferredBackBufferWidth = 1280;
                _graphics.PreferredBackBufferHeight = 720;
                _graphics.IsFullScreen = false;
                try
                {
                    _graphics.ApplyChanges();
                }
                catch { }

                _lastValidWidth = 1280;
                _lastValidHeight = 720;
                _lastValidFullscreen = false;

                // Guardar fallback
                _settings.WindowWidth = 1280;
                _settings.WindowHeight = 720;
                _settings.Fullscreen = false;
                _settings.Save();
            }

            Console.WriteLine($"[Game1] Window: {_lastValidWidth}x{_lastValidHeight} (Fullscreen: {_lastValidFullscreen})");

            // Crear directorios necesarios con salvaguardas para permisos de escritura
            try
            {
                Directory.CreateDirectory(_settings.SongsDirectory);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Game1] Warning: Failed to create Songs directory at {_settings.SongsDirectory}: {ex.Message}");
                string appDataSongs = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Nullscent", "Songs");
                _settings.SongsDirectory = appDataSongs;
                Directory.CreateDirectory(appDataSongs);
                _settings.Save();
            }

            try
            {
                Directory.CreateDirectory(_settings.SkinDirectory);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Game1] Warning: Failed to create Skin directory at {_settings.SkinDirectory}: {ex.Message}");
                string appDataSkin = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Nullscent", "Skin");
                _settings.SkinDirectory = appDataSkin;
                Directory.CreateDirectory(appDataSkin);
                _settings.Save();
            }

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
                bool success = _skinManager.LoadSkin(_settings.SkinDirectory);
                if (success && _skinManager.CurrentSkinConfig != null)
                {
                    Console.WriteLine($"[Game1] Loaded default skin from {_skinManager.CurrentSkinDirectory}");
                    if (_hitSoundPlayer != null)
                    {
                        _hitSoundPlayer.SkinDirectory = _skinManager.CurrentSkinDirectory;
                    }
                }
                else
                {
                    Console.WriteLine("[Game1] No skin.ini found in Skin directory or subdirectories, using fallback rendering");
                    if (_hitSoundPlayer != null)
                    {
                        _hitSoundPlayer.SkinDirectory = _settings.SkinDirectory;
                    }
                }
            }

            // Resolver la carpeta raíz de skins (padre de la skin activa si no es el directorio raíz "Skin")
            string skinsRoot = _settings.SkinDirectory;
            try
            {
                string normalizedSkins = skinsRoot.Replace('\\', '/').TrimEnd('/');
                if (!normalizedSkins.EndsWith("/Skin", StringComparison.OrdinalIgnoreCase) && !normalizedSkins.EndsWith("/Skins", StringComparison.OrdinalIgnoreCase))
                {
                    string? parent = Path.GetDirectoryName(skinsRoot);
                    if (!string.IsNullOrEmpty(parent) && Directory.Exists(parent))
                    {
                        skinsRoot = parent;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Game1] Error resolving skins root directory: {ex.Message}");
            }

            // Inicializar FileDropManager para drag-and-drop de .osz y .osk
            _fileDropManager = new FileDropManager(Window, _settings.SongsDirectory, skinsRoot);
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
                if (_hitSoundPlayer != null)
                {
                    _hitSoundPlayer.SkinDirectory = path;
                    _hitSoundPlayer.ClearCache();
                }
            };
            _fileDropManager.OnImportError += error =>
            {
                Console.WriteLine($"[FileDropManager] ERROR: {error}");
            };

            Console.WriteLine("[Game1] File drop manager initialized (drag .osz or .osk files to import!)");

            // Iniciar en el Menú Principal
            StartMainMenu();

            // Inicializar SettingsOverlay deslizable global
            _settingsOverlay = new SettingsOverlay(this, _inputManager!, _fontRenderer!);
            _settingsOverlay.Initialize();

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

                // Toggle Settings Panel con Ctrl+O
                if (IsActive && (keyboardState.IsKeyDown(Keys.LeftControl) || keyboardState.IsKeyDown(Keys.RightControl)) &&
                    keyboardState.IsKeyDown(Keys.O) && _previousKeyboardState.IsKeyUp(Keys.O))
                {
                    _settingsOverlay?.Toggle();
                }

                // Captura de pantalla con F12
                if (IsActive && keyboardState.IsKeyDown(Keys.F12) && _previousKeyboardState.IsKeyUp(Keys.F12))
                {
                    _takeScreenshotRequested = true;
                    Console.WriteLine("[Game1] Screenshot requested (F12)");
                }

                _previousKeyboardState = keyboardState;
                
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

                // Si el panel de ajustes está completamente abierto, bloquea el input y actualiza el overlay
                if (_settingsOverlay != null && _settingsOverlay.IsFullyOpen)
                {
                    _settingsOverlay.Update(gameTime);
                    _audioEngine?.Update();
                    base.Update(gameTime);
                    return;
                }

                // Actualizar overlay (para animar deslizamientos) y juego normal
                _settingsOverlay?.Update(gameTime);
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
            // Actualizar contador de FPS en Draw para reflejar los frames reales renderizados
            _fpsTimer += gameTime.ElapsedGameTime.TotalSeconds;
            _fpsCounter++;
            if (_fpsTimer >= 1.0)
            {
                _currentFps = _fpsCounter;
                _fpsCounter = 0;
                _fpsTimer -= 1.0;
            }

            try
            {
                GraphicsDevice.Clear(Color.Black);

                // Dibujar pantalla actual
                _stateManager?.Draw(gameTime);

                // Dibujar settings overlay deslizable por encima
                _settingsOverlay?.Draw(gameTime);

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

                // Dibujar cursor personalizado de osu! si está disponible en la skin
                var cursorTex = _skinManager?.GetCursorTexture();
                if (cursorTex != null && _spriteBatch != null)
                {
                    IsMouseVisible = false;
                    var mouseState = Mouse.GetState();
                    _spriteBatch.Begin();
                    float cursorScale = _skinManager != null ? _skinManager.GetTextureScale(cursorTex) : 1.0f;
                    _spriteBatch.Draw(
                        cursorTex, 
                        new Vector2(mouseState.X, mouseState.Y), 
                        null, 
                        Color.White, 
                        0f, 
                        new Vector2(cursorTex.Width / 2f, cursorTex.Height / 2f), 
                        cursorScale, 
                        SpriteEffects.None, 
                        0f
                    );
                    _spriteBatch.End();
                }
                else
                {
                    IsMouseVisible = true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Game1] CRITICAL ERROR in Draw: {ex.GetType().Name}: {ex.Message}");
                Console.WriteLine($"[Game1] Stack trace: {ex.StackTrace}");
                Exit();
                return;
            }

            if (_takeScreenshotRequested)
            {
                ScreenshotHelper.TakeScreenshot(GraphicsDevice, _settings!.CompressScreenshots);
                _takeScreenshotRequested = false;
            }

            base.Draw(gameTime);
        }

        public void ApplyResolution(int width, int height, bool fullscreen)
        {
            try
            {
                // 1. Intentar aplicar el cambio a nivel de hardware gráfico
                _graphics.PreferredBackBufferWidth = width;
                _graphics.PreferredBackBufferHeight = height;
                _graphics.IsFullScreen = fullscreen;
                _graphics.HardwareModeSwitch = true;
                _graphics.ApplyChanges();

                // 2. Si tiene éxito, registrar nuevo estado de rollback y guardar de forma persistente
                _lastValidWidth = width;
                _lastValidHeight = height;
                _lastValidFullscreen = fullscreen;

                _settings!.WindowWidth = width;
                _settings.WindowHeight = height;
                _settings.Fullscreen = fullscreen;
                _settings.Save();

                Console.WriteLine($"[Game1] Resolution applied successfully: {width}x{height} (Fullscreen: {fullscreen})");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Game1] ERROR Gráfico al aplicar resolución ({width}x{height} Fullscreen={fullscreen}): {ex.Message}. Aplicando rollback...");

                // 3. Rollback de hardware al último modo seguro (No sobrescribe settings.json en disco para preservar el deseo del usuario)
                try
                {
                    _graphics.PreferredBackBufferWidth = _lastValidWidth;
                    _graphics.PreferredBackBufferHeight = _lastValidHeight;
                    _graphics.IsFullScreen = _lastValidFullscreen;
                    _graphics.ApplyChanges();
                }
                catch (Exception rollbackEx)
                {
                    Console.WriteLine($"[Game1] Fallback crítico de hardware fallido: {rollbackEx.Message}. Forzando modo ventana.");
                    
                    // Fallback final a ventana segura de emergencia
                    try
                    {
                        _graphics.IsFullScreen = false;
                        _graphics.PreferredBackBufferWidth = 1280;
                        _graphics.PreferredBackBufferHeight = 720;
                        _graphics.ApplyChanges();
                    }
                    catch { }
                }
            }
        }

        public void ToggleFullscreen()
        {
            ApplyResolution(_settings!.WindowWidth, _settings.WindowHeight, !_settings.Fullscreen);
        }

        private void OnWindowSizeChanged(object? sender, EventArgs e)
        {
            // Evitar recursión infinita
            if (_graphics.PreferredBackBufferWidth == Window.ClientBounds.Width &&
                _graphics.PreferredBackBufferHeight == Window.ClientBounds.Height)
                return;

            try
            {
                _graphics.PreferredBackBufferWidth = Window.ClientBounds.Width;
                _graphics.PreferredBackBufferHeight = Window.ClientBounds.Height;
                _graphics.ApplyChanges();

                _lastValidWidth = _graphics.PreferredBackBufferWidth;
                _lastValidHeight = _graphics.PreferredBackBufferHeight;

                _settings!.WindowWidth = _graphics.PreferredBackBufferWidth;
                _settings.WindowHeight = _graphics.PreferredBackBufferHeight;
                _settings.Save();

                Console.WriteLine($"[Game1] Window size changed to {_graphics.PreferredBackBufferWidth}x{_graphics.PreferredBackBufferHeight}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Game1] Warning: Failed to apply window size change during resize event: {ex.Message}");
            }
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
