#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Nullscent.Config;
using Nullscent.Core;
using Nullscent.UI;
using Nullscent.Skin;
using System;
using System.Collections.Generic;
using System.IO;

namespace Nullscent.Screens
{
    /// <summary>
    /// Panel global de configuración deslizable (estilo McOsu / osu!lazer).
    /// Se activa con Ctrl+O y se desliza desde la izquierda.
    /// Bloquea el input de pantallas inferiores cuando está completamente abierto.
    /// </summary>
    public class SettingsOverlay
    {
        private readonly Game1 _game;
        private readonly InputManager _inputManager;
        private readonly TrueTypeFontRenderer _fontRenderer;
        private readonly GameSettings _settings;

        private readonly List<OverlaySettingItem> _settingItems = new();
        private OverlaySettingItem? _skinSelector;
        private List<(string Name, string? Path)> _skins = new();
        private int _selectedIndex = 0;
        private int _scrollOffset = 0;

        private KeyboardState _previousKeyState;
        private MouseState _prevMouseState;
        private float[] _hoverProgressArray = Array.Empty<float>();

        // Animación de deslizamiento
        public bool IsOpen { get; set; } = false;
        private float _currentX = -450f; // Ancho del panel es 420px + margen
        private const float PANEL_WIDTH = 420f;

        // Arrastre de sliders
        private int _draggingIndex = -1;

        // Configuración visual de Keybinds en el panel
        private bool _isBindingKey;
        private int _bindingKeyCount = 4;
        private int _bindingColumnIndex = -1;
        private bool _showKeybinds = false;
        private float _keybindPanelProgress = 0.0f;

        public bool IsFullyOpen => IsOpen && Math.Abs(_currentX) < 1.0f;
        public bool IsVisible => _currentX > -PANEL_WIDTH + 1.0f;

        public SettingsOverlay(Game1 game, InputManager inputManager, TrueTypeFontRenderer fontRenderer)
        {
            _game = game;
            _inputManager = inputManager;
            _fontRenderer = fontRenderer;
            _settings = game.Settings;
            _game.SkinCatalog.CatalogChanged += RefreshSkinSelector;
        }

        private void RefreshSkinSelector()
        {
            _skins = new List<(string Name, string? Path)> { ("Nullscent (interna)", null) };
            foreach (var entry in _game.SkinCatalog.Entries)
            {
                string prefix = entry.Status == SkinValidity.Invalid ? "[inválida] "
                    : entry.Status == SkinValidity.Partial ? "[parcial] " : "";
                _skins.Add((prefix + Path.GetFileName(entry.Directory), entry.Directory));
            }
            if (_skinSelector != null) _skinSelector.SelectorOptions = _skins.ConvertAll(s => s.Name).ToArray();
        }

        public void Initialize()
        {
            Console.WriteLine("[SettingsOverlay] Initializing global settings panel...");

            _settingItems.Clear();
            RefreshSkinSelector();

            // --- CATEGORÍA: GAMEPLAY ---
            _settingItems.Add(OverlaySettingItem.CreateHeader("GAMEPLAY"));

            _settingItems.Add(OverlaySettingItem.CreateSlider(
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

            _settingItems.Add(OverlaySettingItem.CreateToggle(
                "Down Scroll",
                () => _settings.DownScroll ? "ON" : "OFF",
                () => { 
                    _settings.DownScroll = !_settings.DownScroll; 
                    _settings.Save(); 
                    Core.SettingsService.Instance.DownScroll = _settings.DownScroll;
                }
            ));

            _settingItems.Add(OverlaySettingItem.CreateToggle(
                "Scale Scroll with BPM",
                () => _settings.ScaleScrollSpeedWithBPM ? "ON" : "OFF",
                () => { 
                    _settings.ScaleScrollSpeedWithBPM = !_settings.ScaleScrollSpeedWithBPM; 
                    _settings.Save(); 
                    Core.SettingsService.Instance.ScaleScrollSpeedWithBPM = _settings.ScaleScrollSpeedWithBPM;
                }
            ));

            _settingItems.Add(OverlaySettingItem.CreateSlider(
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

            _settingItems.Add(OverlaySettingItem.CreateSlider(
                "Background Dim",
                0.0f, 1.0f,
                () => _settings.BackgroundDim,
                (val) => { _settings.BackgroundDim = val; _settings.Save(); },
                () => $"{(int)(_settings.BackgroundDim * 100)}%"
            ));

            _settingItems.Add(OverlaySettingItem.CreateToggle(
                "Health Drain",
                () => _settings.HealthDrainEnabled ? "ON" : "OFF (Practice)",
                () => { _settings.HealthDrainEnabled = !_settings.HealthDrainEnabled; _settings.Save(); }
            ));

            // --- CATEGORÍA: AUDIO ---
            _settingItems.Add(OverlaySettingItem.CreateHeader("AUDIO"));

            _settingItems.Add(OverlaySettingItem.CreateSlider(
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

            _settingItems.Add(OverlaySettingItem.CreateSlider(
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

            _settingItems.Add(OverlaySettingItem.CreateSlider(
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

            _settingItems.Add(OverlaySettingItem.CreateSlider(
                "Global Offset",
                -250f, 250f,
                () => (float)_settings.GlobalAudioOffset,
                (val) => { 
                    _settings.GlobalAudioOffset = (double)Math.Round(val); 
                    _settings.Save(); 
                    Core.SettingsService.Instance.GlobalOffsetMs = (int)Math.Round(val);
                },
                () => $"{_settings.GlobalAudioOffset:F0}ms"
            ));

            // --- CATEGORÍA: SYSTEM ---
            _settingItems.Add(OverlaySettingItem.CreateHeader("SYSTEM"));

            _settingItems.Add(OverlaySettingItem.CreateToggle(
                "Fullscreen",
                () => _settings.Fullscreen ? "ON" : "OFF",
                () => {
                    _game.ApplyResolution(_settings.WindowWidth, _settings.WindowHeight, !_settings.Fullscreen);
                }
            ));

            // Selector de Resolución 16:9
            var predefined = new (int Width, int Height)[]
            {
                (1920, 1080),
                (1600, 900),
                (1366, 768),
                (1360, 768),
                (1280, 720)
            };
            string[] resNames = new string[predefined.Length];
            for (int i = 0; i < predefined.Length; i++)
            {
                resNames[i] = $"{predefined[i].Width}x{predefined[i].Height}";
            }

            _settingItems.Add(OverlaySettingItem.CreateSelector(
                "Resolution",
                resNames,
                () => {
                    for (int i = 0; i < predefined.Length; i++)
                    {
                        if (predefined[i].Width == _settings.WindowWidth && predefined[i].Height == _settings.WindowHeight)
                        {
                            return i;
                        }
                    }
                    return 0;
                },
                (index) => {
                    if (index >= 0 && index < predefined.Length)
                    {
                        var res = predefined[index];
                        _game.ApplyResolution(res.Width, res.Height, _settings.Fullscreen);
                    }
                }
            ));

            _settingItems.Add(OverlaySettingItem.CreateToggle(
                "Show FPS",
                () => _settings.ShowFPS ? "ON" : "OFF",
                () => { _settings.ShowFPS = !_settings.ShowFPS; _settings.Save(); }
            ));

            _settingItems.Add(OverlaySettingItem.CreateToggle(
                "VSync",
                () => _settings.VSync ? "ON" : "OFF",
                () => { 
                    _settings.VSync = !_settings.VSync; 
                    _settings.Save(); 
                    _game.Graphics.SynchronizeWithVerticalRetrace = _settings.VSync;
                    _game.Graphics.ApplyChanges();
                }
            ));

            // Selector de Skins
            _skinSelector = OverlaySettingItem.CreateSelector(
                "Current Skin",
                _skins.ConvertAll(s => s.Name).ToArray(),
                () => {
                    string currentFull = _game.SkinManager.CurrentSkinDirectory;
                    for (int i = 1; i < _skins.Count; i++)
                    {
                        if (string.Equals(_skins[i].Path, currentFull, StringComparison.OrdinalIgnoreCase))
                        {
                            return i;
                        }
                    }
                    return 0;
                },
                (index) => {
                    if (index >= 0 && index < _skins.Count)
                    {
                        _game.SelectSkin(_skins[index].Path);
                    }
                }
            );
            _settingItems.Add(_skinSelector);

            _settingItems.Add(OverlaySettingItem.CreateToggle(
                "HD Skin Assets",
                () => _settings.HighResolutionSkins ? "ON" : "OFF",
                () => {
                    _settings.HighResolutionSkins = !_settings.HighResolutionSkins;
                    _settings.Save();
                    Core.SettingsService.Instance.HighResolutionSkins = _settings.HighResolutionSkins;
                    string current = _game.SkinManager.CurrentSkinDirectory;
                    if (current.Length > 0) _game.SkinManager.LoadSkin(current);
                }
            ));

            _settingItems.Add(OverlaySettingItem.CreateToggle(
                "Override Hitsounds",
                () => _settings.OverrideMapHitsounds ? "ON" : "OFF",
                () => {
                    _settings.OverrideMapHitsounds = !_settings.OverrideMapHitsounds;
                    _settings.Save();
                    Core.SettingsService.Instance.OverrideMapHitsounds = _settings.OverrideMapHitsounds;
                }
            ));

            _settingItems.Add(OverlaySettingItem.CreateToggle(
                "Configure Keys",
                () => "CONFIGURE...",
                () => { _showKeybinds = true; }
            ));

            _settingItems.Add(OverlaySettingItem.CreateSlider(
                "Note Height Scale",
                0.5f, 2.0f,
                () => _settings.SkinNoteHeightScale,
                (val) => {
                    _settings.SkinNoteHeightScale = val;
                    _settings.Save();
                    Core.SettingsService.Instance.SkinNoteHeightScale = val;
                },
                () => $"{_settings.SkinNoteHeightScale:F2}x"
            ));

            _settingItems.Add(OverlaySettingItem.CreateSlider(
                "Receptor Height Scale",
                0.5f, 2.0f,
                () => _settings.SkinReceptorHeightScale,
                (val) => {
                    _settings.SkinReceptorHeightScale = val;
                    _settings.Save();
                    Core.SettingsService.Instance.SkinReceptorHeightScale = val;
                },
                () => $"{_settings.SkinReceptorHeightScale:F2}x"
            ));

            _bindingKeyCount = _inputManager.ActiveKeyCount;
            _previousKeyState = Keyboard.GetState();
            _prevMouseState = Mouse.GetState();
            _hoverProgressArray = new float[_settingItems.Count];
        }

        public void Toggle()
        {
            IsOpen = !IsOpen;
            if (IsOpen)
            {
                _bindingKeyCount = _inputManager.ActiveKeyCount;
                _prevMouseState = Mouse.GetState();
            }
        }

        public void Update(GameTime gameTime)
        {
            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

            // Transición del panel deslizable
            float targetX = IsOpen ? 0f : -PANEL_WIDTH - 20f;
            _currentX += (targetX - _currentX) * 0.18f;

            // Transición del panel de Keybinds Mapper
            float targetProgress = _showKeybinds ? 1.0f : 0.0f;
            _keybindPanelProgress += (targetProgress - _keybindPanelProgress) * dt * 12f;

            if (!IsVisible) return;

            var currentKeyState = Keyboard.GetState();
            var currentMouseState = Mouse.GetState();

            // ESC para cerrar el panel
            if (IsKeyPressed(currentKeyState, Keys.Escape) && !_isBindingKey)
            {
                if (_showKeybinds)
                {
                    _showKeybinds = false;
                }
                else
                {
                    IsOpen = false;
                }
                _draggingIndex = -1;
            }

            // Manejar captura de tecla para Keybind
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
                            Console.WriteLine($"[SettingsOverlay] Bound {_bindingKeyCount}K Col {_bindingColumnIndex} to {key}");
                        }
                        break;
                    }
                }
                _previousKeyState = currentKeyState;
                return;
            }

            int py = 80;
            int ph = _game.WindowHeight - py - 100;
            int ih = 44;
            int maxVisibleItems = ph / ih;

            int lx = (int)_currentX + 20;
            int lw = (int)PANEL_WIDTH - 40;

            // Si el mapper de keybinds está visible en pantalla (progreso > 0.5), el scroll y hover normal se desactiva
            if (_keybindPanelProgress <= 0.5f)
            {
                // Actualizar transiciones de hover normales
                if (_hoverProgressArray.Length != _settingItems.Count)
                {
                    _hoverProgressArray = new float[_settingItems.Count];
                }

                for (int i = 0; i < _settingItems.Count; i++)
                {
                    if (_settingItems[i].IsHeader) continue;

                    bool active = (i == _selectedIndex);
                    int yPos = py + 30 + (i - _scrollOffset) * ih;
                    var itemRect = new Rectangle(lx, yPos - 5, lw, ih - 6);

                    if (itemRect.Contains(currentMouseState.X, currentMouseState.Y))
                    {
                        active = true;
                    }

                    float target = active ? 1.0f : 0.0f;
                    _hoverProgressArray[i] += (target - _hoverProgressArray[i]) * dt * 10f;
                }

                // Scroll con rueda de mouse para el listado estándar
                if (currentMouseState.ScrollWheelValue != _prevMouseState.ScrollWheelValue)
                {
                    int scrollDelta = currentMouseState.ScrollWheelValue - _prevMouseState.ScrollWheelValue;
                    int maxScroll = Math.Max(0, _settingItems.Count - maxVisibleItems + 1);
                    if (scrollDelta > 0)
                        _scrollOffset = Math.Max(0, _scrollOffset - 1);
                    else
                        _scrollOffset = Math.Min(maxScroll, _scrollOffset + 1);
                }

                // Manejar arrastre continuo de Sliders
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
            }

            // Procesar Clics del mouse
            if (currentMouseState.LeftButton == ButtonState.Pressed && _prevMouseState.LeftButton == ButtonState.Released)
            {
                if (_keybindPanelProgress > 0.5f)
                {
                    // === Clics en el Sub-Panel de Keybinds Mapper ===
                    int kX = lx;
                    int kY = py + 30;

                    // Botón "Back"
                    var backRect = new Rectangle(kX + 10, kY, 80, 26);
                    if (backRect.Contains(currentMouseState.X, currentMouseState.Y))
                    {
                        _showKeybinds = false;
                        _prevMouseState = currentMouseState;
                        return;
                    }

                    // Selector de K (4K - 7K)
                    int selectorY = kY + 50;
                    int btnW = 55;
                    int btnH = 28;
                    int spacing = 8;
                    int startBtnX = kX + (lw - (4 * btnW + 3 * spacing)) / 2;

                    for (int k = 4; k <= 7; k++)
                    {
                        var btnRect = new Rectangle(startBtnX + (k - 4) * (btnW + spacing), selectorY, btnW, btnH);
                        if (btnRect.Contains(currentMouseState.X, currentMouseState.Y))
                        {
                            _bindingKeyCount = k;
                            break;
                        }
                    }

                    // Selector de columna individual
                    int keyRowsY = selectorY + 50;
                    int rowH = 28;
                    if (_inputManager.Keybinds.TryGetValue(_bindingKeyCount, out Keys[]? keys))
                    {
                        for (int c = 0; c < _bindingKeyCount; c++)
                        {
                            int rowY = keyRowsY + c * (rowH + 6);
                            var bindBtnRect = new Rectangle(kX + 130, rowY, lw - 140, rowH);
                            if (bindBtnRect.Contains(currentMouseState.X, currentMouseState.Y))
                            {
                                _isBindingKey = true;
                                _bindingColumnIndex = c;
                                break;
                            }
                        }
                    }
                }
                else
                {
                    // === Clics en el Listado de Ajustes Estándar ===
                    
                    // 1. Comprobar si hay algún selector abierto
                    OverlaySettingItem? openSelector = null;
                    int openSelectorIndex = -1;
                    for (int i = 0; i < _settingItems.Count; i++)
                    {
                        if (_settingItems[i].IsSelector && _settingItems[i].IsSelectorOpen)
                        {
                            openSelector = _settingItems[i];
                            openSelectorIndex = i;
                            break;
                        }
                    }

                    if (openSelector != null)
                    {
                        int yPos = py + 30 + (openSelectorIndex - _scrollOffset) * ih;
                        var selectorRect = new Rectangle(lx + lw - 180, yPos + 1, 165, 22);
                        int optionH = 26;
                        int dropdownH = openSelector.SelectorOptions.Length * optionH;
                        var dropdownRect = openSelector == _skinSelector
                            ? new Rectangle(lx, selectorRect.Y + selectorRect.Height, lw, dropdownH)
                            : new Rectangle(selectorRect.X, selectorRect.Y + selectorRect.Height, selectorRect.Width, dropdownH);

                        if (dropdownRect.Contains(currentMouseState.X, currentMouseState.Y))
                        {
                            int clickedOptionIdx = (currentMouseState.Y - dropdownRect.Y) / optionH;
                            clickedOptionIdx = Math.Clamp(clickedOptionIdx, 0, openSelector.SelectorOptions.Length - 1);
                            
                            openSelector.OnSelectorChanged?.Invoke(clickedOptionIdx);
                            openSelector.IsSelectorOpen = false;
                            
                            _previousKeyState = currentKeyState;
                            _prevMouseState = currentMouseState;
                            return; // Consumir clic
                        }
                        else
                        {
                            openSelector.IsSelectorOpen = false;
                        }
                    }

                    // 2. Clics normales en items
                    int startIndex = _scrollOffset;
                    int endIndex = Math.Min(_scrollOffset + maxVisibleItems, _settingItems.Count);

                    for (int i = startIndex; i < endIndex; i++)
                    {
                        var item = _settingItems[i];
                        if (item.IsHeader) continue;

                        int yPos = py + 30 + (i - _scrollOffset) * ih;
                        var itemRect = new Rectangle(lx, yPos - 5, lw, ih - 6);

                        if (itemRect.Contains(currentMouseState.X, currentMouseState.Y))
                        {
                            _selectedIndex = i;

                            if (item.IsSlider)
                            {
                                var sliderRect = new Rectangle(lx + lw - 170, yPos + 6, 110, 16);
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
                            else if (item.IsSelector)
                            {
                                for (int s = 0; s < _settingItems.Count; s++)
                                {
                                    if (s != i && _settingItems[s].IsSelector)
                                    {
                                        _settingItems[s].IsSelectorOpen = false;
                                    }
                                }
                                item.IsSelectorOpen = !item.IsSelectorOpen;
                            }
                        }
                    }
                }
            }

            _previousKeyState = currentKeyState;
            _prevMouseState = currentMouseState;
        }

        public void Draw(GameTime gameTime)
        {
            if (!IsVisible) return;

            var spriteBatch = _game.SpriteBatch;
            var mouseState = Mouse.GetState();

            // Dibujar fondo oscurecido para la pantalla inferior
            float dimAlpha = IsOpen ? Math.Clamp((_currentX + PANEL_WIDTH) / PANEL_WIDTH * 0.4f, 0.0f, 0.4f) : 0.0f;
            if (dimAlpha > 0.01f)
            {
                spriteBatch.Begin();
                _fontRenderer.DrawBox(new Rectangle(0, 0, _game.WindowWidth, _game.WindowHeight), Color.Black * dimAlpha);
                spriteBatch.End();
            }

            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied);

            // 1. Dibujar Contenedor del Panel
            var panelRect = new Rectangle((int)_currentX, 0, (int)PANEL_WIDTH, _game.WindowHeight);
            _fontRenderer.DrawBox(panelRect, new Color(18, 18, 24, 250)); // Panel oscuro con alta opacidad

            // Línea de borde derecho neón rosa
            var borderRect = new Rectangle((int)(_currentX + PANEL_WIDTH - 2), 0, 2, _game.WindowHeight);
            _fontRenderer.DrawBox(borderRect, Color.HotPink);

            // Sombra a la derecha
            for (int i = 0; i < 15; i++)
            {
                var shadowLine = new Rectangle((int)(_currentX + PANEL_WIDTH + i), 0, 1, _game.WindowHeight);
                _fontRenderer.DrawBox(shadowLine, Color.Black * (0.18f * (1.0f - (i / 15f))));
            }

            int py = 80;
            int ph = _game.WindowHeight - py - 100;
            int ih = 44;
            int maxVisibleItems = ph / ih;

            int lx = (int)_currentX + 20;
            int lw = (int)PANEL_WIDTH - 40;

            // Offsets de deslizamiento lateral entre los dos sub-paneles
            int standardOffset = (int)(-_keybindPanelProgress * PANEL_WIDTH);
            int keybindsOffset = (int)((1.0f - _keybindPanelProgress) * PANEL_WIDTH);

            // Título de Configuración (desliza con el panel estándar)
            _fontRenderer.SetFontSize(32);
            _fontRenderer.DrawText("SETTINGS", new Vector2(lx + 10 + standardOffset, 25), Color.HotPink, 1.0f);

            // --- 2. RENDERIZAR AJUSTES ESTÁNDAR ---
            int startIndex = _scrollOffset;
            int endIndex = Math.Min(_scrollOffset + maxVisibleItems, _settingItems.Count);

            for (int i = startIndex; i < endIndex; i++)
            {
                var item = _settingItems[i];
                int yPos = py + 30 + (i - _scrollOffset) * ih;

                if (item.IsHeader)
                {
                    _fontRenderer.SetFontSize(14);
                    _fontRenderer.DrawText(item.Name, new Vector2(lx + 10 + standardOffset, yPos + 4), Color.HotPink * 0.8f, 1.0f);
                    // Línea decorativa
                    var lineRect = new Rectangle(lx + 10 + standardOffset, yPos + 22, lw - 20, 1);
                    _fontRenderer.DrawBox(lineRect, Color.White * 0.08f);
                    continue;
                }

                float progress = i < _hoverProgressArray.Length ? _hoverProgressArray[i] : 0.0f;
                var itemRect = new Rectangle(lx + standardOffset, yPos - 5, lw, ih - 6);

                // Animación hover
                if (progress > 0.01f)
                {
                    _fontRenderer.DrawBox(itemRect, new Color(255, 102, 171, 30) * progress);
                    _fontRenderer.DrawBoxBorder(itemRect, Color.HotPink * progress, 1);
                }

                // Nombre
                _fontRenderer.SetFontSize(16);
                Color textColor = Color.Lerp(new Color(200, 200, 210), Color.White, progress);
                _fontRenderer.DrawText(item.Name, new Vector2(lx + 12 + standardOffset + progress * 6f, yPos), textColor, 1.0f);

                if (item.IsSlider)
                {
                    var sliderRect = new Rectangle(lx + standardOffset + lw - 170, yPos + 6, 110, 16);
                    item.SliderBounds = sliderRect;
                    DrawSlider(sliderRect, item.GetSliderValue(), item.MinSliderValue, item.MaxSliderValue, item.GetValue(), progress > 0.5f);
                }
                else if (item.IsToggle)
                {
                    var toggleRect = new Rectangle(lx + standardOffset + lw - 90, yPos + 1, 75, 22);
                    bool isON = item.GetValue().Contains("ON");
                    Color pillColor = isON ? Color.HotPink : new Color(50, 50, 60);
                    _fontRenderer.DrawBox(toggleRect, pillColor);
                    _fontRenderer.DrawBoxBorder(toggleRect, Color.White * 0.4f, 1);

                    _fontRenderer.SetFontSize(12);
                    _fontRenderer.DrawTextCentered(item.GetValue(), toggleRect.X + toggleRect.Width / 2, toggleRect.Y + toggleRect.Height / 2, Color.White);
                }
                else if (item.IsSelector)
                {
                    var selectorRect = new Rectangle(lx + standardOffset + lw - 180, yPos + 1, 165, 22);
                    Color selectorColor = item.IsSelectorOpen ? new Color(40, 40, 50) : new Color(30, 30, 40);
                    _fontRenderer.DrawBox(selectorRect, selectorColor);
                    _fontRenderer.DrawBoxBorder(selectorRect, progress > 0.5f || item.IsSelectorOpen ? Color.HotPink : Color.White * 0.3f, 1);

                    _fontRenderer.SetFontSize(12);
                    _fontRenderer.DrawTextCentered(FitSelectorText(item.GetValue(), selectorRect.Width - 24), selectorRect.X + (selectorRect.Width - 16) / 2, selectorRect.Y + selectorRect.Height / 2, Color.White);

                    int arrowX = selectorRect.X + selectorRect.Width - 14;
                    int arrowY = selectorRect.Y + (selectorRect.Height - 10) / 2;
                    _fontRenderer.SetFontSize(10);
                    _fontRenderer.DrawText(item.IsSelectorOpen ? "^" : "v", new Vector2(arrowX, arrowY - 2), Color.HotPink);
                }
            }

            // Scrollbar indicador
            if (_settingItems.Count > maxVisibleItems && _keybindPanelProgress <= 0.5f)
            {
                int scrollbarHeight = Math.Max(20, (ph * maxVisibleItems) / _settingItems.Count);
                float scrollPercent = (float)_scrollOffset / (_settingItems.Count - maxVisibleItems + 1);
                int scrollbarY = py + 20 + (int)((ph - scrollbarHeight - 30) * scrollPercent);

                var scrollbarRect = new Rectangle(lx + standardOffset + lw - 4, scrollbarY, 3, scrollbarHeight);
                _fontRenderer.DrawBox(scrollbarRect, Color.HotPink);
            }

            // --- 3. RENDERIZAR SUB-PANEL KEYBINDS MAPPER (SLIDING FROM RIGHT) ---
            if (_keybindPanelProgress > 0.01f)
            {
                int kX = lx + keybindsOffset;
                int kY = py + 30;

                // Título de la sección
                _fontRenderer.SetFontSize(24);
                _fontRenderer.DrawText("KEY BINDINGS", new Vector2(kX + 10, 20), Color.HotPink, 1.0f);

                // Botón "Back"
                var backRect = new Rectangle(kX + 10, kY, 80, 26);
                bool backHovered = backRect.Contains(mouseState.X, mouseState.Y);
                _fontRenderer.DrawBox(backRect, backHovered ? new Color(50, 50, 60) : new Color(30, 30, 40));
                _fontRenderer.DrawBoxBorder(backRect, backHovered ? Color.HotPink : Color.Gray, 1);
                _fontRenderer.SetFontSize(12);
                _fontRenderer.DrawTextCentered("<- Back", backRect.X + backRect.Width / 2, backRect.Y + backRect.Height / 2, Color.White);

                // Selector de K (4K - 7K)
                int selectorY = kY + 50;
                int btnW = 55;
                int btnH = 28;
                int spacing = 8;
                int startBtnX = kX + (lw - (4 * btnW + 3 * spacing)) / 2;

                for (int k = 4; k <= 7; k++)
                {
                    int btnX = startBtnX + (k - 4) * (btnW + spacing);
                    var btnRect = new Rectangle(btnX, selectorY, btnW, btnH);
                    bool isBtnHovered = btnRect.Contains(mouseState.X, mouseState.Y);
                    bool isBtnActive = _bindingKeyCount == k;

                    Color btnBg = isBtnActive ? Color.HotPink : (isBtnHovered ? new Color(50, 50, 65) : new Color(30, 30, 40));
                    _fontRenderer.DrawBox(btnRect, btnBg);
                    _fontRenderer.DrawBoxBorder(btnRect, isBtnActive ? Color.White : Color.Gray, 1);

                    _fontRenderer.SetFontSize(14);
                    _fontRenderer.DrawTextCentered($"{k}K", btnX + btnW / 2, selectorY + btnH / 2, Color.White);
                }

                // Filas de columnas para keybinds
                int keyRowsY = selectorY + 50;
                int rowH = 28;
                if (_inputManager.Keybinds.TryGetValue(_bindingKeyCount, out Keys[]? keys))
                {
                    for (int c = 0; c < _bindingKeyCount; c++)
                    {
                        int rowY = keyRowsY + c * (rowH + 6);
                        if (rowY > _game.WindowHeight - 20) break; // Culling por si sale de pantalla

                        _fontRenderer.SetFontSize(14);
                        _fontRenderer.DrawText($"Col {c + 1}:", new Vector2(kX + 20, rowY + 5), Color.White, 1.0f);

                        var bindBtnRect = new Rectangle(kX + 130, rowY, lw - 140, rowH);
                        bool isBindHovered = bindBtnRect.Contains(mouseState.X, mouseState.Y);
                        bool isBindingThis = _isBindingKey && _bindingColumnIndex == c;

                        Color bindBtnColor = isBindingThis ? Color.Yellow * 0.25f : (isBindHovered ? new Color(50, 50, 60) : new Color(30, 30, 40));
                        Color borderCol = isBindingThis ? Color.Yellow : (isBindHovered ? Color.HotPink : Color.Gray);
                        _fontRenderer.DrawBox(bindBtnRect, bindBtnColor);
                        _fontRenderer.DrawBoxBorder(bindBtnRect, borderCol, 1);

                        _fontRenderer.SetFontSize(14);
                        string keyText = isBindingThis ? "[ Press... ]" : keys[c].ToString();
                        _fontRenderer.DrawTextCentered(keyText, bindBtnRect.X + bindBtnRect.Width / 2, bindBtnRect.Y + bindBtnRect.Height / 2, isBindingThis ? Color.Yellow : Color.White);
                    }
                }
            }

            // --- 4. DIBUJAR MENÚ FLOTANTE DE SELECTOR DE FORMA SOBREPUESTA (OVERLAY) ---
            OverlaySettingItem? activeOpenSelector = null;
            int activeOpenIndex = -1;
            for (int i = startIndex; i < endIndex; i++)
            {
                if (_settingItems[i].IsSelector && _settingItems[i].IsSelectorOpen)
                {
                    activeOpenSelector = _settingItems[i];
                    activeOpenIndex = i;
                    break;
                }
            }

            if (activeOpenSelector != null)
            {
                int yPos = py + 30 + (activeOpenIndex - _scrollOffset) * ih;
                var selectorRect = new Rectangle(lx + standardOffset + lw - 180, yPos + 1, 165, 22);
                int optionH = 26;
                int dropdownH = activeOpenSelector.SelectorOptions.Length * optionH;
                var dropdownRect = activeOpenSelector == _skinSelector
                    ? new Rectangle(lx + standardOffset, selectorRect.Y + selectorRect.Height, lw, dropdownH)
                    : new Rectangle(selectorRect.X, selectorRect.Y + selectorRect.Height, selectorRect.Width, dropdownH);

                // Dibujar caja de fondo del menú flotante con sombra y bordes neón
                _fontRenderer.DrawBox(dropdownRect, new Color(22, 22, 28, 255));
                _fontRenderer.DrawBoxBorder(dropdownRect, Color.HotPink, 2);

                for (int o = 0; o < activeOpenSelector.SelectorOptions.Length; o++)
                {
                    var optRect = new Rectangle(dropdownRect.X, dropdownRect.Y + o * optionH, dropdownRect.Width, optionH);
                    bool optHovered = optRect.Contains(mouseState.X, mouseState.Y);
                    bool optSelected = o == activeOpenSelector.GetSelectorIndex();

                    if (optHovered)
                    {
                        _fontRenderer.DrawBox(optRect, new Color(255, 102, 171, 50));
                    }
                    else if (optSelected)
                    {
                        _fontRenderer.DrawBox(optRect, new Color(255, 102, 171, 20));
                    }

                    _fontRenderer.SetFontSize(12);
                    Color optColor = optHovered || optSelected ? Color.White : new Color(200, 200, 200);
                    _fontRenderer.DrawTextCentered(FitSelectorText(activeOpenSelector.SelectorOptions[o], optRect.Width - 14), optRect.X + optRect.Width / 2, optRect.Y + optRect.Height / 2, optColor);
                }
            }

            spriteBatch.End();
        }

        private string FitSelectorText(string value, int maxWidth)
        {
            if (_fontRenderer.MeasureText(value).X <= maxWidth) return value;
            const string ellipsis = "...";
            while (value.Length > 0 && _fontRenderer.MeasureText(value + ellipsis).X > maxWidth)
                value = value[..^1];
            return value + ellipsis;
        }

        private void DrawSlider(Rectangle bounds, float value, float min, float max, string displayValue, bool highlighted)
        {
            var trackRect = new Rectangle(bounds.X, bounds.Y + bounds.Height / 2 - 2, bounds.Width, 4);
            _fontRenderer.DrawBox(trackRect, new Color(50, 50, 60));

            float percent = Math.Clamp((value - min) / (max - min), 0f, 1f);
            int fillW = (int)(bounds.Width * percent);
            var fillRect = new Rectangle(bounds.X, bounds.Y + bounds.Height / 2 - 2, fillW, 4);
            _fontRenderer.DrawBox(fillRect, Color.HotPink);

            int knobX = bounds.X + fillW;
            var knobRect = new Rectangle(knobX - 4, bounds.Y + bounds.Height / 2 - 6, 8, 12);
            _fontRenderer.DrawBox(knobRect, Color.White);
            _fontRenderer.DrawBoxBorder(knobRect, highlighted ? Color.HotPink : Color.Gray, 1);

            _fontRenderer.SetFontSize(12);
            _fontRenderer.DrawText(displayValue, new Vector2(bounds.X + bounds.Width + 8, bounds.Y + bounds.Height / 2 - 6), Color.White, 1.0f);
        }

        private bool IsKeyPressed(KeyboardState currentState, Keys key)
        {
            return currentState.IsKeyDown(key) && _previousKeyState.IsKeyUp(key);
        }
    }

    /// <summary>
    /// Item de ajuste para el SettingsOverlay.
    /// </summary>
    internal class OverlaySettingItem
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
        public bool IsHeader { get; }
        public Rectangle? SliderBounds { get; set; }

        // Propiedades de selector dropdown
        public bool IsSelector { get; set; }
        public string[] SelectorOptions { get; set; } = Array.Empty<string>();
        public Func<int> GetSelectorIndex { get; set; } = () => 0;
        public Action<int> OnSelectorChanged { get; set; } = _ => {};
        public bool IsSelectorOpen { get; set; }

        public OverlaySettingItem(
            string name, 
            Func<string> getValue, 
            bool isSlider, 
            float min, 
            float max, 
            Func<float> getSlider, 
            Action<float> onSliderChanged, 
            bool isToggle, 
            Action onToggle,
            bool isHeader)
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
            IsHeader = isHeader;
        }

        public static OverlaySettingItem CreateSlider(string name, float min, float max, Func<float> getSlider, Action<float> onSliderChanged, Func<string> getValue)
        {
            return new OverlaySettingItem(name, getValue, true, min, max, getSlider, onSliderChanged, false, () => {}, false);
        }

        public static OverlaySettingItem CreateToggle(string name, Func<string> getValue, Action onToggle)
        {
            return new OverlaySettingItem(name, getValue, false, 0, 0, () => 0, _ => {}, true, onToggle, false);
        }

        public static OverlaySettingItem CreateHeader(string title)
        {
            return new OverlaySettingItem(title, () => "", false, 0, 0, () => 0, _ => {}, false, () => {}, true);
        }

        public static OverlaySettingItem CreateSelector(string name, string[] options, Func<int> getIndex, Action<int> onChanged)
        {
            OverlaySettingItem? item = null;
            item = new OverlaySettingItem(name, () => {
                int idx = getIndex();
                return idx >= 0 && idx < item!.SelectorOptions.Length ? item.SelectorOptions[idx] : "";
            }, false, 0, 0, () => 0, _ => {}, false, () => {}, false);
            item.IsSelector = true;
            item.SelectorOptions = options;
            item.GetSelectorIndex = getIndex;
            item.OnSelectorChanged = onChanged;
            return item;
        }
    }
}
