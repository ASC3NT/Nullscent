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

        // Propiedades públicas para acceso desde screens
        public SpriteBatch SpriteBatch => _spriteBatch!;
        public GameStateManager StateManager => _stateManager!;
        public TrueTypeFontRenderer FontRenderer => _fontRenderer!;
        public AudioEngine AudioEngine => _audioEngine!;
        public HitSoundPlayer HitSoundPlayer => _hitSoundPlayer!;
        public GameSettings Settings => _settings!;
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
            // Exit con Alt+F4 (no con Escape, usado para pause)
            var keyboardState = Keyboard.GetState();
            if (keyboardState.IsKeyDown(Keys.LeftAlt) && keyboardState.IsKeyDown(Keys.F4))
            {
                Console.WriteLine("[Game1] Exiting via Alt+F4...");
                Exit();
                return;
            }

            // Actualizar estado actual del juego
            _stateManager?.Update(gameTime);

            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.Black);

            // Dibujar pantalla actual
            _stateManager?.Draw(gameTime);

            base.Draw(gameTime);
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
