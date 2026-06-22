#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Nullscent.Config;
using Nullscent.Core;
using System;

namespace Nullscent.UI
{
    /// <summary>
    /// Pantalla de settings (configuración del juego).
    /// Permite ajustar: scroll speed, offset, volumen, keybinds, etc.
    /// Accesible desde song select con F1 o desde pause menu.
    /// </summary>
    public class SettingsScreen : IGameScreen
    {
        private readonly Game _game;
        private readonly GraphicsDevice _graphicsDevice;
        private readonly SpriteBatch _spriteBatch;
        private readonly GameStateManager _stateManager;
        private readonly GameSettings _settings;
        private readonly InputManager _inputManager;

        private FontRenderer? _fontRenderer;
        private int _selectedIndex;
        private KeyboardState _previousKeyboardState;
        private bool _isEditingKeybind;
        private int _editingKeyCount = 4;
        private int _editingColumn;

        // Opciones de settings
        private readonly string[] _settingOptions = new[]
        {
            "Scroll Speed",
            "Global Audio Offset",
            "Master Volume",
            "Music Volume",
            "Hitsound Volume",
            "Health Drain",
            "Down Scroll",
            "Edit Keybinds",
            "Back"
        };

        // Colors
        private readonly Color _backgroundColor = new(20, 20, 30);
        private readonly Color _accentColor = new(255, 102, 171);
        private readonly Color _textColor = Color.White;
        private readonly Color _dimTextColor = new(180, 180, 200);

        public SettingsScreen(
            Game game,
            GraphicsDevice graphicsDevice,
            SpriteBatch spriteBatch,
            GameStateManager stateManager,
            GameSettings settings,
            InputManager inputManager)
        {
            _game = game;
            _graphicsDevice = graphicsDevice;
            _spriteBatch = spriteBatch;
            _stateManager = stateManager;
            _settings = settings;
            _inputManager = inputManager;
        }

        public void Initialize()
        {
            Console.WriteLine("[SettingsScreen] Initialized");
            _fontRenderer = new FontRenderer(_graphicsDevice, _spriteBatch);
            _selectedIndex = 0;
            _isEditingKeybind = false;
        }

        public void Update(GameTime gameTime)
        {
            var currentKeyboardState = Keyboard.GetState();

            if (_isEditingKeybind)
            {
                HandleKeybindEdit(currentKeyboardState);
            }
            else
            {
                HandleMainMenu(currentKeyboardState);
            }

            _previousKeyboardState = currentKeyboardState;
        }

        private void HandleMainMenu(KeyboardState keyboardState)
        {
            // Navegación
            if (IsKeyPressed(keyboardState, Keys.Down))
                _selectedIndex = (_selectedIndex + 1) % _settingOptions.Length;

            if (IsKeyPressed(keyboardState, Keys.Up))
                _selectedIndex = (_selectedIndex - 1 + _settingOptions.Length) % _settingOptions.Length;

            // Ajustar valores con Left/Right
            if (IsKeyPressed(keyboardState, Keys.Left))
                AdjustSetting(-1);

            if (IsKeyPressed(keyboardState, Keys.Right))
                AdjustSetting(1);

            // Enter para confirmar/entrar en submenu
            if (IsKeyPressed(keyboardState, Keys.Enter))
            {
                if (_selectedIndex == 7) // Edit Keybinds
                {
                    _isEditingKeybind = true;
                    _editingColumn = 0;
                }
                else if (_selectedIndex == 8) // Back
                {
                    SaveAndExit();
                }
            }

            // Escape para salir
            if (IsKeyPressed(keyboardState, Keys.Escape))
                SaveAndExit();
        }

        private void HandleKeybindEdit(KeyboardState keyboardState)
        {
            // Escape para salir del modo edición
            if (IsKeyPressed(keyboardState, Keys.Escape))
            {
                _isEditingKeybind = false;
                return;
            }

            // Cambiar key count
            if (IsKeyPressed(keyboardState, Keys.Tab))
            {
                _editingKeyCount = _editingKeyCount switch
                {
                    4 => 7,
                    7 => 6,
                    6 => 5,
                    5 => 4,
                    _ => 4
                };
                _editingColumn = 0;
            }

            // Navegar columnas
            if (IsKeyPressed(keyboardState, Keys.Right))
            {
                _editingColumn = (_editingColumn + 1) % _editingKeyCount;
            }

            if (IsKeyPressed(keyboardState, Keys.Left))
            {
                _editingColumn = (_editingColumn - 1 + _editingKeyCount) % _editingKeyCount;
            }

            // Asignar tecla (cualquier tecla presionada)
            var pressedKeys = keyboardState.GetPressedKeys();
            foreach (var key in pressedKeys)
            {
                if (_previousKeyboardState.IsKeyUp(key) && key != Keys.Escape && key != Keys.Tab && key != Keys.Left && key != Keys.Right)
                {
                    _inputManager.SetKeybind(_editingKeyCount, _editingColumn, key);
                    _editingColumn = (_editingColumn + 1) % _editingKeyCount;
                    break;
                }
            }
        }

        private void AdjustSetting(int direction)
        {
            switch (_selectedIndex)
            {
                case 0: // Scroll Speed
                    _settings.ScrollSpeed = Math.Clamp(_settings.ScrollSpeed + direction, 1, 40);
                    break;

                case 1: // Global Audio Offset
                    _settings.GlobalAudioOffset += direction * 5; // 5ms increments
                    break;

                case 2: // Master Volume
                    _settings.MasterVolume = Math.Clamp(_settings.MasterVolume + direction * 0.05f, 0f, 1f);
                    break;

                case 3: // Music Volume
                    _settings.MusicVolume = Math.Clamp(_settings.MusicVolume + direction * 0.05f, 0f, 1f);
                    break;

                case 4: // Hitsound Volume
                    _settings.HitsoundVolume = Math.Clamp(_settings.HitsoundVolume + direction * 0.05f, 0f, 1f);
                    break;

                case 5: // Health Drain
                    _settings.HealthDrainEnabled = !_settings.HealthDrainEnabled;
                    break;

                case 6: // Down Scroll
                    _settings.DownScroll = !_settings.DownScroll;
                    break;
            }
        }

        private void SaveAndExit()
        {
            _settings.Save();
            Console.WriteLine("[SettingsScreen] Settings saved, returning to song select");

            // TODO: Return to previous screen (implement screen stack)
            // Por ahora, simplemente cerrar
            _game.Exit();
        }

        public void Draw(GameTime gameTime)
        {
            _graphicsDevice.Clear(_backgroundColor);

            if (_fontRenderer == null)
                return;

            _spriteBatch.Begin();

            int screenWidth = _graphicsDevice.Viewport.Width;
            int screenHeight = _graphicsDevice.Viewport.Height;

            if (_isEditingKeybind)
            {
                DrawKeybindEditor(screenWidth, screenHeight);
            }
            else
            {
                DrawMainSettings(screenWidth, screenHeight);
            }

            _spriteBatch.End();
        }

        private void DrawMainSettings(int screenWidth, int screenHeight)
        {
            if (_fontRenderer == null)
                return;

            int centerX = screenWidth / 2;

            // Título
            _fontRenderer.DrawTextCentered("SETTINGS", new Vector2(centerX, 40), _accentColor, 2.5f);

            // Opciones
            int startY = 150;
            int spacing = 60;

            for (int i = 0; i < _settingOptions.Length; i++)
            {
                bool isSelected = i == _selectedIndex;
                Color color = isSelected ? _accentColor : _dimTextColor;
                float scale = isSelected ? 1.3f : 1.0f;

                int y = startY + i * spacing;

                // Nombre de la opción
                string optionText = _settingOptions[i];
                _fontRenderer.DrawText(optionText, new Vector2(centerX - 200, y), color, scale);

                // Valor actual
                string valueText = GetSettingValue(i);
                if (!string.IsNullOrEmpty(valueText))
                    _fontRenderer.DrawText(valueText, new Vector2(centerX + 100, y), _textColor, scale);
            }

            // Footer
            _fontRenderer.DrawTextCentered("Arrow keys to navigate  |  Enter to select  |  Esc to save & exit",
                new Vector2(centerX, screenHeight - 40), _dimTextColor * 0.6f, 0.8f);
        }

        private void DrawKeybindEditor(int screenWidth, int screenHeight)
        {
            if (_fontRenderer == null)
                return;

            int centerX = screenWidth / 2;

            // Título
            _fontRenderer.DrawTextCentered($"KEYBIND EDITOR - {_editingKeyCount}K", new Vector2(centerX, 40), _accentColor, 2.0f);
            _fontRenderer.DrawTextCentered("Press Tab to change key count", new Vector2(centerX, 80), _dimTextColor, 0.9f);

            // Dibujar columnas
            int startY = 200;
            int columnSpacing = Math.Min(100, screenWidth / (_editingKeyCount + 2));
            int startX = centerX - (columnSpacing * _editingKeyCount) / 2;

            for (int i = 0; i < _editingKeyCount; i++)
            {
                bool isEditing = i == _editingColumn;
                Color color = isEditing ? _accentColor : _dimTextColor;
                float scale = isEditing ? 1.5f : 1.0f;

                int x = startX + i * columnSpacing;

                // Número de columna
                _fontRenderer.DrawTextCentered($"Col {i + 1}", new Vector2(x, startY), color, scale);

                // Tecla actual
                var key = _inputManager.GetKeybindForColumn(i);
                string keyText = key?.ToString() ?? "None";
                _fontRenderer.DrawTextCentered(keyText, new Vector2(x, startY + 40), _textColor, scale);

                // Indicador de edición
                if (isEditing)
                {
                    _fontRenderer.DrawTextCentered("< Press key >", new Vector2(x, startY + 80), _accentColor, 0.9f);
                }
            }

            // Footer
            _fontRenderer.DrawTextCentered("Arrow keys to select column  |  Press any key to bind  |  Esc to finish",
                new Vector2(centerX, screenHeight - 40), _dimTextColor * 0.6f, 0.8f);
        }

        private string GetSettingValue(int index)
        {
            return index switch
            {
                0 => $"{_settings.ScrollSpeed}",
                1 => $"{_settings.GlobalAudioOffset:+0;-0}ms",
                2 => $"{(_settings.MasterVolume * 100):F0}%",
                3 => $"{(_settings.MusicVolume * 100):F0}%",
                4 => $"{(_settings.HitsoundVolume * 100):F0}%",
                5 => _settings.HealthDrainEnabled ? "ON" : "OFF",
                6 => _settings.DownScroll ? "DOWN" : "UP",
                _ => string.Empty
            };
        }

        private bool IsKeyPressed(KeyboardState current, Keys key)
        {
            return current.IsKeyDown(key) && _previousKeyboardState.IsKeyUp(key);
        }

        public void Cleanup()
        {
            Console.WriteLine("[SettingsScreen] Cleanup");
            _fontRenderer?.Dispose();
        }
    }
}
