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
        private KeyboardState _previousKeyboardState;
        private MouseState _prevMouseState;
        private bool _needsRescan;
        private string? _pendingImportedPath;
        private bool _showModsOverlay;
        private readonly List<ManiaMod> _activeMods = new();

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

        public async void Initialize()
        {
            Console.WriteLine("[SongSelectScreen] Initialized");

            _fontRenderer = _game.FontRenderer;

            // Reset font size to default for song select (18px is good for lists)
            _fontRenderer?.SetFontSize(18);

            _songList = new SongList();

            // Inicializar estado del teclado y mouse para prevenir input fantasma
            _previousKeyboardState = Keyboard.GetState();
            _prevMouseState = Mouse.GetState();

            // Escanear beatmaps de forma asíncrona
            Console.WriteLine($"[SongSelectScreen] Scanning {_settings.SongsDirectory}...");

            var beatmapSets = await BeatmapScanner.ScanDirectoryAsync(
                _settings.SongsDirectory,
                (current, total) =>
                {
                    // Progress callback (opcional: actualizar UI)
                }
            );

            _songList.LoadBeatmapSets(beatmapSets);
            _isScanning = false;

            Console.WriteLine($"[SongSelectScreen] Scan complete: {beatmapSets.Count} sets loaded");
        }

        public void Update(GameTime gameTime)
        {
            if (_isScanning || _songList == null)
                return;

            var currentKeyboardState = Keyboard.GetState();
            var currentMouseState = Mouse.GetState();

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

                // Click en elementos de la lista de canciones
                int panelWidth = (int)(_graphicsDevice.Viewport.Width * 0.45f);
                int panelHeight = _graphicsDevice.Viewport.Height - 120;
                int panelY = 80;
                int itemHeight = 60;
                int visibleItems = panelHeight / itemHeight;
                var filteredSets = _songList.FilteredBeatmapSets;
                int startIndex = _scrollOffset;
                int endIndex = Math.Min(startIndex + visibleItems, filteredSets.Count);

                for (int i = startIndex; i < endIndex; i++)
                {
                    int yItem = panelY + (i - startIndex) * itemHeight;
                    var itemRect = new Rectangle(10, yItem, panelWidth - 20, itemHeight);
                    if (itemRect.Contains(currentMouseState.X, currentMouseState.Y))
                    {
                        if (currentMouseState.LeftButton == ButtonState.Pressed && _prevMouseState.LeftButton == ButtonState.Released)
                        {
                            if (_songList.SelectedSetIndex == i)
                            {
                                StartSelectedBeatmap();
                            }
                            else
                            {
                                _songList.SelectedSetIndex = i;
                                _songList.SelectedDifficultyIndex = 0;
                            }
                            break;
                        }
                    }
                }

                // Click en dificultades del panel lateral
                var selectedSet = _songList.SelectedBeatmapSet;
                if (selectedSet != null)
                {
                    int panelX = (int)(_graphicsDevice.Viewport.Width * 0.5f);
                    int xDiff = panelX + 20;
                    int currentY = panelY + 115;
                    for (int i = 0; i < selectedSet.Difficulties.Count; i++)
                    {
                        var diffRect = new Rectangle(xDiff + 10, currentY - 2, panelWidth - 40, 20);
                        if (diffRect.Contains(currentMouseState.X, currentMouseState.Y))
                        {
                            if (currentMouseState.LeftButton == ButtonState.Pressed && _prevMouseState.LeftButton == ButtonState.Released)
                            {
                                if (_songList.SelectedDifficultyIndex == i)
                                {
                                    StartSelectedBeatmap();
                                }
                                else
                                {
                                    _songList.SelectedDifficultyIndex = i;
                                }
                                break;
                            }
                        }
                        currentY += 22;
                    }
                }

                // Input handling
                HandleInput(currentKeyboardState);
            }

            // Procesar re-escaneo asíncrono desde el hilo principal
            if (_needsRescan)
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
            if (_songList == null)
                return;

            // Navegación
            if (IsKeyPressed(keyboardState, Keys.Down))
            {
                _songList.SelectNext();
                _scrollOffset = Math.Max(0, _songList.SelectedSetIndex - 5);
            }

            if (IsKeyPressed(keyboardState, Keys.Up))
            {
                _songList.SelectPrevious();
                _scrollOffset = Math.Max(0, _songList.SelectedSetIndex - 5);
            }

            if (IsKeyPressed(keyboardState, Keys.Right))
                _songList.SelectNextDifficulty();

            if (IsKeyPressed(keyboardState, Keys.Left))
                _songList.SelectPreviousDifficulty();

            // Enter para iniciar beatmap
            if (IsKeyPressed(keyboardState, Keys.Enter))
            {
                StartSelectedBeatmap();
            }

            // Escape para volver al main menu
            if (IsKeyPressed(keyboardState, Keys.Escape))
            {
                var mainMenu = new MainMenuScreen(_game, _inputManager, _game.FontRenderer);
                _stateManager.ChangeState(GameState.MainMenu, mainMenu);
            }

            // Backspace para borrar búsqueda
            if (IsKeyPressed(keyboardState, Keys.Back) && _searchText.Length > 0)
            {
                _searchText = _searchText[..^1];
                _songList.SearchQuery = _searchText;
            }

            // Input de búsqueda (letras/números)
            HandleSearchInput(keyboardState);
        }

        private void HandleSearchInput(KeyboardState keyboardState)
        {
            if (_songList == null)
                return;

            // Detectar teclas presionadas para búsqueda
            var keys = keyboardState.GetPressedKeys();

            foreach (var key in keys)
            {
                if (_previousKeyboardState.IsKeyUp(key))
                {
                    char? character = GetCharFromKey(key, keyboardState.IsKeyDown(Keys.LeftShift) || keyboardState.IsKeyDown(Keys.RightShift));

                    if (character.HasValue)
                    {
                        _searchText += character.Value;
                        _songList.SearchQuery = _searchText;
                    }
                }
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

        public async void RescanBeatmaps(string? newDirectoryToSelect = null)
        {
            if (_songList == null) return;

            string? selectedDifficultyPath = _songList.SelectedDifficulty?.FilePath;

            _isScanning = true;
            Console.WriteLine($"[SongSelectScreen] Rescanning {_settings.SongsDirectory}...");

            var beatmapSets = await BeatmapScanner.ScanDirectoryAsync(
                _settings.SongsDirectory,
                null
            );

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
            Console.WriteLine($"[SongSelectScreen] Rescan complete: {beatmapSets.Count} sets loaded");
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

            // Panel izquierdo: Lista de beatmaps
            DrawBeatmapList(screenWidth, screenHeight);

            // Panel derecho: Metadata y dificultades
            DrawMetadataPanel(screenWidth, screenHeight);

            // Barra superior: Búsqueda
            DrawSearchBar(screenWidth);

            // Footer: Controles
            DrawFooter(screenWidth, screenHeight);
        }

        private void DrawBeatmapList(int screenWidth, int screenHeight)
        {
            if (_fontRenderer == null || _songList == null)
                return;

            int panelWidth = (int)(screenWidth * 0.45f);
            int panelHeight = screenHeight - 120;
            int panelY = 80;

            // Fondo del panel
            var panelRect = new Rectangle(0, panelY, panelWidth, panelHeight);
            _fontRenderer.DrawBox(panelRect, _panelColor);

            // Dibujar lista de beatmap sets
            var filteredSets = _songList.FilteredBeatmapSets;
            int itemHeight = 60;
            int visibleItems = panelHeight / itemHeight;
            int startIndex = _scrollOffset;
            int endIndex = Math.Min(startIndex + visibleItems, filteredSets.Count);

            for (int i = startIndex; i < endIndex; i++)
            {
                var set = filteredSets[i];
                int y = panelY + (i - startIndex) * itemHeight;
                bool isSelected = i == _songList.SelectedSetIndex;

                DrawBeatmapSetItem(set, 10, y, panelWidth - 20, itemHeight, isSelected);
            }

            // Scrollbar indicador
            if (filteredSets.Count > visibleItems)
            {
                float scrollPercent = (float)_scrollOffset / (filteredSets.Count - visibleItems);
                int scrollbarHeight = Math.Max(20, panelHeight * visibleItems / filteredSets.Count);
                int scrollbarY = panelY + (int)((panelHeight - scrollbarHeight) * scrollPercent);

                var scrollbarRect = new Rectangle(panelWidth - 5, scrollbarY, 5, scrollbarHeight);
                _fontRenderer.DrawBox(scrollbarRect, _accentColor);
            }
        }

        private void DrawBeatmapSetItem(BeatmapSet set, int x, int y, int width, int height, bool isSelected)
        {
            if (_fontRenderer == null)
                return;

            // Fondo seleccionado
            if (isSelected)
            {
                var bgRect = new Rectangle(x, y, width, height);
                _fontRenderer.DrawBox(bgRect, _accentColor * 0.2f);

                // Borde izquierdo de acento
                var accentRect = new Rectangle(x, y, 4, height);
                _fontRenderer.DrawBox(accentRect, _accentColor);
            }

            // Texto
            Color color = isSelected ? _textColor : _dimTextColor;

            string title = TruncateString(set.DisplayTitle, 30);
            string artist = TruncateString(set.DisplayArtist, 30);
            string creator = $"mapped by {set.Creator}";

            _fontRenderer.DrawText(title, x + 10, y + 5, color);
            _fontRenderer.DrawText(artist, x + 10, y + 25, color * 0.8f);
            _fontRenderer.DrawText(creator, x + 10, y + 40, _dimTextColor * 0.6f);
        }

        private void DrawMetadataPanel(int screenWidth, int screenHeight)
        {
            if (_fontRenderer == null || _songList == null)
                return;

            int panelX = (int)(screenWidth * 0.5f);
            int panelWidth = screenWidth - panelX;
            int panelHeight = screenHeight - 120;
            int panelY = 80;

            var panelRect = new Rectangle(panelX, panelY, panelWidth, panelHeight);
            _fontRenderer.DrawBox(panelRect, _panelColor);

            var selectedSet = _songList.SelectedBeatmapSet;
            if (selectedSet == null)
            {
                _fontRenderer.DrawTextCentered("No beatmap selected", panelX + panelWidth / 2, panelY + panelHeight / 2, _dimTextColor);
                return;
            }

            int x = panelX + 20;
            int y = panelY + 20;

            // Título y artista
            _fontRenderer.DrawText(selectedSet.DisplayTitle, x, y, _textColor);
            y += 30;
            _fontRenderer.DrawText(selectedSet.DisplayArtist, x, y, _dimTextColor);
            y += 40;

            // Dificultades
            _fontRenderer.DrawText("Difficulties:", x, y, _textColor);
            y += 25;

            for (int i = 0; i < selectedSet.Difficulties.Count; i++)
            {
                var diff = selectedSet.Difficulties[i];
                bool isSelected = i == _songList.SelectedDifficultyIndex;
                Color diffColor = isSelected ? _accentColor : _dimTextColor;

                string diffText = $"[{diff.KeyCount}K] {diff.DifficultyName} - OD{diff.OverallDifficulty:F1}";
                _fontRenderer.DrawText(diffText, x + 10, y, diffColor);
                y += 22;
            }

            y += 20;

            // Metadata de la dificultad seleccionada
            var selectedDiff = _songList.SelectedDifficulty;
            if (selectedDiff != null)
            {
                _fontRenderer.DrawText("Stats:", x, y, _textColor);
                y += 25;

                _fontRenderer.DrawText($"BPM: {selectedDiff.BPM:F0}", x + 10, y, _dimTextColor);
                y += 20;

                _fontRenderer.DrawText($"Length: {FormatTime(selectedDiff.Length)}", x + 10, y, _dimTextColor);
                y += 20;

                _fontRenderer.DrawText($"Objects: {selectedDiff.ObjectCount}", x + 10, y, _dimTextColor);
                y += 20;

                _fontRenderer.DrawText($"HP: {selectedDiff.HPDrainRate:F1}", x + 10, y, _dimTextColor);
            }
        }

        private void DrawSearchBar(int screenWidth)
        {
            if (_fontRenderer == null)
                return;

            int barHeight = 60;
            var barRect = new Rectangle(0, 0, screenWidth, barHeight);
            _fontRenderer.DrawBox(barRect, new Color(15, 15, 20));

            // Logo/título
            _fontRenderer.DrawText("Nullscent", 20, 15, _accentColor);

            var mouseState = Mouse.GetState();

            // Botón de Importar (.osz)
            var importButtonRect = new Rectangle(160, 12, 160, 36);
            bool isImportHovered = importButtonRect.Contains(mouseState.X, mouseState.Y);
            Color importBtnColor = isImportHovered ? _accentColor : _accentColor * 0.7f;
            _fontRenderer.DrawBox(importButtonRect, importBtnColor);
            _fontRenderer.DrawTextCentered("Import .osz", importButtonRect.X + importButtonRect.Width / 2, importButtonRect.Y + importButtonRect.Height / 2, Color.White);

            // Botón de Mods
            var modsButtonRect = new Rectangle(330, 12, 120, 36);
            bool isModsHovered = modsButtonRect.Contains(mouseState.X, mouseState.Y);
            string modsText = _activeMods.Count > 0 ? $"Mods ({_activeMods.Count})" : "Mods (F1)";
            Color modsBtnColor = _showModsOverlay ? _accentColor : (isModsHovered ? _accentColor * 0.8f : new Color(40, 40, 50));
            _fontRenderer.DrawBox(modsButtonRect, modsBtnColor);
            _fontRenderer.DrawTextCentered(modsText, modsButtonRect.X + modsButtonRect.Width / 2, modsButtonRect.Y + modsButtonRect.Height / 2, Color.White);

            // Búsqueda
            string searchDisplay = string.IsNullOrEmpty(_searchText) ? "Type to search..." : _searchText;
            Color searchColor = string.IsNullOrEmpty(_searchText) ? _dimTextColor * 0.5f : _textColor;

            int searchX = screenWidth - 400;
            _fontRenderer.DrawText("Search: ", searchX, 20, _dimTextColor);
            _fontRenderer.DrawText(searchDisplay, searchX + 80, 20, searchColor);
        }

        private void DrawFooter(int screenWidth, int screenHeight)
        {
            if (_fontRenderer == null)
                return;

            int footerY = screenHeight - 40;
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
            new ModItem("Mirror", "MR", typeof(ManiaModMirror), "Flip columns horizontally")
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
        }
    }
}
