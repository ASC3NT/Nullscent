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
using System;
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

            // Inicializar estado del teclado para prevenir input fantasma
            _previousKeyboardState = Keyboard.GetState();

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

            // Input handling
            HandleInput(currentKeyboardState);

            _previousKeyboardState = currentKeyboardState;
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
                    beatmap
                );

                _stateManager.ChangeState(GameState.Gameplay, gameplayScreen);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SongSelectScreen] Failed to load beatmap: {ex.Message}");
            }
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

        public void Cleanup()
        {
            Console.WriteLine("[SongSelectScreen] Cleanup");
        }
    }
}
