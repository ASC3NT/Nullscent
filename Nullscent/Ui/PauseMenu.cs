#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Nullscent.Audio;
using System;

namespace Nullscent.UI
{
    /// <summary>
    /// Menú de pausa que se muestra como overlay durante el gameplay.
    /// Opciones: Resume, Retry, Change Rate, Quit to Song Select.
    /// Estilo osu!lazer con fondo oscurecido.
    /// </summary>
    public class PauseMenu
    {
        private readonly GraphicsDevice _graphicsDevice;
        private readonly SpriteBatch _spriteBatch;
        private readonly AudioEngine _audioEngine;

        private TrueTypeFontRenderer? _fontRenderer;
        private int _selectedIndex;
        private KeyboardState _previousKeyboardState;
        private bool _isVisible;

        // Opciones del menú
        private readonly string[] _menuOptions = new[]
        {
            "Resume",
            "Retry",
            "Change Rate",
            "Quit to Song Select"
        };

        // Rate options
        private readonly double[] _rateOptions = new[] { 0.5, 0.75, 1.0, 1.25, 1.5 };
        private int _currentRateIndex = 2; // 1.0x por defecto

        // Colors
        private readonly Color _overlayColor = new Color(0, 0, 0, 200);
        private readonly Color _accentColor = new Color(255, 102, 171);
        private readonly Color _textColor = Color.White;
        private readonly Color _dimTextColor = new(180, 180, 200);

        /// <summary>
        /// Indica si el menú está visible.
        /// </summary>
        public bool IsVisible
        {
            get => _isVisible;
            set => _isVisible = value;
        }

        /// <summary>
        /// Resultado de la acción del menú.
        /// </summary>
        public enum MenuAction
        {
            None,
            Resume,
            Retry,
            Quit
        }

        /// <summary>
        /// Acción seleccionada por el usuario.
        /// </summary>
        public MenuAction SelectedAction { get; private set; } = MenuAction.None;

        public PauseMenu(GraphicsDevice graphicsDevice, SpriteBatch spriteBatch, AudioEngine audioEngine, TrueTypeFontRenderer fontRenderer)
        {
            _graphicsDevice = graphicsDevice;
            _spriteBatch = spriteBatch;
            _audioEngine = audioEngine;
            _fontRenderer = fontRenderer;

            // Inicializar rate index según rate actual del audio engine
            UpdateRateIndex();
        }

        /// <summary>
        /// Muestra el menú de pausa.
        /// </summary>
        public void Show()
        {
            _isVisible = true;
            _selectedIndex = 0;
            SelectedAction = MenuAction.None;
            _previousKeyboardState = Keyboard.GetState(); // Resetear estado de teclado
            UpdateRateIndex();
        }

        /// <summary>
        /// Oculta el menú de pausa.
        /// </summary>
        public void Hide()
        {
            _isVisible = false;
            SelectedAction = MenuAction.None;
        }

        /// <summary>
        /// Actualiza el índice de rate según el rate actual del audio engine.
        /// </summary>
        private void UpdateRateIndex()
        {
            double currentRate = _audioEngine.RateMultiplier;
            for (int i = 0; i < _rateOptions.Length; i++)
            {
                if (Math.Abs(_rateOptions[i] - currentRate) < 0.01)
                {
                    _currentRateIndex = i;
                    break;
                }
            }
        }

        /// <summary>
        /// Actualiza la lógica del menú (input).
        /// </summary>
        public void Update(GameTime gameTime)
        {
            if (!_isVisible)
                return;

            var currentKeyboardState = Keyboard.GetState();

            // Navegación
            if (IsKeyPressed(currentKeyboardState, Keys.Down))
            {
                _selectedIndex = (_selectedIndex + 1) % _menuOptions.Length;
            }

            if (IsKeyPressed(currentKeyboardState, Keys.Up))
            {
                _selectedIndex = (_selectedIndex - 1 + _menuOptions.Length) % _menuOptions.Length;
            }

            // Cambiar rate (si la opción "Change Rate" está seleccionada)
            if (_selectedIndex == 2)
            {
                if (IsKeyPressed(currentKeyboardState, Keys.Right))
                {
                    _currentRateIndex = (_currentRateIndex + 1) % _rateOptions.Length;
                    _audioEngine.RateMultiplier = _rateOptions[_currentRateIndex];
                }

                if (IsKeyPressed(currentKeyboardState, Keys.Left))
                {
                    _currentRateIndex = (_currentRateIndex - 1 + _rateOptions.Length) % _rateOptions.Length;
                    _audioEngine.RateMultiplier = _rateOptions[_currentRateIndex];
                }
            }

            // Confirmar selección
            if (IsKeyPressed(currentKeyboardState, Keys.Enter))
            {
                ExecuteSelectedOption();
            }

            // Resume con Escape
            if (IsKeyPressed(currentKeyboardState, Keys.Escape))
            {
                SelectedAction = MenuAction.Resume;
                Hide();
            }

            _previousKeyboardState = currentKeyboardState;
        }

        private void ExecuteSelectedOption()
        {
            switch (_selectedIndex)
            {
                case 0: // Resume
                    SelectedAction = MenuAction.Resume;
                    Hide();
                    break;

                case 1: // Retry
                    SelectedAction = MenuAction.Retry;
                    Hide();
                    break;

                case 2: // Change Rate
                    // Ya se maneja con Left/Right
                    break;

                case 3: // Quit
                    SelectedAction = MenuAction.Quit;
                    Hide();
                    break;
            }
        }

        /// <summary>
        /// Dibuja el menú de pausa.
        /// </summary>
        public void Draw(GameTime gameTime)
        {
            if (!_isVisible || _fontRenderer == null)
                return;

            int screenWidth = _graphicsDevice.Viewport.Width;
            int screenHeight = _graphicsDevice.Viewport.Height;

            // Overlay oscuro
            var overlayRect = new Rectangle(0, 0, screenWidth, screenHeight);
            _fontRenderer.DrawBox(overlayRect, _overlayColor);

            // Panel central
            int panelWidth = 500;
            int panelHeight = 400;
            int panelX = (screenWidth - panelWidth) / 2;
            int panelY = (screenHeight - panelHeight) / 2;

            var panelRect = new Rectangle(panelX, panelY, panelWidth, panelHeight);
            _fontRenderer.DrawBox(panelRect, new Color(30, 30, 40));
            _fontRenderer.DrawBoxBorder(panelRect, _accentColor, 3);

            // Título
            _fontRenderer.DrawTextCentered("PAUSED", screenWidth / 2, panelY + 40, _accentColor);

            // Opciones del menú
            int optionY = panelY + 120;
            int optionSpacing = 60;

            for (int i = 0; i < _menuOptions.Length; i++)
            {
                bool isSelected = i == _selectedIndex;
                Color color = isSelected ? _accentColor : _dimTextColor;

                string optionText = _menuOptions[i];

                // Para "Change Rate", mostrar el rate actual
                if (i == 2)
                {
                    double currentRate = _rateOptions[_currentRateIndex];
                    optionText = $"Rate: < {currentRate:F2}x >";
                }

                _fontRenderer.DrawTextCentered(optionText, screenWidth / 2, optionY + i * optionSpacing, color);

                // Indicador de selección
                if (isSelected)
                {
                    int arrowX = (int)(screenWidth / 2 - _fontRenderer.MeasureText(optionText).X / 2 - 30);
                    _fontRenderer.DrawText(">", arrowX, optionY + i * optionSpacing, _accentColor);
                }
            }

            // Footer hint
            string hint = _selectedIndex == 2 ? "Arrow keys to change rate  |  Enter to select  |  Esc to resume" : "Enter to select  |  Esc to resume";
            _fontRenderer.DrawTextCentered(hint, screenWidth / 2, panelY + panelHeight - 40, _dimTextColor * 0.7f);
        }

        private bool IsKeyPressed(KeyboardState current, Keys key)
        {
            return current.IsKeyDown(key) && _previousKeyboardState.IsKeyUp(key);
        }

        public void Dispose()
        {
        }
    }
}
