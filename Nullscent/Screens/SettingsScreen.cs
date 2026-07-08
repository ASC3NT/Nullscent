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
    /// Pantalla de configuración avanzada e interactiva con soporte para ratón,
    /// barras de deslizamiento, botones toggle y re-mapeador visual de Keybinds (4K-7K).
    /// </summary>
    public class SettingsScreen : IGameScreen
    {
        private readonly Game1 _game;
        private readonly InputManager _inputManager;
        private readonly TrueTypeFontRenderer _fontRenderer;
        private readonly GameSettings _settings;

        private readonly List<SettingItem> _settingItems = new();
        private int _selectedIndex = 0;
        private int _scrollOffset = 0;

        private KeyboardState _previousKeyState;
        private MouseState _prevMouseState;
        private float[] _hoverProgressArray = Array.Empty<float>();

        // Arrastre de sliders
        private int _draggingIndex = -1;

        // Configuración visual de Keybinds
        private bool _isBindingKey;
        private int _bindingKeyCount = 4;
        private int _bindingColumnIndex = -1;

        public SettingsScreen(Game1 game, InputManager inputManager, TrueTypeFontRenderer fontRenderer)
        {
            _game = game;
            _inputManager = inputManager;
            _fontRenderer = fontRenderer;
            _settings = game.Settings;
        }

        public void Initialize()
        {
            Console.WriteLine("[SettingsScreen] Initializing settings screen...");

            _settingItems.Clear();

            // 1. Velocidad de Scroll (Slider de 1 a 40)
            _settingItems.Add(SettingItem.CreateSlider(
                "Scroll Speed",
                1.0f, 40.0f,
                () => (float)_settings.ScrollSpeed,
                (val) => { 
                    int intVal = (int)Math.Round(val);
                    _settings.ScrollSpeed = intVal; 
                    _settings.Save(); 
                    Core.SettingsService.Instance.ScrollSpeed = (double)intVal;
                },
                () => _settings.ScrollSpeed.ToString()
            ));

            // 2. Down Scroll (Toggle ON/OFF)
            _settingItems.Add(SettingItem.CreateToggle(
                "Down Scroll",
                () => _settings.DownScroll ? "ON" : "OFF",
                () => { 
                    _settings.DownScroll = !_settings.DownScroll; 
                    _settings.Save(); 
                    Core.SettingsService.Instance.DownScroll = _settings.DownScroll;
                }
            ));

            // 3. Posición del Receptor (Slider 10% a 95%)
            _settingItems.Add(SettingItem.CreateSlider(
                "Receptor Position",
                0.1f, 0.95f,
                () => _settings.ReceptorPosition,
                (val) => { 
                    _settings.ReceptorPosition = val; 
                    _settings.Save(); 
                    Core.SettingsService.Instance.ReceptorPosition = val;
                },
                () => $"{(int)(_settings.ReceptorPosition * 100)}%"
            ));

            // 4. Volumen Maestro (Slider 0% a 100%)
            _settingItems.Add(SettingItem.CreateSlider(
                "Master Volume",
                0.0f, 1.0f,
                () => _settings.MasterVolume,
                (val) => { 
                    _settings.MasterVolume = val; 
                    _settings.Save(); 
                    _game.AudioEngine.Volume = val;
                    Core.SettingsService.Instance.MasterVolume = val;
                },
                () => $"{(int)(_settings.MasterVolume * 100)}%"
            ));

            // 5. Volumen de la Música (Slider 0% a 100%)
            _settingItems.Add(SettingItem.CreateSlider(
                "Music Volume",
                0.0f, 1.0f,
                () => _settings.MusicVolume,
                (val) => { 
                    _settings.MusicVolume = val; 
                    _settings.Save(); 
                    _game.AudioEngine.StreamVolume = val;
                    Core.SettingsService.Instance.MusicVolume = val;
                },
                () => $"{(int)(_settings.MusicVolume * 100)}%"
            ));

            // 6. Volumen de los Hitsounds (Slider 0% a 100%)
            _settingItems.Add(SettingItem.CreateSlider(
                "Hitsound Volume",
                0.0f, 1.0f,
                () => _settings.HitsoundVolume,
                (val) => { 
                    _settings.HitsoundVolume = val; 
                    _settings.Save(); 
                    Core.SettingsService.Instance.HitsoundVolume = val;
                },
                () => $"{(int)(_settings.HitsoundVolume * 100)}%"
            ));

            // 7. Offset de Audio Global (Slider -250ms a 250ms)
            _settingItems.Add(SettingItem.CreateSlider(
                "Global Audio Offset",
                -250f, 250f,
                () => (float)_settings.GlobalAudioOffset,
                (val) => { 
                    _settings.GlobalAudioOffset = (double)Math.Round(val); 
                    _settings.Save(); 
                    Core.SettingsService.Instance.GlobalOffsetMs = (int)Math.Round(val);
                },
                () => $"{_settings.GlobalAudioOffset:F0}ms"
            ));

            // 8. Opacidad del Fondo / Dim (Slider 0% a 100%)
            _settingItems.Add(SettingItem.CreateSlider(
                "Background Dim",
                0.0f, 1.0f,
                () => _settings.BackgroundDim,
                (val) => { _settings.BackgroundDim = val; _settings.Save(); },
                () => $"{(int)(_settings.BackgroundDim * 100)}%"
            ));

            // 9. Drenaje de Vida (Toggle ON/OFF)
            _settingItems.Add(SettingItem.CreateToggle(
                "Health Drain",
                () => _settings.HealthDrainEnabled ? "ON" : "OFF (Practice)",
                () => { _settings.HealthDrainEnabled = !_settings.HealthDrainEnabled; _settings.Save(); }
            ));

            // 10. Mostrar FPS (Toggle ON/OFF)
            _settingItems.Add(SettingItem.CreateToggle(
                "Show FPS",
                () => _settings.ShowFPS ? "ON" : "OFF",
                () => { _settings.ShowFPS = !_settings.ShowFPS; _settings.Save(); }
            ));

            // 11. Sincronización Vertical (Toggle ON/OFF)
            _settingItems.Add(SettingItem.CreateToggle(
                "VSync",
                () => _settings.VSync ? "ON" : "OFF",
                () => { 
                    _settings.VSync = !_settings.VSync; 
                    _settings.Save(); 
                    _game.Graphics.SynchronizeWithVerticalRetrace = _settings.VSync;
                    _game.Graphics.ApplyChanges();
                }
            ));

            // 12. Skin seleccionada (Sólo lectura visual)
            _settingItems.Add(SettingItem.CreateToggle(
                "Current Skin",
                () => Path.GetFileName(_settings.SkinDirectory) ?? "Default",
                () => {}
            ));

            _selectedIndex = 0;
            _bindingKeyCount = _inputManager.ActiveKeyCount;
            _previousKeyState = Keyboard.GetState();
            _prevMouseState = Mouse.GetState();
            _hoverProgressArray = new float[_settingItems.Count];

            Console.WriteLine("[SettingsScreen] Settings screen initialized");
        }

        public void Update(GameTime gameTime)
        {
            var currentKeyState = Keyboard.GetState();
            var currentMouseState = Mouse.GetState();

            if (!_game.IsActive)
            {
                _previousKeyState = currentKeyState;
                _prevMouseState = currentMouseState;
                return;
            }

            // 0. Actualizar transiciones de hover/selección (LERP a 60fps)
            float elapsed = (float)gameTime.ElapsedGameTime.TotalSeconds;
            if (_hoverProgressArray.Length != _settingItems.Count)
            {
                _hoverProgressArray = new float[_settingItems.Count];
            }

            int sw = _game.WindowWidth;
            int sh = _game.WindowHeight;
            int lx = 60;
            int lw = (int)(sw * 0.52f);
            int py = 140;
            int ph = sh - py - 120;
            int ih = 54;

            for (int i = 0; i < _settingItems.Count; i++)
            {
                bool active = (i == _selectedIndex);
                int yPos = py + 50 + (i - _scrollOffset) * ih;
                var itemRect = new Rectangle(lx + 5, yPos - 5, lw - 10, ih - 6);
                if (itemRect.Contains(currentMouseState.X, currentMouseState.Y))
                {
                    active = true;
                }

                float target = active ? 1.0f : 0.0f;
                _hoverProgressArray[i] += (target - _hoverProgressArray[i]) * elapsed * 10f;
            }

            // 1. Si estamos esperando tecla para Keybind
            if (_isBindingKey)
            {
                var pressedKeys = currentKeyState.GetPressedKeys();
                foreach (var key in pressedKeys)
                {
                    if (_previousKeyState.IsKeyUp(key))
                    {
                        if (key == Keys.Escape)
                        {
                            _isBindingKey = false;
                        }
                        else
                        {
                            _inputManager.SetKeybind(_bindingKeyCount, _bindingColumnIndex, key);
                            _isBindingKey = false;
                            _settings.CustomKeybinds = _inputManager.SerializeCustomKeybinds();
                            _settings.Save();
                            Console.WriteLine($"[SettingsScreen] Bound {_bindingKeyCount}K Col {_bindingColumnIndex} to {key} (saved)");
                        }
                        break;
                    }
                }
                _previousKeyState = currentKeyState;
                return;
            }

            // 2. ESC para salir al Menú Principal
            if (IsKeyPressed(currentKeyState, Keys.Escape))
            {
                var mainMenu = new MainMenuScreen(_game, _inputManager, _fontRenderer);
                _game.StateManager.ChangeState(GameState.MainMenu, mainMenu);
                return;
            }

            int screenWidth = _game.WindowWidth;
            int screenHeight = _game.WindowHeight;

            int leftX = 60;
            int leftW = (int)(screenWidth * 0.52f);
            int rightX = leftX + leftW + 40;
            int rightW = screenWidth - rightX - 60;

            int panelY = 140;
            int panelH = screenHeight - panelY - 120;

            int itemHeight = 54;
            int maxVisibleItems = panelH / itemHeight;

            // 3. Scroll mediante rueda del mouse
            if (currentMouseState.ScrollWheelValue != _prevMouseState.ScrollWheelValue)
            {
                int scrollDelta = currentMouseState.ScrollWheelValue - _prevMouseState.ScrollWheelValue;
                if (scrollDelta > 0)
                    _scrollOffset = Math.Max(0, _scrollOffset - 1);
                else
                    _scrollOffset = Math.Min(Math.Max(0, _settingItems.Count - maxVisibleItems), _scrollOffset + 1);
            }

            // 4. Manejar arrastre continuo de Sliders
            if (_draggingIndex != -1)
            {
                if (currentMouseState.LeftButton == ButtonState.Pressed)
                {
                    var item = _settingItems[_draggingIndex];
                    if (item.IsSlider && item.SliderBounds != null)
                    {
                        var bounds = item.SliderBounds.Value;
                        float percent = Math.Clamp((float)(currentMouseState.X - bounds.X) / bounds.Width, 0f, 1f);
                        float newValue = item.MinSliderValue + percent * (item.MaxSliderValue - item.MinSliderValue);
                        item.OnSliderChanged?.Invoke(newValue);
                    }
                }
                else
                {
                    _draggingIndex = -1;
                }
            }

            // 5. Procesar Clics del Mouse
            if (currentMouseState.LeftButton == ButtonState.Pressed && _prevMouseState.LeftButton == ButtonState.Released)
            {
                int startIndex = _scrollOffset;
                int endIndex = Math.Min(_scrollOffset + maxVisibleItems, _settingItems.Count);

                // Clic en la columna izquierda (Ajustes)
                for (int i = startIndex; i < endIndex; i++)
                {
                    var item = _settingItems[i];
                    int yPos = panelY + 50 + (i - _scrollOffset) * itemHeight;

                    var itemRect = new Rectangle(leftX, yPos - 5, leftW, itemHeight - 6);
                    if (itemRect.Contains(currentMouseState.X, currentMouseState.Y))
                    {
                        _selectedIndex = i;

                        if (item.IsSlider)
                        {
                            var sliderRect = new Rectangle(leftX + leftW - 250, yPos + 8, 160, 20);
                            item.SliderBounds = sliderRect;
                            if (sliderRect.Contains(currentMouseState.X, currentMouseState.Y) || currentMouseState.X >= sliderRect.X)
                            {
                                _draggingIndex = i;
                                float percent = Math.Clamp((float)(currentMouseState.X - sliderRect.X) / sliderRect.Width, 0f, 1f);
                                float newValue = item.MinSliderValue + percent * (item.MaxSliderValue - item.MinSliderValue);
                                item.OnSliderChanged?.Invoke(newValue);
                            }
                        }
                        else if (item.IsToggle)
                        {
                            item.OnToggle?.Invoke();
                        }
                    }
                }

                // Clic en la columna derecha (Keybinds)
                // Selectores de Key Count (4K, 5K, 6K, 7K)
                int keyCountsY = panelY + 60;
                int btnW = 70;
                int btnH = 36;
                int spacing = 12;
                int startBtnX = rightX + (rightW - (4 * btnW + 3 * spacing)) / 2;

                for (int k = 4; k <= 7; k++)
                {
                    int btnX = startBtnX + (k - 4) * (btnW + spacing);
                    var btnRect = new Rectangle(btnX, keyCountsY, btnW, btnH);
                    if (btnRect.Contains(currentMouseState.X, currentMouseState.Y))
                    {
                        _bindingKeyCount = k;
                        break;
                    }
                }

                // Selección de columna para rebind
                int keyRowsY = keyCountsY + 65;
                int rowH = 34;
                if (_inputManager.Keybinds.TryGetValue(_bindingKeyCount, out Keys[]? keys))
                {
                    for (int c = 0; c < _bindingKeyCount; c++)
                    {
                        int rowY = keyRowsY + c * (rowH + 8);
                        var bindBtnRect = new Rectangle(rightX + 180, rowY, rightW - 200, rowH);
                        if (bindBtnRect.Contains(currentMouseState.X, currentMouseState.Y))
                        {
                            _isBindingKey = true;
                            _bindingColumnIndex = c;
                            break;
                        }
                    }
                }
            }

            // 6. Navegación clásica por teclado de respaldo
            if (IsKeyPressed(currentKeyState, Keys.Down) || IsKeyPressed(currentKeyState, Keys.S))
            {
                _selectedIndex = (_selectedIndex + 1) % _settingItems.Count;
                if (_selectedIndex >= _scrollOffset + maxVisibleItems)
                    _scrollOffset = _selectedIndex - maxVisibleItems + 1;
            }
            if (IsKeyPressed(currentKeyState, Keys.Up) || IsKeyPressed(currentKeyState, Keys.W))
            {
                _selectedIndex--;
                if (_selectedIndex < 0) _selectedIndex = _settingItems.Count - 1;
                if (_selectedIndex < _scrollOffset) _scrollOffset = _selectedIndex;
            }
            if (IsKeyPressed(currentKeyState, Keys.Left) || IsKeyPressed(currentKeyState, Keys.A))
            {
                var item = _settingItems[_selectedIndex];
                if (item.IsSlider)
                {
                    float currentVal = item.GetSliderValue();
                    float step = (item.MaxSliderValue - item.MinSliderValue) / 20.0f;
                    if (item.Name == "Global Audio Offset") step = 1.0f;
                    item.OnSliderChanged?.Invoke(Math.Max(item.MinSliderValue, currentVal - step));
                }
                else if (item.IsToggle)
                {
                    item.OnToggle?.Invoke();
                }
            }
            if (IsKeyPressed(currentKeyState, Keys.Right) || IsKeyPressed(currentKeyState, Keys.D))
            {
                var item = _settingItems[_selectedIndex];
                if (item.IsSlider)
                {
                    float currentVal = item.GetSliderValue();
                    float step = (item.MaxSliderValue - item.MinSliderValue) / 20.0f;
                    if (item.Name == "Global Audio Offset") step = 1.0f;
                    item.OnSliderChanged?.Invoke(Math.Min(item.MaxSliderValue, currentVal + step));
                }
                else if (item.IsToggle)
                {
                    item.OnToggle?.Invoke();
                }
            }

            _previousKeyState = currentKeyState;
            _prevMouseState = currentMouseState;
            _inputManager.Update(gameTime.TotalGameTime.TotalMilliseconds);
        }

        public void Draw(GameTime gameTime)
        {
            _game.GraphicsDevice.Clear(new Color(15, 15, 20));

            if (_fontRenderer == null)
                return;

            var spriteBatch = _game.SpriteBatch;
            spriteBatch.Begin();

            int screenWidth = _game.WindowWidth;
            int screenHeight = _game.WindowHeight;

            // 1. Dibujar Cabecera
            _fontRenderer.SetFontSize(48);
            _fontRenderer.DrawTextCentered("SETTINGS", new Vector2(screenWidth / 2, 60), Color.HotPink, 1.0f);
            
            _fontRenderer.SetFontSize(18);
            _fontRenderer.DrawTextCentered("Drag sliders or click keybinds to customize. Press ESC to return.", new Vector2(screenWidth / 2, 105), Color.Gray, 1.0f);

            int leftX = 60;
            int leftW = (int)(screenWidth * 0.52f);
            int rightX = leftX + leftW + 40;
            int rightW = screenWidth - rightX - 60;

            int panelY = 140;
            int panelH = screenHeight - panelY - 120;

            // 2. Columna Izquierda: Configuración de Sistema y Juego
            var leftRect = new Rectangle(leftX, panelY, leftW, panelH);
            _fontRenderer.DrawBox(leftRect, new Color(25, 25, 30, 200));
            _fontRenderer.DrawBoxBorder(leftRect, new Color(50, 50, 60), 2);

            _fontRenderer.SetFontSize(24);
            _fontRenderer.DrawText("SYSTEM & GAMEPLAY", new Vector2(leftX + 25, panelY + 15), Color.HotPink, 1.0f);

            int itemHeight = 54;
            int maxVisibleItems = panelH / itemHeight;
            int startIndex = _scrollOffset;
            int endIndex = Math.Min(_scrollOffset + maxVisibleItems, _settingItems.Count);

            var mouseState = Mouse.GetState();

            for (int i = startIndex; i < endIndex; i++)
            {
                var item = _settingItems[i];
                int yPos = panelY + 50 + (i - _scrollOffset) * itemHeight;
                float progress = i < _hoverProgressArray.Length ? _hoverProgressArray[i] : 0.0f;

                var itemRect = new Rectangle(leftX + 5, yPos - 5, leftW - 10, itemHeight - 6);

                // Fondo animado y bordes con LERP
                if (progress > 0.01f)
                {
                    Color itemBgColor = Color.Lerp(Color.Transparent, new Color(75, 20, 58, 120), progress);
                    _fontRenderer.DrawBox(itemRect, itemBgColor);

                    Color borderCol = Color.Lerp(Color.Transparent, Color.HotPink, progress);
                    _fontRenderer.DrawBoxBorder(itemRect, borderCol, 1);
                }

                // Deslizar texto animado ligeramente a la derecha (estilo osu!)
                float textOffset = progress * 12f;
                Color nameColor = Color.Lerp(new Color(200, 200, 210), Color.White, progress);
                _fontRenderer.SetFontSize(20);
                _fontRenderer.DrawText(item.Name, new Vector2(leftX + 25 + textOffset, yPos), nameColor, 1.0f);

                if (item.IsSlider)
                {
                    var sliderRect = new Rectangle(leftX + leftW - 250, yPos + 8, 160, 20);
                    item.SliderBounds = sliderRect; // Guardar límites para el arrastre
                    float val = item.GetSliderValue();
                    DrawSlider(sliderRect, val, item.MinSliderValue, item.MaxSliderValue, item.GetValue(), progress > 0.5f);
                }
                else if (item.IsToggle)
                {
                    var toggleRect = new Rectangle(leftX + leftW - 120, yPos + 2, 90, 26);
                    bool isON = item.GetValue().Contains("ON");
                    Color pillColor = isON ? Color.HotPink : new Color(50, 50, 60);
                    _fontRenderer.DrawBox(toggleRect, pillColor);
                    _fontRenderer.DrawBoxBorder(toggleRect, Color.White * 0.6f, 1);
                    
                    _fontRenderer.SetFontSize(16);
                    _fontRenderer.DrawTextCentered(item.GetValue(), toggleRect.X + toggleRect.Width / 2, toggleRect.Y + toggleRect.Height / 2, Color.White);
                }
            }

            // Scrollbar lateral para columna izquierda
            if (_settingItems.Count > maxVisibleItems)
            {
                int scrollbarHeight = Math.Max(20, (panelH * maxVisibleItems) / _settingItems.Count);
                float scrollPercent = (float)_scrollOffset / (_settingItems.Count - maxVisibleItems);
                int scrollbarY = panelY + 10 + (int)((panelH - scrollbarHeight - 20) * scrollPercent);

                var scrollbarRect = new Rectangle(leftX + leftW - 8, scrollbarY, 5, scrollbarHeight);
                _fontRenderer.DrawBox(scrollbarRect, Color.HotPink);
            }

            // 3. Columna Derecha: Re-mapeador de Teclas Visual
            var rightRect = new Rectangle(rightX, panelY, rightW, panelH);
            _fontRenderer.DrawBox(rightRect, new Color(25, 25, 30, 200));
            _fontRenderer.DrawBoxBorder(rightRect, new Color(50, 50, 60), 2);

            _fontRenderer.SetFontSize(24);
            _fontRenderer.DrawText("KEYBINDS MAPPER", new Vector2(rightX + 25, panelY + 15), Color.HotPink, 1.0f);

            // Botones de selección de K (4K, 5K, 6K, 7K)
            int keyCountsY = panelY + 60;
            int btnW = 70;
            int btnH = 36;
            int spacing = 12;
            int startBtnX = rightX + (rightW - (4 * btnW + 3 * spacing)) / 2;

            for (int k = 4; k <= 7; k++)
            {
                int btnX = startBtnX + (k - 4) * (btnW + spacing);
                var btnRect = new Rectangle(btnX, keyCountsY, btnW, btnH);
                bool isBtnHovered = btnRect.Contains(mouseState.X, mouseState.Y);
                bool isBtnActive = _bindingKeyCount == k;

                Color btnBgColor = isBtnActive ? Color.HotPink : (isBtnHovered ? new Color(60, 60, 75) : new Color(40, 40, 50));
                _fontRenderer.DrawBox(btnRect, btnBgColor);
                _fontRenderer.DrawBoxBorder(btnRect, isBtnActive ? Color.White : Color.Gray, 1);
                
                _fontRenderer.SetFontSize(18);
                _fontRenderer.DrawTextCentered($"{k}K", btnX + btnW / 2, keyCountsY + btnH / 2, Color.White);
            }

            // Lista de teclas para el K seleccionado
            int keyRowsY = keyCountsY + 65;
            int rowH = 34;
            if (_inputManager.Keybinds.TryGetValue(_bindingKeyCount, out Keys[]? keys))
            {
                for (int c = 0; c < _bindingKeyCount; c++)
                {
                    int rowY = keyRowsY + c * (rowH + 8);
                    
                    _fontRenderer.SetFontSize(18);
                    _fontRenderer.DrawText($"Column {c + 1}:", new Vector2(rightX + 30, rowY + 6), Color.White, 1.0f);

                    var bindBtnRect = new Rectangle(rightX + 180, rowY, rightW - 200, rowH);
                    bool isBindHovered = bindBtnRect.Contains(mouseState.X, mouseState.Y);
                    bool isBindingThis = _isBindingKey && _bindingColumnIndex == c;

                    Color bindBtnColor = isBindingThis ? Color.Yellow * 0.3f : (isBindHovered ? new Color(50, 50, 60) : new Color(30, 30, 40));
                    Color borderCol = isBindingThis ? Color.Yellow : (isBindHovered ? Color.HotPink : Color.Gray);
                    _fontRenderer.DrawBox(bindBtnRect, bindBtnColor);
                    _fontRenderer.DrawBoxBorder(bindBtnRect, borderCol, 1);

                    _fontRenderer.SetFontSize(18);
                    string keyText = isBindingThis ? "[ Press key... ]" : keys[c].ToString();
                    _fontRenderer.DrawTextCentered(keyText, bindBtnRect.X + bindBtnRect.Width / 2, bindBtnRect.Y + bindBtnRect.Height / 2, isBindingThis ? Color.Yellow : Color.White);
                }
            }

            // 4. Footer y Consejos de Importación
            _fontRenderer.SetFontSize(16);
            string footerTip = "Skins/Beatmaps: Drag & Drop .osk / .osz files anytime to import instantly.";
            _fontRenderer.DrawTextCentered(footerTip, new Vector2(screenWidth / 2, screenHeight - 60), new Color(130, 130, 140), 1.0f);

            _fontRenderer.SetFontSize(14);
            _fontRenderer.DrawTextCentered("Nullscent Practice Client", new Vector2(screenWidth / 2, screenHeight - 30), new Color(90, 90, 100), 1.0f);

            spriteBatch.End();
        }

        private void DrawSlider(Rectangle bounds, float value, float min, float max, string displayValue, bool highlighted)
        {
            // Fondo de la pista
            var trackRect = new Rectangle(bounds.X, bounds.Y + bounds.Height / 2 - 3, bounds.Width, 6);
            _fontRenderer.DrawBox(trackRect, new Color(50, 50, 60));

            // Relleno progresivo
            float percent = Math.Clamp((value - min) / (max - min), 0f, 1f);
            int fillW = (int)(bounds.Width * percent);
            var fillRect = new Rectangle(bounds.X, bounds.Y + bounds.Height / 2 - 3, fillW, 6);
            _fontRenderer.DrawBox(fillRect, Color.HotPink);

            // Botón/Knob
            int knobX = bounds.X + fillW;
            var knobRect = new Rectangle(knobX - 6, bounds.Y + bounds.Height / 2 - 8, 12, 16);
            _fontRenderer.DrawBox(knobRect, Color.White);
            _fontRenderer.DrawBoxBorder(knobRect, highlighted ? Color.HotPink : Color.Gray, 1);

            // Texto a la derecha
            _fontRenderer.SetFontSize(16);
            _fontRenderer.DrawText(displayValue, new Vector2(bounds.X + bounds.Width + 10, bounds.Y + bounds.Height / 2 - 8), Color.White, 1.0f);
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
    /// Estructura para almacenar items de configuración (Sliders y Toggles) en el panel.
    /// </summary>
    internal class SettingItem
    {
        public string Name { get; }
        public Func<string> GetValue { get; }
        public bool IsSlider { get; }
        public float MinSliderValue { get; }
        public float MaxSliderValue { get; }
        public Func<float> GetSliderValue { get; }
        public Action<float> OnSliderChanged { get; }
        public bool IsToggle { get; }
        public Action OnToggle { get; }
        public Rectangle? SliderBounds { get; set; }

        public SettingItem(
            string name, 
            Func<string> getValue, 
            bool isSlider, 
            float min, 
            float max, 
            Func<float> getSlider, 
            Action<float> onSliderChanged, 
            bool isToggle, 
            Action onToggle)
        {
            Name = name;
            GetValue = getValue;
            IsSlider = isSlider;
            MinSliderValue = min;
            MaxSliderValue = max;
            GetSliderValue = getSlider;
            OnSliderChanged = onSliderChanged;
            IsToggle = isToggle;
            OnToggle = onToggle;
        }

        public static SettingItem CreateSlider(string name, float min, float max, Func<float> getSlider, Action<float> onSliderChanged, Func<string> getValue)
        {
            return new SettingItem(name, getValue, true, min, max, getSlider, onSliderChanged, false, () => {});
        }

        public static SettingItem CreateToggle(string name, Func<string> getValue, Action onToggle)
        {
            return new SettingItem(name, getValue, false, 0, 0, () => 0, _ => {}, true, onToggle);
        }
    }
}
