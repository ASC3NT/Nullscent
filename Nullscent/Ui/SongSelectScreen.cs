#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Nullscent.Audio;
using Nullscent.Beatmap;
using Nullscent.Config;
using Nullscent.Core;
using Nullscent.Gameplay;
using Nullscent.Screens;
using Nullscent.Rulesets.Mania.Mods;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nullscent.UI
{
    /// <summary>
    /// Pantalla de song select (selección de beatmaps).
    /// Actualizada para usar TrueTypeFontRenderer.
    /// </summary>
    public class SongSelectScreen : IGameScreen
    {
        private readonly Game1 _game;
        private readonly GraphicsDevice _graphicsDevice;
        private readonly SpriteBatch _spriteBatch;
        private readonly GameStateManager _stateManager;
        private readonly AudioEngine _audioEngine;
        private readonly InputManager _inputManager;
        private readonly HitSoundPlayer _hitSoundPlayer;
        private readonly GameSettings _settings;

        private TrueTypeFontRenderer? _fontRenderer;
        private SongList? _songList;
        private bool _isScanning = true;
        private string _searchText = string.Empty;
        private int _scrollOffset;
        private float _smoothScrollOffset;
        private KeyboardState _previousKeyboardState;
        private MouseState _prevMouseState;
        private bool _needsRescan;
        private bool _isDisposed;
        private string? _pendingImportedPath;
        private bool _showModsOverlay;
        private readonly List<ManiaMod> _activeMods = new();
        private float _entranceProgress = 0f;
        private float EaseOutCubic(float x) => 1f - (float)Math.Pow(1f - x, 3);
        private readonly List<(BeatmapSet Set, BeatmapDifficulty Difficulty, int SetIndex, int DifficultyIndex)> _flatDifficulties = new();
        private int _selectedFlatIndex = 0;

        private void RebuildFlatDifficulties()
        {
            _flatDifficulties.Clear();
            if (_songList == null) return;
            var filteredSets = _songList.FilteredBeatmapSets;
            for (int i = 0; i < filteredSets.Count; i++)
            {
                var set = filteredSets[i];
                for (int d = 0; d < set.Difficulties.Count; d++)
                {
                    _flatDifficulties.Add((set, set.Difficulties[d], i, d));
                }
            }
        }

        private void SyncFlatIndex()
        {
            _selectedFlatIndex = 0;
            if (_songList == null) return;
            for (int i = 0; i < _flatDifficulties.Count; i++)
            {
                if (_flatDifficulties[i].SetIndex == _songList.SelectedSetIndex && _flatDifficulties[i].DifficultyIndex == _songList.SelectedDifficultyIndex)
                {
                    _selectedFlatIndex = i;
                    break;
                }
            }
        }

        private void ScrollToSelected()
        {
            if (_songList == null) return;
            int targetIndex = -1;
            for (int i = 0; i < _carouselItems.Count; i++)
            {
                var item = _carouselItems[i];
                if (!item.IsSetHeader && item.SetIndex == _songList.SelectedSetIndex && item.DifficultyIndex == _songList.SelectedDifficultyIndex)
                {
                    targetIndex = i;
                    break;
                }
            }
            if (targetIndex != -1)
            {
                int pHeight = _graphicsDevice.Viewport.Height - 120;
                int iHeight = 60;
                int vItems = pHeight / iHeight;
                _scrollOffset = Math.Max(0, targetIndex - vItems / 2);
            }
        }

        private class CarouselItem
        {
            public bool IsSetHeader { get; set; }
            public BeatmapSet Set { get; set; } = null!;
            public BeatmapDifficulty? Difficulty { get; set; }
            public int SetIndex { get; set; }
            public int DifficultyIndex { get; set; }
        }

        private readonly List<CarouselItem> _carouselItems = new();

        private void RebuildCarouselItems()
        {
            _carouselItems.Clear();
            if (_songList == null) return;

            var filteredSets = _songList.FilteredBeatmapSets;
            for (int i = 0; i < filteredSets.Count; i++)
            {
                var set = filteredSets[i];
                _carouselItems.Add(new CarouselItem
                {
                    IsSetHeader = true,
                    Set = set,
                    SetIndex = i
                });

                if (i == _songList.SelectedSetIndex)
                {
                    for (int d = 0; d < set.Difficulties.Count; d++)
                    {
                        _carouselItems.Add(new CarouselItem
                        {
                            IsSetHeader = false,
                            Set = set,
                            Difficulty = set.Difficulties[d],
                            SetIndex = i,
                            DifficultyIndex = d
                        });
                    }
                }
            }
        }

        // Audio preview, background images and sorting
        private string? _lastSelectedDifficultyPath;
        private Texture2D? _currentBackgroundTexture;
        private string? _currentBackgroundImagePath;
        public enum SortMode { Artist, Title, Stars, BPM, Length }
        private SortMode _currentSortMode = SortMode.Artist;

        // Colors (osu!lazer style)
        private readonly Color _backgroundColor = new(20, 20, 30);
        private readonly Color _accentColor = new(255, 102, 171); // Pink magenta
        private readonly Color _textColor = Color.White;
        private readonly Color _dimTextColor = new(180, 180, 200);
        private readonly Color _panelColor = new(30, 30, 40);

        public SongSelectScreen(
            Game game,
            GraphicsDevice graphicsDevice,
            SpriteBatch spriteBatch,
            GameStateManager stateManager,
            AudioEngine audioEngine,
            InputManager inputManager,
            HitSoundPlayer hitSoundPlayer,
            GameSettings settings)
        {
            _game = (Game1)game;
            _graphicsDevice = graphicsDevice;
            _spriteBatch = spriteBatch;
            _stateManager = stateManager;
            _audioEngine = audioEngine;
            _inputManager = inputManager;
            _hitSoundPlayer = hitSoundPlayer;
            _settings = settings;
        }

        public void Initialize()
        {
            Console.WriteLine("[SongSelectScreen] Initializing...");
            _fontRenderer = _game.FontRenderer;
            _fontRenderer?.SetFontSize(18);
            _songList = new SongList();

            _previousKeyboardState = Keyboard.GetState();
            _prevMouseState = Mouse.GetState();

            _isScanning = true;

            // Iniciar tarea asíncrona de carga de forma segura
            _ = InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            try
            {
                Console.WriteLine(
                    $"[SongSelectScreen] Initialize thread: {Environment.CurrentManagedThreadId}, " +
                    $"Context: {System.Threading.SynchronizationContext.Current?.GetType().FullName ?? "null"}");

                Console.WriteLine($"[SongSelectScreen] Scanning {_settings.SongsDirectory}...");

                var beatmapSets = await BeatmapScanner.ScanDirectoryAsync(
                    _settings.SongsDirectory,
                    (current, total) =>
                    {
                        // Progress callback (opcional: actualizar UI)
                    }
                );

                Console.WriteLine(
                    $"[SongSelectScreen] After await thread: {Environment.CurrentManagedThreadId}, " +
                    $"Context: {System.Threading.SynchronizationContext.Current?.GetType().FullName ?? "null"}");

                if (_isDisposed)
                {
                    Console.WriteLine("[SongSelectScreen] Initialize completed after screen was disposed. Aborting setup.");
                    return;
                }

                _songList.LoadBeatmapSets(beatmapSets);
                _isScanning = false;
                _game.Window.TextInput += OnTextInput;
                RebuildFlatDifficulties();
                SyncFlatIndex();
                RebuildCarouselItems();

                Console.WriteLine($"[SongSelectScreen] Scan complete: {beatmapSets.Count} sets loaded");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SongSelectScreen] CRITICAL ERROR during initialization: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                _isScanning = false;
                if (_songList != null)
                {
                    _songList.LoadBeatmapSets(new List<BeatmapSet>());
                }
            }
        }

        public void Update(GameTime gameTime)
        {
            if (_isScanning || _songList == null)
                return;

            _entranceProgress = Math.Min(1.0f, _entranceProgress + (float)gameTime.ElapsedGameTime.TotalSeconds * 3.0f);

            var currentKeyboardState = Keyboard.GetState();
            var currentMouseState = Mouse.GetState();

            // Detectar cambio de selección para reproducir preview de audio y cargar fondo
            var selectedDiff = _songList.SelectedDifficulty;
            if (selectedDiff != null && selectedDiff.FilePath != _lastSelectedDifficultyPath)
            {
                _lastSelectedDifficultyPath = selectedDiff.FilePath;
                PlaySongPreview(selectedDiff);
                LoadBackgroundTexture(selectedDiff);
                SyncFlatIndex();
                RebuildCarouselItems();
            }

            // Interpolate smooth scroll
            _smoothScrollOffset = MathHelper.Lerp(_smoothScrollOffset, _scrollOffset, 0.15f);

            // Actualizar reloj maestro de audio para permitir bucle de preview
            if (_audioEngine.IsPlaying)
            {
                _audioEngine.Update();
                if (selectedDiff != null && _audioEngine.LengthMs > 0 && _audioEngine.CurrentPositionMs >= _audioEngine.LengthMs - 100)
                {
                    double previewStart = selectedDiff.PreviewTime;
                    if (previewStart <= 0) previewStart = _audioEngine.LengthMs * 0.4;
                    _audioEngine.Seek(previewStart);
                }
            }

            if (!_game.IsActive)
            {
                _prevMouseState = currentMouseState;
                _previousKeyboardState = currentKeyboardState;
                return;
            }

            // Tecla F2 para ciclo de ordenamiento
            if (IsKeyPressed(currentKeyboardState, Keys.F2))
            {
                _currentSortMode = (SortMode)(((int)_currentSortMode + 1) % Enum.GetValues(typeof(SortMode)).Length);
                ApplySorting();
            }

            int pHeight = _graphicsDevice.Viewport.Height - 120;
            int iHeight = 60;
            int vItems = pHeight / iHeight;
            int mouseScrollDelta = currentMouseState.ScrollWheelValue - _prevMouseState.ScrollWheelValue;
            if (mouseScrollDelta != 0)
            {
                int scrollDirection = mouseScrollDelta > 0 ? -1 : 1;
                
                // Si el puntero está en el panel derecho (carrusel de canciones)
                if (currentMouseState.X > _graphicsDevice.Viewport.Width * 0.52f)
                {
                    if (_carouselItems.Count > 0)
                    {
                        if (_flatDifficulties.Count > 0)
                        {
                            _selectedFlatIndex = Math.Clamp(_selectedFlatIndex + scrollDirection, 0, _flatDifficulties.Count - 1);
                            var flat = _flatDifficulties[_selectedFlatIndex];
                            _songList.SelectedSetIndex = flat.SetIndex;
                            _songList.SelectedDifficultyIndex = flat.DifficultyIndex;
                            RebuildCarouselItems();
                            ScrollToSelected();
                        }
                    }
                }
            }

            if (_showModsOverlay)
            {
                // ESC o F1 para cerrar
                if (IsKeyPressed(currentKeyboardState, Keys.Escape) || IsKeyPressed(currentKeyboardState, Keys.F1))
                {
                    _showModsOverlay = false;
                }

                // Check clics en cerrar
                int panelW = 600;
                int panelH = 420;
                int panelX = (_graphicsDevice.Viewport.Width - panelW) / 2;
                int panelY = (_graphicsDevice.Viewport.Height - panelH) / 2;
                var closeBtnRect = new Rectangle(panelX + (panelW - 150) / 2, panelY + panelH - 50, 150, 36);
                if (closeBtnRect.Contains(currentMouseState.X, currentMouseState.Y) &&
                    currentMouseState.LeftButton == ButtonState.Pressed && _prevMouseState.LeftButton == ButtonState.Released)
                {
                    _showModsOverlay = false;
                }

                // Check clics en botones de mods
                int cols = 3;
                int btnW = 160;
                int btnH = 80;
                int startX = panelX + (panelW - (cols * btnW + (cols - 1) * 20)) / 2;
                int startY = panelY + 80;

                for (int i = 0; i < AvailableMods.Length; i++)
                {
                    var mod = AvailableMods[i];
                    int col = i % cols;
                    int row = i / cols;
                    int x = startX + col * (btnW + 20);
                    int y = startY + row * (btnH + 20);

                    var btnRect = new Rectangle(x, y, btnW, btnH);
                    if (btnRect.Contains(currentMouseState.X, currentMouseState.Y) &&
                        currentMouseState.LeftButton == ButtonState.Pressed && _prevMouseState.LeftButton == ButtonState.Released)
                    {
                        bool isActive = _activeMods.Any(m => m.GetType() == mod.ModType);
                        if (isActive)
                        {
                            _activeMods.RemoveAll(m => m.GetType() == mod.ModType);
                        }
                        else
                        {
                            // Mutua exclusión de modificadores incompatibles
                            if (mod.ModType == typeof(ManiaModDoubleTime))
                                _activeMods.RemoveAll(m => m.GetType() == typeof(ManiaModHalfTime));
                            else if (mod.ModType == typeof(ManiaModHalfTime))
                                _activeMods.RemoveAll(m => m.GetType() == typeof(ManiaModDoubleTime));
                            else if (mod.ModType == typeof(ManiaModEasy))
                                _activeMods.RemoveAll(m => m.GetType() == typeof(ManiaModHardRock));
                            else if (mod.ModType == typeof(ManiaModHardRock))
                                _activeMods.RemoveAll(m => m.GetType() == typeof(ManiaModEasy));

                            // Instanciar el modificador seleccionado
                            var newMod = (ManiaMod)Activator.CreateInstance(mod.ModType)!;
                            _activeMods.Add(newMod);
                        }
                        break;
                    }
                }
            }
            else
            {
                // Detectar click en el botón de Importar (.osz)
                var importButtonRect = new Rectangle(160, 12, 160, 36);
                bool isImportHovered = currentMouseState.X >= importButtonRect.X && currentMouseState.X < importButtonRect.X + importButtonRect.Width &&
                                       currentMouseState.Y >= importButtonRect.Y && currentMouseState.Y < importButtonRect.Y + importButtonRect.Height;

                if (isImportHovered && currentMouseState.LeftButton == ButtonState.Pressed && _prevMouseState.LeftButton == ButtonState.Released)
                {
                    OpenImportFileDialog();
                }

                // Detectar click en el botón de Mods
                var modsButtonRect = new Rectangle(330, 12, 120, 36);
                bool isModsHovered = currentMouseState.X >= modsButtonRect.X && currentMouseState.X < modsButtonRect.X + modsButtonRect.Width &&
                                     currentMouseState.Y >= modsButtonRect.Y && currentMouseState.Y < modsButtonRect.Y + modsButtonRect.Height;

                if (isModsHovered && currentMouseState.LeftButton == ButtonState.Pressed && _prevMouseState.LeftButton == ButtonState.Released)
                {
                    _showModsOverlay = true;
                }

                // Tecla F1 para abrir mods
                if (IsKeyPressed(currentKeyboardState, Keys.F1))
                {
                    _showModsOverlay = true;
                }

                // Click en elementos de la lista de canciones (carrusel cilíndrico de CarouselItems)
                int panelWidth = (int)(_graphicsDevice.Viewport.Width * 0.45f);
                int panelHeight = _graphicsDevice.Viewport.Height - 120;
                int panelY = 80;
                int itemHeight = 60;
                int visibleItems = panelHeight / itemHeight;
                int startIndex = Math.Max(0, _scrollOffset - 2);
                int endIndex = Math.Min(startIndex + visibleItems + 4, _carouselItems.Count);

                int screenWidth = _graphicsDevice.Viewport.Width;
                int listPanelX = screenWidth - panelWidth;
                int centerY = panelY + panelHeight / 2;

                for (int i = startIndex; i < endIndex; i++)
                {
                    var item = _carouselItems[i];
                    int yItem = panelY + (int)((i - _smoothScrollOffset) * itemHeight);
                    if (yItem < panelY - itemHeight || yItem > panelY + panelHeight) continue;

                    float dist = Math.Abs(yItem + itemHeight / 2 - centerY);
                    int slantX = (int)(Math.Pow(dist / centerY, 2) * 40.0f);
                    int hitX = listPanelX + 10 + slantX;
                    if (!item.IsSetHeader) hitX += 25; // Indentación

                    var itemRect = new Rectangle(hitX, yItem, panelWidth - (item.IsSetHeader ? 20 : 45), itemHeight);
                    if (itemRect.Contains(currentMouseState.X, currentMouseState.Y))
                    {
                        if (currentMouseState.LeftButton == ButtonState.Pressed && _prevMouseState.LeftButton == ButtonState.Released)
                        {
                            if (item.IsSetHeader)
                            {
                                _songList.SelectedSetIndex = item.SetIndex;
                                _songList.SelectedDifficultyIndex = 0;
                            }
                            else
                            {
                                if (_songList.SelectedSetIndex == item.SetIndex && _songList.SelectedDifficultyIndex == item.DifficultyIndex)
                                {
                                    StartSelectedBeatmap();
                                }
                                else
                                {
                                    _songList.SelectedSetIndex = item.SetIndex;
                                    _songList.SelectedDifficultyIndex = item.DifficultyIndex;
                                }
                            }
                            SyncFlatIndex();
                            RebuildCarouselItems();
                            break;
                        }
                    }
                }

                // Input handling
                HandleInput(currentKeyboardState);
            }

            // Procesar re-escaneo asíncrono desde el hilo principal
            if (_needsRescan && !_isScanning)
            {
                _needsRescan = false;
                string? pathToSelect = _pendingImportedPath;
                _pendingImportedPath = null;
                RescanBeatmaps(pathToSelect);
            }

            _previousKeyboardState = currentKeyboardState;
            _prevMouseState = currentMouseState;
        }

        private void HandleInput(KeyboardState keyboardState)
        {
            if (_songList == null || _carouselItems.Count == 0)
                return;

            // Navegación
            if (IsKeyPressed(keyboardState, Keys.Down))
            {
                if (_flatDifficulties.Count > 0)
                {
                    _selectedFlatIndex = (_selectedFlatIndex + 1) % _flatDifficulties.Count;
                    var flat = _flatDifficulties[_selectedFlatIndex];
                    _songList.SelectedSetIndex = flat.SetIndex;
                    _songList.SelectedDifficultyIndex = flat.DifficultyIndex;
                    RebuildCarouselItems();
                    ScrollToSelected();
                }
            }

            if (IsKeyPressed(keyboardState, Keys.Up))
            {
                if (_flatDifficulties.Count > 0)
                {
                    _selectedFlatIndex = (_selectedFlatIndex - 1 + _flatDifficulties.Count) % _flatDifficulties.Count;
                    var flat = _flatDifficulties[_selectedFlatIndex];
                    _songList.SelectedSetIndex = flat.SetIndex;
                    _songList.SelectedDifficultyIndex = flat.DifficultyIndex;
                    RebuildCarouselItems();
                    ScrollToSelected();
                }
            }

            if (IsKeyPressed(keyboardState, Keys.Right))
            {
                _songList.SelectNextDifficulty();
                SyncFlatIndex();
                RebuildCarouselItems();
                ScrollToSelected();
            }

            if (IsKeyPressed(keyboardState, Keys.Left))
            {
                _songList.SelectPreviousDifficulty();
                SyncFlatIndex();
                RebuildCarouselItems();
                ScrollToSelected();
            }

            // Enter para iniciar beatmap
            if (IsKeyPressed(keyboardState, Keys.Enter))
            {
                StartSelectedBeatmap();
            }

            // Escape para volver al main menu o limpiar búsqueda
            if (IsKeyPressed(keyboardState, Keys.Escape))
            {
                if (_searchText.Length > 0)
                {
                    _searchText = string.Empty;
                    _songList.SearchQuery = _searchText;
                    RebuildCarouselItems();
                }
                else
                {
                    _audioEngine.Stop();
                    var mainMenu = new MainMenuScreen(_game, _inputManager, _game.FontRenderer);
                    _stateManager.ChangeState(GameState.MainMenu, mainMenu);
                }
            }

            // Backspace para borrar búsqueda
            if (IsKeyPressed(keyboardState, Keys.Back) && _searchText.Length > 0)
            {
                _searchText = _searchText[..^1];
                _songList.SearchQuery = _searchText;
                RebuildCarouselItems();
            }
        }

        private void OnTextInput(object? sender, TextInputEventArgs e)
        {
            if (_isScanning || _showModsOverlay || _songList == null)
                return;

            // Enter inicia el beatmap seleccionado inmediatamente
            if (e.Key == Keys.Enter || e.Character == '\r' || e.Character == '\n')
            {
                StartSelectedBeatmap();
                return;
            }

            if (e.Key == Keys.Back || e.Character == '\b')
            {
                if (_searchText.Length > 0)
                {
                    _searchText = _searchText[..^1];
                    _songList.SearchQuery = _searchText;
                    RebuildFlatDifficulties();
                    _selectedFlatIndex = 0;
                    if (_flatDifficulties.Count > 0)
                    {
                        var flat = _flatDifficulties[0];
                        _songList.SelectedSetIndex = flat.SetIndex;
                        _songList.SelectedDifficultyIndex = flat.DifficultyIndex;
                    }
                    RebuildCarouselItems();
                    ScrollToSelected();
                }
                return;
            }

            // Ignorar otras teclas de control para evitar interferencias
            if (e.Key == Keys.Escape || e.Key == Keys.Tab)
                return;

            if (!char.IsControl(e.Character))
            {
                _searchText += e.Character;
                _songList.SearchQuery = _searchText;
                RebuildFlatDifficulties();
                _selectedFlatIndex = 0;
                if (_flatDifficulties.Count > 0)
                {
                    var flat = _flatDifficulties[0];
                    _songList.SelectedSetIndex = flat.SetIndex;
                    _songList.SelectedDifficultyIndex = flat.DifficultyIndex;
                }
                RebuildCarouselItems();
                ScrollToSelected();
            }
        }

        private void StartSelectedBeatmap()
        {
            if (_songList?.SelectedDifficulty == null)
            {
                Console.WriteLine("[SongSelectScreen] No beatmap selected");
                return;
            }

            try
            {
                var difficulty = _songList.SelectedDifficulty;
                Console.WriteLine($"[SongSelectScreen] Loading: {difficulty.FilePath}");

                // Parsear beatmap completo
                var beatmap = BeatmapParser.Parse(difficulty.FilePath);

                // Crear ManiaGameplayScreen (nueva implementación)
                var gameplayScreen = new ManiaGameplayScreen(
                    _game,
                    _graphicsDevice,
                    _spriteBatch,
                    _stateManager,
                    _audioEngine,
                    _inputManager,
                    _hitSoundPlayer,
                    _settings,
                    beatmap,
                    _activeMods
                );

                _stateManager.ChangeState(GameState.Gameplay, gameplayScreen);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SongSelectScreen] Failed to load beatmap: {ex.Message}");
            }
        }

        public void TriggerRescan(string? importedPath = null)
        {
            _pendingImportedPath = importedPath;
            _needsRescan = true;
        }

        public void RescanBeatmaps(string? newDirectoryToSelect = null)
        {
            if (_songList == null || _isScanning) return;

            _isScanning = true;
            _ = RescanBeatmapsAsync(newDirectoryToSelect);
        }

        private async Task RescanBeatmapsAsync(string? newDirectoryToSelect)
        {
            try
            {
                string? selectedDifficultyPath = _songList!.SelectedDifficulty?.FilePath;
                Console.WriteLine($"[SongSelectScreen] Rescanning {_settings.SongsDirectory}...");

                var beatmapSets = await BeatmapScanner.ScanDirectoryAsync(
                    _settings.SongsDirectory,
                    null
                );

                if (_isDisposed)
                {
                    Console.WriteLine("[SongSelectScreen] Rescan completed after screen was disposed. Aborting.");
                    return;
                }

                _songList.LoadBeatmapSets(beatmapSets);

                bool selectedNew = false;
                if (newDirectoryToSelect != null)
                {
                    for (int i = 0; i < _songList.FilteredBeatmapSets.Count; i++)
                    {
                        if (string.Equals(_songList.FilteredBeatmapSets[i].Directory, newDirectoryToSelect, StringComparison.OrdinalIgnoreCase))
                        {
                            _songList.SelectedSetIndex = i;
                            _songList.SelectedDifficultyIndex = 0;
                            selectedNew = true;
                            break;
                        }
                    }
                }

                if (!selectedNew && selectedDifficultyPath != null)
                {
                    _songList.TrySelectDifficultyByPath(selectedDifficultyPath);
                }

                _isScanning = false;
                RebuildFlatDifficulties();
                SyncFlatIndex();
                RebuildCarouselItems();
                ScrollToSelected();
                Console.WriteLine($"[SongSelectScreen] Rescan complete: {beatmapSets.Count} sets loaded");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SongSelectScreen] Error during rescan: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                _isScanning = false;
            }
        }

        private string? FindBackgroundImage(string directory)
        {
            if (string.IsNullOrEmpty(directory) || !System.IO.Directory.Exists(directory))
                return null;

            var extensions = new[] { "*.jpg", "*.jpeg", "*.png" };
            foreach (var ext in extensions)
            {
                try
                {
                    var files = System.IO.Directory.GetFiles(directory, ext);
                    if (files.Length > 0)
                        return files[0];
                }
                catch
                {
                    // Ignorar errores de IO
                }
            }
            return null;
        }

        private void LoadBackgroundTexture(BeatmapDifficulty diff)
        {
            string? bgPath = FindBackgroundImage(System.IO.Path.GetDirectoryName(diff.FilePath)!);
            if (bgPath != _currentBackgroundImagePath)
            {
                _currentBackgroundImagePath = bgPath;
                _currentBackgroundTexture?.Dispose();
                _currentBackgroundTexture = null;

                if (bgPath != null && System.IO.File.Exists(bgPath))
                {
                    try
                    {
                        using (var stream = new System.IO.FileStream(bgPath, System.IO.FileMode.Open, System.IO.FileAccess.Read))
                        {
                            _currentBackgroundTexture = Texture2D.FromStream(_graphicsDevice, stream);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[SongSelectScreen] Failed to load bg image: {ex.Message}");
                    }
                }
            }
        }

        private void PlaySongPreview(BeatmapDifficulty diff)
        {
            try
            {
                _audioEngine.Stop();
                string audioDir = System.IO.Path.GetDirectoryName(diff.FilePath) ?? string.Empty;
                string audioPath = System.IO.Path.Combine(audioDir, diff.AudioFilename);

                if (System.IO.File.Exists(audioPath))
                {
                    if (_audioEngine.LoadTrack(audioPath))
                    {
                        double previewStart = diff.PreviewTime;
                        if (previewStart <= 0)
                        {
                            previewStart = _audioEngine.LengthMs * 0.4;
                        }
                        
                        _audioEngine.Seek(previewStart);
                        _audioEngine.Play();
                        Console.WriteLine($"[SongSelectScreen] Playing preview for: {diff.DifficultyName} starting at {previewStart:F0}ms");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SongSelectScreen] Error playing preview: {ex.Message}");
            }
        }

        private void ApplySorting()
        {
            if (_songList == null) return;
            var selectedDiffPath = _songList.SelectedDifficulty?.FilePath;

            var sets = new List<BeatmapSet>(_songList.AllBeatmapSets);
            switch (_currentSortMode)
            {
                case SortMode.Artist:
                    sets.Sort((a, b) => string.Compare(a.DisplayArtist, b.DisplayArtist, StringComparison.OrdinalIgnoreCase));
                    break;
                case SortMode.Title:
                    sets.Sort((a, b) => string.Compare(a.DisplayTitle, b.DisplayTitle, StringComparison.OrdinalIgnoreCase));
                    break;
                case SortMode.Stars:
                    sets.Sort((a, b) => b.MaxStars.CompareTo(a.MaxStars)); // Higher stars first
                    break;
                case SortMode.BPM:
                    sets.Sort((a, b) => {
                        double maxA = a.Difficulties.Count > 0 ? a.Difficulties.Max(d => d.BPM) : 0;
                        double maxB = b.Difficulties.Count > 0 ? b.Difficulties.Max(d => d.BPM) : 0;
                        return maxB.CompareTo(maxA); // Faster BPM first
                    });
                    break;
                case SortMode.Length:
                    sets.Sort((a, b) => {
                        int maxA = a.Difficulties.Count > 0 ? a.Difficulties.Max(d => d.Length) : 0;
                        int maxB = b.Difficulties.Count > 0 ? b.Difficulties.Max(d => d.Length) : 0;
                        return maxB.CompareTo(maxA); // Longer first
                    });
                    break;
            }

            _songList.LoadBeatmapSets(sets);
            if (selectedDiffPath != null)
            {
                _songList.TrySelectDifficultyByPath(selectedDiffPath);
            }
            RebuildFlatDifficulties();
            SyncFlatIndex();
            RebuildCarouselItems();
            ScrollToSelected();
        }

        private void OpenImportFileDialog()
        {
            var thread = new System.Threading.Thread(() =>
            {
                using (var ofd = new System.Windows.Forms.OpenFileDialog())
                {
                    ofd.Title = "Select Beatmap (.osz) or Skin (.osk) files";
                    ofd.Filter = "osu! Packages (*.osz;*.osk)|*.osz;*.osk|Beatmaps (*.osz)|*.osz|Skins (*.osk)|*.osk|All files (*.*)|*.*";
                    ofd.Multiselect = true;

                    if (ofd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    {
                        Console.WriteLine($"[SongSelectScreen] User selected {ofd.FileNames.Length} files for import");
                        foreach (var file in ofd.FileNames)
                        {
                            _game.FileDropManager.ImportFile(file);
                        }
                    }
                }
            });
            thread.SetApartmentState(System.Threading.ApartmentState.STA);
            thread.Start();
        }

        public void Draw(GameTime gameTime)
        {
            _graphicsDevice.Clear(_backgroundColor);

            if (_fontRenderer == null)
                return;

            _spriteBatch.Begin();

            if (_isScanning)
            {
                DrawScanningScreen();
            }
            else if (_songList != null)
            {
                DrawSongSelectUI();

                if (_showModsOverlay)
                {
                    DrawModsOverlay(_graphicsDevice.Viewport.Width, _graphicsDevice.Viewport.Height);
                }
            }

            _spriteBatch.End();
        }

        private void DrawScanningScreen()
        {
            if (_fontRenderer == null)
                return;

            int centerX = _graphicsDevice.Viewport.Width / 2;
            int centerY = _graphicsDevice.Viewport.Height / 2;

            _fontRenderer.DrawTextCentered("Scanning beatmaps...", centerX, centerY, _textColor);
        }

        private void DrawSongSelectUI()
        {
            if (_fontRenderer == null || _songList == null)
                return;

            int screenWidth = _graphicsDevice.Viewport.Width;
            int screenHeight = _graphicsDevice.Viewport.Height;

            // Dibujar fondo de pantalla de la canción con opacidad oscura (estilo osu!)
            if (_currentBackgroundTexture != null)
            {
                _spriteBatch.Draw(_currentBackgroundTexture, new Rectangle(0, 0, screenWidth, screenHeight), Color.White * 0.15f);
            }

            float ease = EaseOutCubic(_entranceProgress);
            int leftOffset = (int)(-150f * (1f - ease));
            int rightOffset = (int)(150f * (1f - ease));
            int topOffset = (int)(-80f * (1f - ease));
            int bottomOffset = (int)(80f * (1f - ease));

            // Panel derecho: Lista de beatmaps (desliza desde la derecha)
            DrawBeatmapList(screenWidth, screenHeight, rightOffset);

            // Panel izquierdo: Metadata (desliza desde la izquierda)
            DrawMetadataPanel(screenWidth, screenHeight, leftOffset);

            // Barra superior: Búsqueda (desliza desde arriba)
            DrawSearchBar(screenWidth, topOffset);

            // Footer: Controles (desliza desde abajo)
            DrawFooter(screenWidth, screenHeight, bottomOffset);
        }

        private void DrawBeatmapList(int screenWidth, int screenHeight, int offsetX)
        {
            if (_fontRenderer == null || _songList == null)
                return;

            int panelWidth = (int)(screenWidth * 0.45f);
            int panelHeight = screenHeight - 120;
            int panelY = 80;
            int panelX = screenWidth - panelWidth + offsetX;

            int itemHeight = 60;
            int visibleItems = panelHeight / itemHeight;
            int startIndex = Math.Max(0, _scrollOffset - 2);
            int endIndex = Math.Min(startIndex + visibleItems + 4, _carouselItems.Count);

            int centerY = panelY + panelHeight / 2;

            for (int i = startIndex; i < endIndex; i++)
            {
                var item = _carouselItems[i];
                int y = panelY + (int)((i - _smoothScrollOffset) * itemHeight);

                if (y < panelY - itemHeight || y > panelY + panelHeight) continue;

                float dist = Math.Abs(y + itemHeight / 2 - centerY);
                int slantX = (int)(Math.Pow(dist / centerY, 2) * 40.0f);

                int x = panelX + 10 + slantX;
                if (!item.IsSetHeader) x += 25; // Indentar dificultades
                
                bool isSelected = !item.IsSetHeader
                    ? (item.SetIndex == _songList.SelectedSetIndex && item.DifficultyIndex == _songList.SelectedDifficultyIndex)
                    : (item.SetIndex == _songList.SelectedSetIndex);
                if (isSelected) x -= (item.IsSetHeader ? 10 : 15);

                if (!item.IsSetHeader)
                    DrawCarouselDifficultyItem(item.Difficulty!, x, y, panelWidth - 45, itemHeight, isSelected);
                else
                    DrawCarouselSetHeader(item.Set, x, y, panelWidth - 20, itemHeight, isSelected);
            }
        }

        private void DrawCarouselSetHeader(BeatmapSet set, int x, int y, int width, int height, bool isSelected)
        {
            if (_fontRenderer == null) return;
            var bgRect = new Rectangle(x, y, width, height);
            Color cardBg = isSelected ? _accentColor * 0.15f : _panelColor * 0.75f;
            _fontRenderer.DrawBox(bgRect, cardBg);
            _fontRenderer.DrawBoxBorder(bgRect, isSelected ? _accentColor : Color.White * 0.1f, 1);
            
            Color titleColor = isSelected ? Color.White : _dimTextColor;
            Color artistColor = isSelected ? _dimTextColor : _dimTextColor * 0.7f;
            
            string title = TruncateString(set.DisplayTitle, 28);
            string artist = TruncateString(set.DisplayArtist, 32);
            
            _fontRenderer.SetFontSize(16);
            _fontRenderer.DrawText(title, x + 15, y + 8, titleColor);
            _fontRenderer.SetFontSize(12);
            _fontRenderer.DrawText(artist, x + 15, y + 28, artistColor);
            
            if (set.MaxStars > 0)
            {
                _fontRenderer.SetFontSize(14);
                _fontRenderer.DrawTextRight($"{set.MaxStars:F2}★", x + width - 15, y + 18, Color.Yellow);
            }
        }

        private void DrawCarouselDifficultyItem(BeatmapDifficulty diff, int x, int y, int width, int height, bool isSelected)
        {
            if (_fontRenderer == null) return;
            var bgRect = new Rectangle(x, y, width, height);
            Color cardBg = isSelected ? _accentColor * 0.25f : _panelColor * 0.5f;
            _fontRenderer.DrawBox(bgRect, cardBg);
            _fontRenderer.DrawBoxBorder(bgRect, isSelected ? _accentColor : Color.White * 0.1f, 1);
            
            Color titleColor = isSelected ? Color.White : _dimTextColor;
            string keyText = $"[{diff.KeyCount}K] ";
            string name = TruncateString(diff.DifficultyName, 20);
            
            _fontRenderer.SetFontSize(14);
            _fontRenderer.DrawText(keyText, x + 15, y + 8, isSelected ? _accentColor : _dimTextColor);
            _fontRenderer.DrawText(name, x + 55, y + 8, titleColor);
            
            _fontRenderer.SetFontSize(12);
            _fontRenderer.DrawText($"BPM: {diff.BPM:F0}  OD: {diff.OverallDifficulty:F1}  HP: {diff.HPDrainRate:F1}", x + 15, y + 28, _dimTextColor * 0.7f);
            
            _fontRenderer.SetFontSize(14);
            _fontRenderer.DrawTextRight($"{diff.StarRating:F2}★", x + width - 15, y + 18, Color.Yellow);
        }

        private void DrawMetadataPanel(int screenWidth, int screenHeight, int offsetX)
        {
            if (_fontRenderer == null || _songList == null)
                return;

            int panelX = 20 + offsetX;
            int panelWidth = (int)(screenWidth * 0.50f);
            int panelHeight = screenHeight - 120;
            int panelY = 80;

            var panelRect = new Rectangle(panelX, panelY, panelWidth, panelHeight);
            _fontRenderer.DrawBox(panelRect, _panelColor * 0.85f);
            _fontRenderer.DrawBoxBorder(panelRect, Color.White * 0.1f, 1);

            var selectedSet = _songList.SelectedBeatmapSet;
            if (selectedSet == null)
            {
                _fontRenderer.SetFontSize(18);
                _fontRenderer.DrawTextCentered("No beatmap selected", panelX + panelWidth / 2, panelY + panelHeight / 2, _dimTextColor);
                return;
            }

            int x = panelX + 20;
            int y = panelY + 20;

            // Renderizar miniatura del fondo si está disponible (aspecto premium)
            if (_currentBackgroundTexture != null)
            {
                int imgW = panelWidth - 40;
                int imgH = (int)(imgW * 0.40f); // 2.5:1 ratio, más compacto
                var destRect = new Rectangle(x, y, imgW, imgH);
                _spriteBatch.Draw(_currentBackgroundTexture, destRect, Color.White * 0.85f);
                _fontRenderer.DrawBoxBorder(destRect, _accentColor * 0.7f, 2);
                y += imgH + 15;
            }

            // Título y artista
            _fontRenderer.SetFontSize(22);
            _fontRenderer.DrawText(TruncateString(selectedSet.DisplayTitle, 32), x, y, _textColor);
            y += 26;
            _fontRenderer.SetFontSize(16);
            _fontRenderer.DrawText(TruncateString(selectedSet.DisplayArtist, 36), x, y, _dimTextColor);
            y += 24;

            // Metadata de la dificultad seleccionada
            var selectedDiff = _songList.SelectedDifficulty;
            if (selectedDiff != null)
            {
                _fontRenderer.SetFontSize(18);
                string diffNameText = $"Difficulty: {selectedDiff.DifficultyName}";
                _fontRenderer.DrawText(TruncateString(diffNameText, 32), x, y, _accentColor);
                _fontRenderer.DrawTextRight($"{selectedDiff.StarRating:F2}★", panelX + panelWidth - 20, y, Color.Gold);
                y += 30;
            }
            if (selectedDiff != null)
            {
                _fontRenderer.SetFontSize(14);
                _fontRenderer.DrawText("DIFFICULTY STATS:", x, y, _accentColor);
                y += 20;

                // Dibujar barras de progreso para OD y HP
                int statBarW = 140;
                int statBarH = 10;

                // BPM y Duración
                _fontRenderer.SetFontSize(12);
                _fontRenderer.DrawText($"BPM: {selectedDiff.BPM:F0}   |   Length: {FormatTime(selectedDiff.Length)}   |   Objects: {selectedDiff.ObjectCount}", x, y, _dimTextColor);
                y += 18;

                // Overall Difficulty (OD)
                _fontRenderer.DrawText("OD", x, y, _dimTextColor);
                var odBgRect = new Rectangle(x + 35, y + 3, statBarW, statBarH);
                _fontRenderer.DrawBox(odBgRect, _panelColor * 0.5f);
                var odFillRect = new Rectangle(x + 35, y + 3, (int)(statBarW * (selectedDiff.OverallDifficulty / 10.0)), statBarH);
                _fontRenderer.DrawBox(odFillRect, _accentColor);
                _fontRenderer.DrawText($"{selectedDiff.OverallDifficulty:F1}", x + 35 + statBarW + 10, y, _textColor);

                // HP Drain
                _fontRenderer.DrawText("HP", x + 230, y, _dimTextColor);
                var hpBgRect = new Rectangle(x + 260, y + 3, statBarW, statBarH);
                _fontRenderer.DrawBox(hpBgRect, _panelColor * 0.5f);
                var hpFillRect = new Rectangle(x + 260, y + 3, (int)(statBarW * (selectedDiff.HPDrainRate / 10.0)), statBarH);
                _fontRenderer.DrawBox(hpFillRect, Color.Green);
                _fontRenderer.DrawText($"{selectedDiff.HPDrainRate:F1}", x + 260 + statBarW + 10, y, _textColor);

                y += 24;

                // Rankings locales
                _fontRenderer.SetFontSize(14);
                _fontRenderer.DrawText("LOCAL RANKINGS:", x, y, _accentColor);
                y += 20;

                var localScores = Nullscent.Core.LocalScoreManager.GetScores(selectedDiff.FilePath, selectedDiff.Hash);
                if (localScores.Count == 0)
                {
                    _fontRenderer.SetFontSize(12);
                    _fontRenderer.DrawText("No scores set yet (play this map to set a record!)", x, y, _dimTextColor * 0.5f);
                }
                else
                {
                    int displayCount = Math.Min(3, localScores.Count);
                    for (int i = 0; i < displayCount; i++)
                    {
                        var score = localScores[i];
                        string modsStr = score.Mods == "None" ? "" : $" +{score.Mods}";
                        string rankingText = $"#{i + 1} {score.PlayerName} - {score.Score:N0} ({score.Accuracy:F2}%){modsStr}";

                        Color gradeColor = score.Grade.Contains("SS") || score.Grade.Contains("S") ? Color.Gold : _dimTextColor;
                        if (score.Grade.Contains("XH") || score.Grade.Contains("SH")) gradeColor = Color.LightPink;

                        _fontRenderer.SetFontSize(12);
                        _fontRenderer.DrawText($"[{score.Grade}]", x, y, gradeColor);
                        _fontRenderer.DrawText(rankingText, x + 40, y, _textColor);
                        y += 18;
                    }
                }
            }
        }

        private void DrawSearchBar(int screenWidth, int offsetY)
        {
            if (_fontRenderer == null)
                return;

            int barHeight = 60;
            var barRect = new Rectangle(0, offsetY, screenWidth, barHeight);
            _fontRenderer.DrawBox(barRect, new Color(15, 15, 20));

            // Logo/título
            _fontRenderer.DrawText("Nullscent", 20, 15 + offsetY, _accentColor);

            var mouseState = Mouse.GetState();

            // Botón de Importar (.osz)
            var importButtonRect = new Rectangle(160, 12 + offsetY, 160, 36);
            bool isImportHovered = importButtonRect.Contains(mouseState.X, mouseState.Y);
            Color importBtnColor = isImportHovered ? _accentColor : _accentColor * 0.7f;
            _fontRenderer.DrawBox(importButtonRect, importBtnColor);
            _fontRenderer.DrawTextCentered("Import .osz", importButtonRect.X + importButtonRect.Width / 2, importButtonRect.Y + importButtonRect.Height / 2, Color.White);

            // Botón de Mods
            var modsButtonRect = new Rectangle(330, 12 + offsetY, 120, 36);
            bool isModsHovered = modsButtonRect.Contains(mouseState.X, mouseState.Y);
            string modsText = _activeMods.Count > 0 ? $"Mods ({_activeMods.Count})" : "Mods (F1)";
            Color modsBtnColor = _showModsOverlay ? _accentColor : (isModsHovered ? _accentColor * 0.8f : new Color(40, 40, 50));
            _fontRenderer.DrawBox(modsButtonRect, modsBtnColor);
            _fontRenderer.DrawTextCentered(modsText, modsButtonRect.X + modsButtonRect.Width / 2, modsButtonRect.Y + modsButtonRect.Height / 2, Color.White);

            // Búsqueda con tamaño de fuente controlado para evitar desbordes
            _fontRenderer.SetFontSize(16);
            string searchDisplay = string.IsNullOrEmpty(_searchText) ? "Type to search..." : _searchText;
            Color searchColor = string.IsNullOrEmpty(_searchText) ? _dimTextColor * 0.5f : _textColor;

            int sortX = Math.Max(420, screenWidth - 460);
            int searchX = Math.Max(660, screenWidth - 260);

            _fontRenderer.DrawText($"Sort: [F2] {_currentSortMode}", sortX, 20 + offsetY, _accentColor);
            _fontRenderer.DrawText("Search: ", searchX, 20 + offsetY, _dimTextColor);
            string truncatedSearch = TruncateString(searchDisplay, 16);
            _fontRenderer.DrawText(truncatedSearch, searchX + 70, 20 + offsetY, searchColor);
        }

        private void DrawFooter(int screenWidth, int screenHeight, int offsetY)
        {
            if (_fontRenderer == null)
                return;

            int footerY = screenHeight - 40 + offsetY;
            var footerRect = new Rectangle(0, footerY, screenWidth, 40);
            _fontRenderer.DrawBox(footerRect, new Color(15, 15, 20));

            string controls = "Enter: Play  |  Arrows: Navigate  |  Esc: Exit";
            _fontRenderer.DrawTextCentered(controls, screenWidth / 2, footerY + 12, _dimTextColor);
        }

        private bool IsKeyPressed(KeyboardState current, Keys key)
        {
            return current.IsKeyDown(key) && _previousKeyboardState.IsKeyUp(key);
        }

        private char? GetCharFromKey(Keys key, bool shift)
        {
            // Letras
            if (key >= Keys.A && key <= Keys.Z)
            {
                char c = (char)('a' + (key - Keys.A));
                return shift ? char.ToUpper(c) : c;
            }

            // Números
            if (key >= Keys.D0 && key <= Keys.D9)
                return (char)('0' + (key - Keys.D0));

            // Espacio
            if (key == Keys.Space)
                return ' ';

            // Símbolos comunes
            if (key == Keys.OemMinus) return shift ? '_' : '-';
            if (key == Keys.OemPeriod) return shift ? '>' : '.';
            if (key == Keys.OemComma) return shift ? '<' : ',';
            if (key == Keys.OemPlus) return shift ? '+' : '=';
            if (key == Keys.OemQuestion) return shift ? '?' : '/';

            return null;
        }

        private string TruncateString(string text, int maxLength)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            return text.Length > maxLength ? text[..(maxLength - 3)] + "..." : text;
        }

        private string FormatTime(int milliseconds)
        {
            int totalSeconds = milliseconds / 1000;
            int minutes = totalSeconds / 60;
            int seconds = totalSeconds % 60;
            return $"{minutes}:{seconds:D2}";
        }

        private class ModItem
        {
            public string Name { get; }
            public string Acronym { get; }
            public Type ModType { get; }
            public string Description { get; }

            public ModItem(string name, string acronym, Type type, string desc)
            {
                Name = name;
                Acronym = acronym;
                ModType = type;
                Description = desc;
            }
        }

        private static readonly ModItem[] AvailableMods = new[]
        {
            new ModItem("Easy", "EZ", typeof(ManiaModEasy), "Reduces overall difficulty"),
            new ModItem("No Fail", "NF", typeof(ManiaModNoFail), "You can't fail"),
            new ModItem("Half Time", "HT", typeof(ManiaModHalfTime), "0.75x playback speed"),
            new ModItem("Hard Rock", "HR", typeof(ManiaModHardRock), "Increases overall difficulty"),
            new ModItem("Double Time", "DT", typeof(ManiaModDoubleTime), "1.5x playback speed"),
            new ModItem("Hidden", "HD", typeof(ManiaModHidden), "Notes fade out before reaching the judgement line"),
            new ModItem("Flashlight", "FL", typeof(ManiaModFlashlight), "Restricted view area"),
            new ModItem("Random", "RD", typeof(ManiaModRandom), "Shuffle the columns"),
            new ModItem("Mirror", "MR", typeof(ManiaModMirror), "Flip columns horizontally"),
            new ModItem("Autoplay", "AT", typeof(ManiaModAutoplay), "Watch the game play itself")
        };

        private void DrawModsOverlay(int screenWidth, int screenHeight)
        {
            if (_fontRenderer == null) return;

            // Fondo negro semi-transparente para toda la pantalla
            _fontRenderer.DrawBox(new Rectangle(0, 0, screenWidth, screenHeight), new Color(0, 0, 0, 150));

            int panelW = 600;
            int panelH = 420;
            int panelX = (screenWidth - panelW) / 2;
            int panelY = (screenHeight - panelH) / 2;

            // Panel de mods
            var panelRect = new Rectangle(panelX, panelY, panelW, panelH);
            _fontRenderer.DrawBox(panelRect, new Color(25, 25, 35, 245));
            _fontRenderer.DrawBoxBorder(panelRect, _accentColor, 4);

            _fontRenderer.SetFontSize(36);
            _fontRenderer.DrawTextCentered("MODIFIERS (Click to toggle)", panelX + panelW / 2, panelY + 30, Color.White);

            int cols = 3;
            int btnW = 160;
            int btnH = 80;
            int startX = panelX + (panelW - (cols * btnW + (cols - 1) * 20)) / 2;
            int startY = panelY + 80;

            var mouseState = Mouse.GetState();

            for (int i = 0; i < AvailableMods.Length; i++)
            {
                var mod = AvailableMods[i];
                int col = i % cols;
                int row = i / cols;
                int x = startX + col * (btnW + 20);
                int y = startY + row * (btnH + 20);

                var btnRect = new Rectangle(x, y, btnW, btnH);
                bool isHovered = btnRect.Contains(mouseState.X, mouseState.Y);
                bool isActive = _activeMods.Any(m => m.GetType() == mod.ModType);

                Color bgColor = isActive ? _accentColor : (isHovered ? new Color(60, 60, 80) : new Color(40, 40, 50));
                Color borderColor = isActive ? Color.White : (isHovered ? _accentColor * 0.7f : new Color(70, 70, 90));
                Color textColor = isActive ? Color.White : (isHovered ? Color.White : _dimTextColor);

                _fontRenderer.DrawBox(btnRect, bgColor);
                _fontRenderer.DrawBoxBorder(btnRect, borderColor, 2);

                _fontRenderer.SetFontSize(28);
                _fontRenderer.DrawTextCentered(mod.Acronym, x + btnW / 2, y + 25, textColor);

                _fontRenderer.SetFontSize(16);
                _fontRenderer.DrawTextCentered(mod.Name, x + btnW / 2, y + 55, textColor * 0.9f);
            }

            // Botón cerrar
            var closeBtnRect = new Rectangle(panelX + (panelW - 150) / 2, panelY + panelH - 50, 150, 36);
            bool isCloseHovered = closeBtnRect.Contains(mouseState.X, mouseState.Y);
            _fontRenderer.DrawBox(closeBtnRect, isCloseHovered ? _accentColor : new Color(50, 50, 60));
            _fontRenderer.DrawBoxBorder(closeBtnRect, Color.White * 0.5f, 1);
            _fontRenderer.SetFontSize(18);
            _fontRenderer.DrawTextCentered("Close (ESC)", closeBtnRect.X + closeBtnRect.Width / 2, closeBtnRect.Y + closeBtnRect.Height / 2, Color.White);
        }

        public void Cleanup()
        {
            Console.WriteLine("[SongSelectScreen] Cleanup");
            _isDisposed = true;
            if (_game?.Window != null)
            {
                _game.Window.TextInput -= OnTextInput;
            }
            _currentBackgroundTexture?.Dispose();
            _currentBackgroundTexture = null;
            _audioEngine.Stop();
        }
    }
}
