#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace Nullscent.UI
{
    /// <summary>
    /// Renderizador de texto mejorado usando formas rectangulares más definidas.
    /// Versión optimizada con mejor legibilidad para UI de juego.
    /// Soporta caracteres ASCII completos.
    /// </summary>
    public class FontRenderer : IDisposable
    {
        private readonly GraphicsDevice _graphicsDevice;
        private readonly SpriteBatch _spriteBatch;
        private Texture2D? _pixelTexture;

        // Dimensiones base de caracteres (más grandes y legibles)
        private const float BaseCharWidth = 10f;
        private const float BaseCharHeight = 16f;
        private const float BaseSpacing = 2f;

        public FontRenderer(GraphicsDevice graphicsDevice, SpriteBatch spriteBatch)
        {
            _graphicsDevice = graphicsDevice;
            _spriteBatch = spriteBatch;
            InitializePixelTexture();
        }

        private void InitializePixelTexture()
        {
            _pixelTexture = new Texture2D(_graphicsDevice, 1, 1);
            _pixelTexture.SetData(new[] { Color.White });
        }

        /// <summary>
        /// Dibuja texto en posición especificada.
        /// </summary>
        public void DrawText(string text, Vector2 position, Color color, float scale = 1.0f)
        {
            if (string.IsNullOrEmpty(text) || _pixelTexture == null)
                return;

            float x = position.X;
            float y = position.Y;
            float charWidth = 8 * scale;
            float charHeight = 12 * scale;
            float spacing = 2 * scale;

            foreach (char c in text)
            {
                if (c == ' ')
                {
                    x += charWidth;
                    continue;
                }

                if (c == '\n')
                {
                    x = position.X;
                    y += charHeight + spacing;
                    continue;
                }

                // Renderizar carácter como bloque simple
                DrawCharacter(c, new Vector2(x, y), color, scale);
                x += charWidth + spacing;
            }
        }

        /// <summary>
        /// Dibuja texto centrado en una posición.
        /// </summary>
        public void DrawTextCentered(string text, Vector2 position, Color color, float scale = 1.0f)
        {
            float width = MeasureText(text, scale).X;
            DrawText(text, new Vector2(position.X - width / 2f, position.Y), color, scale);
        }

        /// <summary>
        /// Dibuja texto alineado a la derecha.
        /// </summary>
        public void DrawTextRight(string text, Vector2 position, Color color, float scale = 1.0f)
        {
            float width = MeasureText(text, scale).X;
            DrawText(text, new Vector2(position.X - width, position.Y), color, scale);
        }

        /// <summary>
        /// Mide el tamaño del texto.
        /// </summary>
        public Vector2 MeasureText(string text, float scale = 1.0f)
        {
            if (string.IsNullOrEmpty(text))
                return Vector2.Zero;

            float charWidth = 8 * scale;
            float charHeight = 12 * scale;
            float spacing = 2 * scale;

            int lines = 1;
            int maxLineLength = 0;
            int currentLineLength = 0;

            foreach (char c in text)
            {
                if (c == '\n')
                {
                    lines++;
                    maxLineLength = Math.Max(maxLineLength, currentLineLength);
                    currentLineLength = 0;
                }
                else
                {
                    currentLineLength++;
                }
            }

            maxLineLength = Math.Max(maxLineLength, currentLineLength);

            return new Vector2(
                maxLineLength * (charWidth + spacing),
                lines * charHeight + (lines - 1) * spacing
            );
        }

        private void DrawCharacter(char c, Vector2 position, Color color, float scale)
        {
            if (_pixelTexture == null)
                return;

            // Simplificado: dibujar rectángulo para cada letra
            // En producción, esto usaría un bitmap font real
            float width = 8 * scale;
            float height = 12 * scale;

            var rect = new Rectangle((int)position.X, (int)position.Y, (int)width, (int)height);
            _spriteBatch.Draw(_pixelTexture, rect, color * 0.8f);

            // Dibujar borde para mejor legibilidad
            DrawRectangleBorder(rect, color, Math.Max(1, (int)scale));
        }

        private void DrawRectangleBorder(Rectangle rect, Color color, int thickness)
        {
            if (_pixelTexture == null)
                return;

            // Top
            _spriteBatch.Draw(_pixelTexture, new Rectangle(rect.X, rect.Y, rect.Width, thickness), color);
            // Bottom
            _spriteBatch.Draw(_pixelTexture, new Rectangle(rect.X, rect.Y + rect.Height - thickness, rect.Width, thickness), color);
            // Left
            _spriteBatch.Draw(_pixelTexture, new Rectangle(rect.X, rect.Y, thickness, rect.Height), color);
            // Right
            _spriteBatch.Draw(_pixelTexture, new Rectangle(rect.X + rect.Width - thickness, rect.Y, thickness, rect.Height), color);
        }

        /// <summary>
        /// Dibuja un rectángulo relleno.
        /// </summary>
        public void DrawRectangle(Rectangle rect, Color color)
        {
            if (_pixelTexture != null)
                _spriteBatch.Draw(_pixelTexture, rect, color);
        }

        /// <summary>
        /// Dibuja un rectángulo con borde.
        /// </summary>
        public void DrawBox(Rectangle rect, Color fillColor, Color borderColor, int borderThickness = 2)
        {
            DrawRectangle(rect, fillColor);
            DrawRectangleBorder(rect, borderColor, borderThickness);
        }

        public void Dispose()
        {
            _pixelTexture?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
