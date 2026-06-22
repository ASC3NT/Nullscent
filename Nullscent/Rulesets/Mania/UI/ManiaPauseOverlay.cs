#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Nullscent.Audio;
using Nullscent.UI;

namespace Nullscent.Rulesets.Mania.UI
{
    public class ManiaPauseOverlay
    {
        private readonly GraphicsDevice _graphicsDevice;
        private readonly SpriteBatch _spriteBatch;
        private readonly AudioEngine _audioEngine;
        private readonly TrueTypeFontRenderer _fontRenderer;

        private int _selectedIndex;
        private KeyboardState _previousKeyboardState;

        private readonly string[] _menuOptions = new[] { "Continue", "Retry", "Quit" };

        public bool ShouldResume { get; private set; }
        public bool ShouldRetry { get; private set; }
        public bool ShouldQuit { get; private set; }

        public ManiaPauseOverlay(GraphicsDevice graphicsDevice, SpriteBatch spriteBatch, AudioEngine audioEngine, TrueTypeFontRenderer fontRenderer)
        {
            _graphicsDevice = graphicsDevice;
            _spriteBatch = spriteBatch;
            _audioEngine = audioEngine;
            _fontRenderer = fontRenderer;
        }

        public void Show()
        {
            _selectedIndex = 0;
            ShouldResume = false;
            ShouldRetry = false;
            ShouldQuit = false;
            _previousKeyboardState = Keyboard.GetState();
        }

        public void Hide()
        {
            // Reset state when hiding
        }

        public void Update(GameTime gameTime)
        {
            var currentKeyboardState = Keyboard.GetState();

            if (IsKeyPressed(currentKeyboardState, Keys.Down))
            {
                _selectedIndex = (_selectedIndex + 1) % _menuOptions.Length;
            }

            if (IsKeyPressed(currentKeyboardState, Keys.Up))
            {
                _selectedIndex = (_selectedIndex - 1 + _menuOptions.Length) % _menuOptions.Length;
            }

            if (IsKeyPressed(currentKeyboardState, Keys.Enter))
            {
                ExecuteSelectedOption();
            }

            _previousKeyboardState = currentKeyboardState;
        }

        private void ExecuteSelectedOption()
        {
            switch (_selectedIndex)
            {
                case 0: ShouldResume = true; break;
                case 1: ShouldRetry = true; break;
                case 2: ShouldQuit = true; break;
            }
        }

        public void Draw(GameTime gameTime)
        {
            _spriteBatch.Begin();

            int screenWidth = _graphicsDevice.Viewport.Width;
            int screenHeight = _graphicsDevice.Viewport.Height;

            var pixel = new Texture2D(_graphicsDevice, 1, 1);
            pixel.SetData(new[] { Color.White });

            var overlayRect = new Rectangle(0, 0, screenWidth, screenHeight);
            _spriteBatch.Draw(pixel, overlayRect, new Color(0, 0, 0, 200));

            _fontRenderer.SetFontSize(48);
            _fontRenderer.DrawTextCentered("PAUSED", screenWidth / 2, 150, new Color(255, 102, 171), 1.0f);

            int menuY = screenHeight / 2 - 60;
            _fontRenderer.SetFontSize(32);
            for (int i = 0; i < _menuOptions.Length; i++)
            {
                bool isSelected = (i == _selectedIndex);
                Color textColor = isSelected ? Color.White : new Color(180, 180, 200);

                if (isSelected)
                {
                    _fontRenderer.DrawTextCentered(">", screenWidth / 2 - 150, menuY + i * 50, new Color(255, 102, 171), 1.0f);
                }

                _fontRenderer.DrawTextCentered(_menuOptions[i], screenWidth / 2, menuY + i * 50, textColor, 1.0f);
            }

            _spriteBatch.End();
        }

        private bool IsKeyPressed(KeyboardState currentState, Keys key)
        {
            return currentState.IsKeyDown(key) && _previousKeyboardState.IsKeyUp(key);
        }

        public void Dispose()
        {
            // Cleanup if needed
        }
    }
}
