#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Nullscent.Config;
using Nullscent.Core;
using Nullscent.UI;
using System;
using System.Collections.Generic;
using System.IO;

namespace Nullscent.Screens
{
    /// <summary>
    /// Pantalla de configuración con ajustes básicos y scroll.
    /// </summary>
    public class SettingsScreen : IGameScreen
    {
        private readonly Game1 _game;
        private readonly InputManager _inputManager;
        private readonly TrueTypeFontRenderer _fontRenderer;
        private readonly GameSettings _settings;

        private readonly List<SettingItem> _settingItems = new();
        private int _selectedIndex = 0;
        private int _scrollOffset = 0; // Para scroll

        private KeyboardState _previousKeyState;

        public SettingsScreen(Game1 game, InputManager inputManager, TrueTypeFontRenderer fontRenderer)
        {
            _game = game;
            _inputManager = inputManager;
            _fontRenderer = fontRenderer;
            _settings = GameSettings.Load();
        }

        public void Initialize()
        {
            Console.WriteLine("[SettingsScreen] Initializing settings screen...");

            _settingItems.Clear();

            // Configuraciones básicas
            _settingItems.Add(new SettingItem(
                "Scroll Speed",
                () => _settings.ScrollSpeed.ToString(),
                () => { _settings.ScrollSpeed = Math.Clamp(_settings.ScrollSpeed - 1, 1, 40); _settings.Save(); },
                () => { _settings.ScrollSpeed = Math.Clamp(_settings.ScrollSpeed + 1, 1, 40); _settings.Save(); }
            ));

            _settingItems.Add(new SettingItem(
                "Down Scroll",
                () => _settings.DownScroll ? "ON" : "OFF",
                () => { _settings.DownScroll = !_settings.DownScroll; _settings.Save(); },
                () => { _settings.DownScroll = !_settings.DownScroll; _settings.Save(); }
            ));

            _settingItems.Add(new SettingItem(
                "Receptor Position",
                () => $"{(int)(_settings.ReceptorPosition * 100)}%",
                () => { _settings.ReceptorPosition = (float)Math.Clamp(_settings.ReceptorPosition - 0.02, 0.1, 0.95); _settings.Save(); },
                () => { _settings.ReceptorPosition = (float)Math.Clamp(_settings.ReceptorPosition + 0.02, 0.1, 0.95); _settings.Save(); }
            ));

            _settingItems.Add(new SettingItem(
                "Master Volume",
                () => $"{(int)(_settings.MasterVolume * 100)}%",
                () => { _settings.MasterVolume = (float)Math.Clamp(_settings.MasterVolume - 0.05, 0.0, 1.0); _settings.Save(); _game.AudioEngine.Volume = _settings.MasterVolume; },
                () => { _settings.MasterVolume = (float)Math.Clamp(_settings.MasterVolume + 0.05, 0.0, 1.0); _settings.Save(); _game.AudioEngine.Volume = _settings.MasterVolume; }
            ));

            _settingItems.Add(new SettingItem(
                "Music Volume",
                () => $"{(int)(_settings.MusicVolume * 100)}%",
                () => { _settings.MusicVolume = (float)Math.Clamp(_settings.MusicVolume - 0.05, 0.0, 1.0); _settings.Save(); },
                () => { _settings.MusicVolume = (float)Math.Clamp(_settings.MusicVolume + 0.05, 0.0, 1.0); _settings.Save(); }
            ));

            _settingItems.Add(new SettingItem(
                "Hitsound Volume",
                () => $"{(int)(_settings.HitsoundVolume * 100)}%",
                () => { _settings.HitsoundVolume = (float)Math.Clamp(_settings.HitsoundVolume - 0.05, 0.0, 1.0); _settings.Save(); },
                () => { _settings.HitsoundVolume = (float)Math.Clamp(_settings.HitsoundVolume + 0.05, 0.0, 1.0); _settings.Save(); }
            ));

            _settingItems.Add(new SettingItem(
                "Global Audio Offset",
                () => $"{_settings.GlobalAudioOffset:F0}ms",
                () => { _settings.GlobalAudioOffset -= 5; _settings.Save(); },
                () => { _settings.GlobalAudioOffset += 5; _settings.Save(); }
            ));

            _settingItems.Add(new SettingItem(
                "Background Dim",
                () => $"{(int)(_settings.BackgroundDim * 100)}%",
                () => { _settings.BackgroundDim = (float)Math.Clamp(_settings.BackgroundDim - 0.05, 0.0, 1.0); _settings.Save(); },
                () => { _settings.BackgroundDim = (float)Math.Clamp(_settings.BackgroundDim + 0.05, 0.0, 1.0); _settings.Save(); }
            ));

            _settingItems.Add(new SettingItem(
                "Health Drain",
                () => _settings.HealthDrainEnabled ? "ON" : "OFF (Practice)",
                () => { _settings.HealthDrainEnabled = !_settings.HealthDrainEnabled; _settings.Save(); },
                () => { _settings.HealthDrainEnabled = !_settings.HealthDrainEnabled; _settings.Save(); }
            ));

            _settingItems.Add(new SettingItem(
                "Show FPS",
                () => _settings.ShowFPS ? "ON" : "OFF",
                () => { _settings.ShowFPS = !_settings.ShowFPS; _settings.Save(); },
                () => { _settings.ShowFPS = !_settings.ShowFPS; _settings.Save(); }
            ));

            _settingItems.Add(new SettingItem(
                "VSync",
                () => _settings.VSync ? "ON" : "OFF",
                () => { _settings.VSync = !_settings.VSync; _settings.Save(); },
                () => { _settings.VSync = !_settings.VSync; _settings.Save(); }
            ));

            _settingItems.Add(new SettingItem(
                "Current Skin",
                () => Path.GetFileName(_settings.SkinDirectory),
                null,
                null
            ));

            _selectedIndex = 0;
            _previousKeyState = Keyboard.GetState();

            Console.WriteLine("[SettingsScreen] Settings screen initialized");
        }

        public void Update(GameTime gameTime)
        {
            var currentKeyState = Keyboard.GetState();

            // Navegación
            if (IsKeyPressed(currentKeyState, Keys.Down) || IsKeyPressed(currentKeyState, Keys.S))
            {
                _selectedIndex = (_selectedIndex + 1) % _settingItems.Count;

                // Ajustar scroll para mantener el item seleccionado visible
                const int itemHeight = 60;
                const int headerHeight = 180;
                const int footerHeight = 120;
                int visibleHeight = _game.WindowHeight - headerHeight - footerHeight;
                int maxVisibleItems = visibleHeight / itemHeight;

                if (_selectedIndex >= _scrollOffset + maxVisibleItems)
                {
                    _scrollOffset = _selectedIndex - maxVisibleItems + 1;
                }
            }

            if (IsKeyPressed(currentKeyState, Keys.Up) || IsKeyPressed(currentKeyState, Keys.W))
            {
                _selectedIndex--;
                if (_selectedIndex < 0)
                    _selectedIndex = _settingItems.Count - 1;

                // Ajustar scroll
                if (_selectedIndex < _scrollOffset)
                {
                    _scrollOffset = _selectedIndex;
                }
            }

            // Ajustar valores
            if (IsKeyPressed(currentKeyState, Keys.Left) || IsKeyPressed(currentKeyState, Keys.A))
            {
                _settingItems[_selectedIndex].OnDecrease?.Invoke();
            }

            if (IsKeyPressed(currentKeyState, Keys.Right) || IsKeyPressed(currentKeyState, Keys.D))
            {
                _settingItems[_selectedIndex].OnIncrease?.Invoke();
            }

            // Volver al menú
            if (IsKeyPressed(currentKeyState, Keys.Escape))
            {
                var mainMenu = new MainMenuScreen(_game, _inputManager, _fontRenderer);
                _game.StateManager.ChangeState(GameState.MainMenu, mainMenu);
            }

            _previousKeyState = currentKeyState;
            _inputManager.Update(gameTime.TotalGameTime.TotalMilliseconds);
        }

        public void Draw(GameTime gameTime)
        {
            var spriteBatch = _game.SpriteBatch;

            spriteBatch.Begin();

            // Fondo
            _fontRenderer.DrawBox(new Rectangle(0, 0, _game.WindowWidth, _game.WindowHeight), new Color(15, 15, 20));

            // Título
            _fontRenderer.SetFontSize(48);
            _fontRenderer.DrawTextCentered("SETTINGS", new Vector2(_game.WindowWidth / 2, 80), Color.HotPink, 1.0f);

            // Área de contenido con scroll
            int itemHeight = 60;
            int headerHeight = 180;
            int footerHeight = 120;
            int contentHeight = _game.WindowHeight - headerHeight - footerHeight;
            int maxVisibleItems = contentHeight / itemHeight;

            // Calcular qué items mostrar
            int startIndex = _scrollOffset;
            int endIndex = Math.Min(_scrollOffset + maxVisibleItems, _settingItems.Count);

            _fontRenderer.SetFontSize(24);
            for (int i = startIndex; i < endIndex; i++)
            {
                var item = _settingItems[i];
                bool isSelected = (i == _selectedIndex);
                int yPos = headerHeight + (i - _scrollOffset) * itemHeight;

                // Fondo del item
                if (isSelected)
                {
                    var itemRect = new Rectangle(200, yPos - 5, _game.WindowWidth - 400, itemHeight - 10);
                    _fontRenderer.DrawBox(itemRect, new Color(60, 20, 50, 150));
                    _fontRenderer.DrawBoxBorder(itemRect, Color.HotPink, 2);
                }

                // Nombre del setting
                Color textColor = isSelected ? Color.White : new Color(180, 180, 190);
                _fontRenderer.DrawText(item.Name, new Vector2(250, yPos), textColor, 1.0f);

                // Valor actual
                string valueText = item.GetValue();
                var valueSize = _fontRenderer.MeasureText(valueText, 1.0f);
                _fontRenderer.DrawText(valueText, new Vector2(_game.WindowWidth - 250 - valueSize.X, yPos), textColor, 1.0f);

                // Flechas de ajuste (si está seleccionado y tiene callbacks)
                if (isSelected && (item.OnDecrease != null || item.OnIncrease != null))
                {
                    _fontRenderer.DrawText("<", new Vector2(_game.WindowWidth - 300 - valueSize.X, yPos), Color.HotPink, 1.0f);
                    _fontRenderer.DrawText(">", new Vector2(_game.WindowWidth - 200, yPos), Color.HotPink, 1.0f);
                }
            }

            // Indicador de scroll si hay más items
            if (_settingItems.Count > maxVisibleItems)
            {
                int scrollbarHeight = Math.Max(20, (contentHeight * maxVisibleItems) / _settingItems.Count);
                float scrollPercent = _settingItems.Count > maxVisibleItems ? (float)_scrollOffset / (_settingItems.Count - maxVisibleItems) : 0;
                int scrollbarY = headerHeight + (int)((contentHeight - scrollbarHeight) * scrollPercent);

                var scrollbarRect = new Rectangle(_game.WindowWidth - 10, scrollbarY, 6, scrollbarHeight);
                _fontRenderer.DrawBox(scrollbarRect, Color.HotPink);
            }

            // Instrucciones
            _fontRenderer.SetFontSize(18);
            string instructions = "UP/DOWN: Navigate  |  LEFT/RIGHT: Adjust  |  ESC: Back";
            _fontRenderer.DrawTextCentered(instructions, new Vector2(_game.WindowWidth / 2, _game.WindowHeight - 80), new Color(120, 120, 130), 1.0f);

            // Hint para skins
            _fontRenderer.SetFontSize(16);
            string skinHint = "Drag & drop .osk files to change skin  |  Drag & drop .osz files to add beatmaps";
            _fontRenderer.DrawTextCentered(skinHint, new Vector2(_game.WindowWidth / 2, _game.WindowHeight - 40), new Color(100, 100, 110), 1.0f);

            spriteBatch.End();
        }

        public void Cleanup()
        {
            Console.WriteLine("[SettingsScreen] Cleaning up settings screen");
            _settingItems.Clear();
        }

        private bool IsKeyPressed(KeyboardState currentState, Keys key)
        {
            return currentState.IsKeyDown(key) && _previousKeyState.IsKeyUp(key);
        }
    }

    /// <summary>
    /// Clase auxiliar para un item de configuración.
    /// </summary>
    internal class SettingItem
    {
        public string Name { get; }
        public Func<string> GetValue { get; }
        public Action? OnDecrease { get; }
        public Action? OnIncrease { get; }

        public SettingItem(string name, Func<string> getValue, Action? onDecrease, Action? onIncrease)
        {
            Name = name;
            GetValue = getValue;
            OnDecrease = onDecrease;
            OnIncrease = onIncrease;
        }
    }
}
