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
    public class LoadingScreen
    {
        private readonly GraphicsDevice _graphicsDevice;
        private readonly SpriteBatch _spriteBatch;
        private readonly Texture2D _pixel;

        // Spinner properties
        private double _spinnerRotation;
        private const double SPINNER_ROTATION_SPEED = 360.0; // degrees per second
        private double _elapsedTime;
        private double _totalDuration; // milliseconds

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

        public LoadingScreen(GraphicsDevice graphicsDevice, SpriteBatch spriteBatch, double durationMs = 3000)
        {
            _graphicsDevice = graphicsDevice;
            _spriteBatch = spriteBatch;
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
            _elapsedTime += deltaSeconds * 1000; // Convert to milliseconds

            // Update spinner rotation
            _spinnerRotation += SPINNER_ROTATION_SPEED * deltaSeconds;
            if (_spinnerRotation >= 360)
                _spinnerRotation -= 360;

            // Check if duration complete
            if (_elapsedTime >= _totalDuration)
            {
                _isActive = false;
            }
        }

        public void Draw()
        {
            if (!_isActive)
                return;

            // Calculate progress for fade effects
            float progress = (float)(_elapsedTime / _totalDuration);

            _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied);

            // Draw background (semi-transparent dark)
            var bgRect = new Rectangle(0, 0, _screenWidth, _screenHeight);
            _spriteBatch.Draw(_pixel, bgRect, new Color(15, 15, 20, 220));

            // Draw spinner circle (animated)
            DrawSpinner(progress);

            // Draw loading text
            DrawLoadingText();

            _spriteBatch.End();
        }

        private void DrawSpinner(float progress)
        {
            // Draw outer ring (static)
            DrawCircleOutline(_spinnerX, _spinnerY, SPINNER_RADIUS, Color.White * 0.3f, 3);

            // Draw rotating arc
            DrawRotatingArc(_spinnerX, _spinnerY, SPINNER_RADIUS, (float)_spinnerRotation, Color.Cyan);

            // Draw inner circle
            DrawCircleOutline(_spinnerX, _spinnerY, SPINNER_RADIUS / 2, Color.White * 0.5f, 2);
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
            var rect = new Rectangle(
                x1,
                y1 - thickness / 2,
                (int)distance,
                thickness
            );

            Matrix transform = Matrix.CreateRotationZ(angle) * 
                              Matrix.CreateTranslation(0, 0, 0);

            _spriteBatch.Draw(_pixel, rect, null, color, angle, Vector2.Zero, SpriteEffects.None, 0);
        }

        private void DrawLoadingText()
        {
            // Simple loading text - would use font renderer in production
            // For now, just draw a simple indicator

            // Draw "Loading..." text or dots animation
            string loadingText = "Loading";
            int dots = ((int)(_elapsedTime / 500) % 4);
            loadingText += new string('.', dots);

            // This would need a font renderer to display properly
            // Placeholder for now
        }

        public void Dispose()
        {
            _pixel?.Dispose();
        }
    }
}
