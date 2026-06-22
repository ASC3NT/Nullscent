#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Nullscent.Config;
using Nullscent.Core;
using Nullscent.UI;
using System;
using System.Collections.Generic;

namespace Nullscent.Screens
{
    /// <summary>
    /// Menú principal del juego con navegación por botones.
    /// Opciones: Play, Settings, Exit
    /// </summary>
    public class MainMenuScreen : IGameScreen
    {
        private readonly Game1 _game;
        private readonly InputManager _inputManager;
        private readonly TrueTypeFontRenderer _fontRenderer;

        private readonly List<MenuButton> _buttons = new();
        private int _selectedButtonIndex = 0;

        private KeyboardState _previousKeyState;

        public MainMenuScreen(Game1 game, InputManager inputManager, TrueTypeFontRenderer fontRenderer)
        {
            _game = game;
            _inputManager = inputManager;
            _fontRenderer = fontRenderer;
        }

        public void Initialize()
        {
            Console.WriteLine("[MainMenuScreen] Initializing main menu...");

            // Configurar botones del menú
            int buttonWidth = 400;
            int buttonHeight = 80;
            int buttonSpacing = 20;
            int startY = _game.WindowHeight / 2 - 100;
            int centerX = _game.WindowWidth / 2;

            _buttons.Clear();

            _buttons.Add(new MenuButton(
                "PLAY",
                new Rectangle(centerX - buttonWidth / 2, startY, buttonWidth, buttonHeight),
                OnPlayClicked
            ));

            _buttons.Add(new MenuButton(
                "SETTINGS",
                new Rectangle(centerX - buttonWidth / 2, startY + buttonHeight + buttonSpacing, buttonWidth, buttonHeight),
                OnSettingsClicked
            ));

            _buttons.Add(new MenuButton(
                "EXIT",
                new Rectangle(centerX - buttonWidth / 2, startY + (buttonHeight + buttonSpacing) * 2, buttonWidth, buttonHeight),
                OnExitClicked
            ));

            _selectedButtonIndex = 0;
            _previousKeyState = Keyboard.GetState();

            Console.WriteLine("[MainMenuScreen] Main menu initialized");
        }

        public void Update(GameTime gameTime)
        {
            var currentKeyState = Keyboard.GetState();

            // Navegación con teclado
            if (IsKeyPressed(currentKeyState, Keys.Down) || IsKeyPressed(currentKeyState, Keys.S))
            {
                _selectedButtonIndex = (_selectedButtonIndex + 1) % _buttons.Count;
            }

            if (IsKeyPressed(currentKeyState, Keys.Up) || IsKeyPressed(currentKeyState, Keys.W))
            {
                _selectedButtonIndex--;
                if (_selectedButtonIndex < 0)
                    _selectedButtonIndex = _buttons.Count - 1;
            }

            if (IsKeyPressed(currentKeyState, Keys.Enter) || IsKeyPressed(currentKeyState, Keys.Space))
            {
                _buttons[_selectedButtonIndex].OnClick?.Invoke();
            }

            if (IsKeyPressed(currentKeyState, Keys.Escape))
            {
                OnExitClicked();
            }

            _previousKeyState = currentKeyState;

            // Actualizar InputManager
            _inputManager.Update(gameTime.TotalGameTime.TotalMilliseconds);
        }

        public void Draw(GameTime gameTime)
        {
            var spriteBatch = _game.SpriteBatch;

            spriteBatch.Begin();

            // Fondo oscuro
            _fontRenderer.DrawBox(new Rectangle(0, 0, _game.WindowWidth, _game.WindowHeight), new Color(15, 15, 20));

            // Título del juego
            string title = "NULLSCENT";
            _fontRenderer.SetFontSize(72);
            _fontRenderer.DrawTextCentered(title, new Vector2(_game.WindowWidth / 2, 120), Color.HotPink, 1.0f);

            // Subtítulo
            _fontRenderer.SetFontSize(24);
            _fontRenderer.DrawTextCentered("osu!mania Practice Client", new Vector2(_game.WindowWidth / 2, 200), Color.Gray, 1.0f);

            // Botones
            _fontRenderer.SetFontSize(36);
            for (int i = 0; i < _buttons.Count; i++)
            {
                var button = _buttons[i];
                bool isSelected = (i == _selectedButtonIndex);

                // Color del botón
                Color bgColor = isSelected ? new Color(80, 20, 60, 220) : new Color(30, 30, 40, 200);
                Color borderColor = isSelected ? Color.HotPink : new Color(60, 60, 70);
                Color textColor = isSelected ? Color.White : new Color(180, 180, 190);

                // Dibujar fondo del botón
                _fontRenderer.DrawBox(button.Bounds, bgColor);

                // Dibujar borde del botón
                _fontRenderer.DrawBoxBorder(button.Bounds, borderColor, isSelected ? 4 : 2);

                // Dibujar texto del botón
                var textSize = _fontRenderer.MeasureText(button.Text, 1.0f);
                var textPos = new Vector2(
                    button.Bounds.X + (button.Bounds.Width - textSize.X) / 2,
                    button.Bounds.Y + (button.Bounds.Height - textSize.Y) / 2
                );
                _fontRenderer.DrawText(button.Text, textPos, textColor, 1.0f);
            }

            // Indicador de selección
            if (_selectedButtonIndex >= 0 && _selectedButtonIndex < _buttons.Count)
            {
                var selectedButton = _buttons[_selectedButtonIndex];
                string arrow = ">";
                _fontRenderer.SetFontSize(48);
                var arrowSize = _fontRenderer.MeasureText(arrow, 1.0f);
                var arrowPos = new Vector2(
                    selectedButton.Bounds.X - arrowSize.X - 20,
                    selectedButton.Bounds.Y + (selectedButton.Bounds.Height - arrowSize.Y) / 2
                );
                _fontRenderer.DrawText(arrow, arrowPos, Color.HotPink, 1.0f);
            }

            // Instrucciones en la parte inferior
            _fontRenderer.SetFontSize(18);
            string instructions = "UP/DOWN: Navigate  |  ENTER: Select  |  ESC: Exit";
            _fontRenderer.DrawTextCentered(instructions, new Vector2(_game.WindowWidth / 2, _game.WindowHeight - 40), new Color(120, 120, 130), 1.0f);

            spriteBatch.End();
        }

        public void Cleanup()
        {
            Console.WriteLine("[MainMenuScreen] Cleaning up main menu");
            _buttons.Clear();
        }

        private bool IsKeyPressed(KeyboardState currentState, Keys key)
        {
            return currentState.IsKeyDown(key) && _previousKeyState.IsKeyUp(key);
        }

        private void OnPlayClicked()
        {
            Console.WriteLine("[MainMenuScreen] Play button clicked");
            // Navegar a Song Select
            var songSelect = new Nullscent.UI.SongSelectScreen(
                _game,
                _game.GraphicsDevice,
                _game.SpriteBatch,
                _game.StateManager,
                _game.AudioEngine,
                _inputManager,
                _game.HitSoundPlayer,
                _game.Settings
            );
            _game.StateManager.ChangeState(GameState.SongSelect, songSelect);
        }

        private void OnSettingsClicked()
        {
            Console.WriteLine("[MainMenuScreen] Settings button clicked");
            // Navegar a Settings (por ahora mostrar mensaje)
            // TODO: Implementar SettingsScreen
            var settings = new SettingsScreen(_game, _inputManager, _fontRenderer);
            _game.StateManager.ChangeState(GameState.Settings, settings);
        }

        private void OnExitClicked()
        {
            Console.WriteLine("[MainMenuScreen] Exit button clicked");
            _game.Exit();
        }
    }

    /// <summary>
    /// Clase auxiliar para representar un botón del menú.
    /// </summary>
    internal class MenuButton
    {
        public string Text { get; }
        public Rectangle Bounds { get; }
        public Action? OnClick { get; }

        public MenuButton(string text, Rectangle bounds, Action? onClick = null)
        {
            Text = text;
            Bounds = bounds;
            OnClick = onClick;
        }
    }
}
