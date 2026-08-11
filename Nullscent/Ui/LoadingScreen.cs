#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace Nullscent.UI
{
    /// <summary>
    /// Loading screen with spinning circle animation, osu!stable style.
    /// Displays while preparing to start gameplay.
    /// </summary>
    public class LoadingScreen : IDisposable
    {
        private readonly GraphicsDevice _graphicsDevice;
        private readonly SpriteBatch _spriteBatch;
        private readonly Texture2D _pixel;

        // Spinner properties
        private double _spinnerRotation;
        private const double SPINNER_ROTATION_SPEED = 360.0; // degrees per second
        private double _elapsedTime;
        private double _totalDuration; // milliseconds
        private bool _isFadingOut;
        private double _fadeOutTime;
        private const double FADE_OUT_DURATION = 400.0; // milliseconds

        public bool IsFadingOut => _isFadingOut;

        // Visual properties
        private int _screenWidth;
        private int _screenHeight;
        private int _spinnerX;
        private int _spinnerY;
        private const int SPINNER_RADIUS = 40;

        // Display state
        private bool _isActive;
        public bool IsActive => _isActive;
        public bool IsComplete => !_isActive;

        private readonly TrueTypeFontRenderer? _fontRenderer;

        public LoadingScreen(GraphicsDevice graphicsDevice, SpriteBatch spriteBatch, TrueTypeFontRenderer? fontRenderer, double durationMs = 3000)
        {
            _graphicsDevice = graphicsDevice;
            _spriteBatch = spriteBatch;
            _fontRenderer = fontRenderer;
            _totalDuration = durationMs;

            // Create white pixel for drawing
            _pixel = new Texture2D(graphicsDevice, 1, 1);
            _pixel.SetData(new[] { Color.White });

            _isActive = true;
            _elapsedTime = 0;
            _spinnerRotation = 0;
        }

        public void SetScreenDimensions(int width, int height)
        {
            _screenWidth = width;
            _screenHeight = height;
            _spinnerX = width / 2;
            _spinnerY = height / 2;
        }

        public void Update(GameTime gameTime)
        {
            if (!_isActive)
                return;

            double deltaSeconds = gameTime.ElapsedGameTime.TotalSeconds;

            if (_isFadingOut)
            {
                _fadeOutTime += deltaSeconds * 1000;
                if (_fadeOutTime >= FADE_OUT_DURATION)
                {
                    _isActive = false;
                }
            }
            else
            {
                _elapsedTime += deltaSeconds * 1000;
                if (_elapsedTime >= _totalDuration)
                {
                    _isFadingOut = true;
                }
            }

            // Update spinner rotation
            _spinnerRotation += SPINNER_ROTATION_SPEED * deltaSeconds;
            if (_spinnerRotation >= 360)
                _spinnerRotation -= 360;
        }

        public void Draw()
        {
            if (!_isActive)
                return;

            float fadeAlpha = 1.0f;
            if (_isFadingOut)
            {
                fadeAlpha = 1.0f - (float)(_fadeOutTime / FADE_OUT_DURATION);
                fadeAlpha = Math.Clamp(fadeAlpha, 0.0f, 1.0f);
            }

            _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied);

            // Draw background (semi-transparent dark)
            var bgRect = new Rectangle(0, 0, _screenWidth, _screenHeight);
            _spriteBatch.Draw(_pixel, bgRect, new Color(15, 15, 20, (int)(225 * fadeAlpha)));

            // Draw spinner circle (animated)
            DrawSpinner(fadeAlpha);

            // Draw loading text
            DrawLoadingText(fadeAlpha);

            _spriteBatch.End();
        }

        private void DrawSpinner(float fadeAlpha)
        {
            // Draw outer ring (static)
            DrawCircleOutline(_spinnerX, _spinnerY, SPINNER_RADIUS, Color.White * 0.3f * fadeAlpha, 3);

            // Draw rotating arc
            DrawRotatingArc(_spinnerX, _spinnerY, SPINNER_RADIUS, (float)_spinnerRotation, Color.Cyan * fadeAlpha);

            // Draw inner circle
            DrawCircleOutline(_spinnerX, _spinnerY, SPINNER_RADIUS / 2, Color.White * 0.5f * fadeAlpha, 2);
        }

        private void DrawCircleOutline(int x, int y, int radius, Color color, int thickness)
        {
            // Draw circle using points around circumference
            int segments = 32;
            for (int i = 0; i < segments; i++)
            {
                float angle1 = (float)(i * Math.PI * 2 / segments);
                float angle2 = (float)((i + 1) * Math.PI * 2 / segments);

                int x1 = x + (int)(Math.Cos(angle1) * radius);
                int y1 = y + (int)(Math.Sin(angle1) * radius);
                int x2 = x + (int)(Math.Cos(angle2) * radius);
                int y2 = y + (int)(Math.Sin(angle2) * radius);

                DrawLine(x1, y1, x2, y2, color, thickness);
            }
        }

        private void DrawRotatingArc(int x, int y, int radius, float rotationDegrees, Color color)
        {
            // Draw a rotating arc (quarter circle)
            float rotationRad = (float)(rotationDegrees * Math.PI / 180.0);
            int segments = 16;
            float arcLength = (float)(Math.PI / 2); // Quarter circle

            for (int i = 0; i < segments; i++)
            {
                float t1 = (float)i / segments;
                float t2 = (float)(i + 1) / segments;

                float angle1 = rotationRad + t1 * arcLength;
                float angle2 = rotationRad + t2 * arcLength;

                int x1 = x + (int)(Math.Cos(angle1) * radius);
                int y1 = y + (int)(Math.Sin(angle1) * radius);
                int x2 = x + (int)(Math.Cos(angle2) * radius);
                int y2 = y + (int)(Math.Sin(angle2) * radius);

                DrawLine(x1, y1, x2, y2, color, 4);
            }
        }

        private void DrawLine(int x1, int y1, int x2, int y2, Color color, int thickness)
        {
            float dx = x2 - x1;
            float dy = y2 - y1;
            float distance = (float)Math.Sqrt(dx * dx + dy * dy);

            if (distance == 0)
                return;

            float angle = (float)Math.Atan2(dy, dx);
            
            // Standard line drawing with scale and origin for perfect MonoGame rotation
            Vector2 position = new Vector2(x1, y1);
            Vector2 scale = new Vector2(distance, thickness);
            Vector2 origin = new Vector2(0f, 0.5f);

            _spriteBatch.Draw(_pixel, position, null, color, angle, origin, scale, SpriteEffects.None, 0);
        }

        private void DrawLoadingText(float fadeAlpha)
        {
            if (_fontRenderer == null) return;

            string loadingText = "Loading";
            int dots = ((int)(_elapsedTime / 500) % 4);
            loadingText += new string('.', dots);

            _fontRenderer.SetFontSize(24);
            _fontRenderer.DrawTextCentered(loadingText, _spinnerX, _spinnerY + SPINNER_RADIUS + 30, Color.White * fadeAlpha);
        }

        public void Dispose()
        {
            _pixel?.Dispose();
        }
    }
}
