#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using FontStashSharp;
using System;
using System.IO;

namespace Nullscent.UI
{
    /// <summary>
    /// Renderizador de texto usando fuentes TrueType/OpenType con FontStashSharp.
    /// Carga el archivo fuente.otf desde la raíz del proyecto.
    /// </summary>
    public class TrueTypeFontRenderer : IDisposable
    {
        private readonly GraphicsDevice _graphicsDevice;
        private readonly SpriteBatch _spriteBatch;
        private FontSystem? _fontSystem;
        private SpriteFontBase? _font;
        private Texture2D? _pixelTexture;

        public bool IsLoaded => _font != null;

        public TrueTypeFontRenderer(GraphicsDevice graphicsDevice, SpriteBatch spriteBatch)
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
        /// Carga una fuente TrueType/OpenType desde archivo.
        /// </summary>
        public bool LoadFont(string fontPath, int fontSize = 24)
        {
            try
            {
                if (!File.Exists(fontPath))
                {
                    Console.WriteLine($"[TrueTypeFontRenderer] Font file not found: {fontPath}");
                    return false;
                }

                // Crear FontSystem con configuración personalizada
                var settings = new FontSystemSettings
                {
                    FontResolutionFactor = 2,
                    KernelWidth = 2,
                    KernelHeight = 2
                };

                _fontSystem = new FontSystem(settings);

                // Cargar bytes de la fuente
                byte[] fontBytes = File.ReadAllBytes(fontPath);

                // Añadir fuente al sistema
                _fontSystem.AddFont(fontBytes);

                // Obtener fuente con el tamaño especificado
                _font = _fontSystem.GetFont(fontSize);

                Console.WriteLine($"[TrueTypeFontRenderer] Font loaded successfully: {fontPath} ({fontSize}px)");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[TrueTypeFontRenderer] Failed to load font: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Cambia el tamaño de la fuente dinámicamente.
        /// </summary>
        public void SetFontSize(int fontSize)
        {
            if (_fontSystem != null)
            {
                _font = _fontSystem.GetFont(fontSize);
            }
        }

        /// <summary>
        /// Dibuja texto en posición especificada.
        /// </summary>
        public void DrawText(string text, Vector2 position, Color color, float scale = 1.0f)
        {
            if (string.IsNullOrEmpty(text) || _font == null)
                return;

            // FontStashSharp: DrawText(SpriteBatch, text, position, color, scale, rotation, origin, layerDepth)
            _font.DrawText(_spriteBatch, text, position, color, scale: new Vector2(scale, scale));
        }

        /// <summary>
        /// Dibuja texto en posición especificada (sobrecarga con coordenadas int).
        /// </summary>
        public void DrawText(string text, int x, int y, Color color, float scale = 1.0f)
        {
            DrawText(text, new Vector2(x, y), color, scale);
        }

        /// <summary>
        /// Dibuja texto centrado en una posición.
        /// </summary>
        public void DrawTextCentered(string text, Vector2 position, Color color, float scale = 1.0f)
        {
            if (string.IsNullOrEmpty(text) || _font == null)
                return;

            var size = _font.MeasureString(text);
            var centeredPos = new Vector2(position.X - (size.X * scale) / 2f, position.Y - (size.Y * scale) / 2f);
            _font.DrawText(_spriteBatch, text, centeredPos, color, scale: new Vector2(scale, scale));
        }

        /// <summary>
        /// Dibuja texto centrado en una posición (sobrecarga con coordenadas int).
        /// </summary>
        public void DrawTextCentered(string text, int x, int y, Color color, float scale = 1.0f)
        {
            DrawTextCentered(text, new Vector2(x, y), color, scale);
        }

        /// <summary>
        /// Dibuja texto alineado a la derecha.
        /// </summary>
        public void DrawTextRight(string text, Vector2 position, Color color, float scale = 1.0f)
        {
            if (string.IsNullOrEmpty(text) || _font == null)
                return;

            var size = _font.MeasureString(text);
            var rightPos = new Vector2(position.X - (size.X * scale), position.Y);
            _font.DrawText(_spriteBatch, text, rightPos, color, scale: new Vector2(scale, scale));
        }

        /// <summary>
        /// Dibuja texto alineado a la derecha (sobrecarga con coordenadas int).
        /// </summary>
        public void DrawTextRight(string text, int x, int y, Color color, float scale = 1.0f)
        {
            DrawTextRight(text, new Vector2(x, y), color, scale);
        }

        /// <summary>
        /// Mide el tamaño del texto.
        /// </summary>
        public Vector2 MeasureText(string text, float scale = 1.0f)
        {
            if (string.IsNullOrEmpty(text) || _font == null)
                return Vector2.Zero;

            var size = _font.MeasureString(text);
            return new Vector2(size.X * scale, size.Y * scale);
        }

        /// <summary>
        /// Dibuja un rectángulo (para backgrounds, borders, etc.)
        /// </summary>
        public void DrawBox(Rectangle rect, Color color)
        {
            if (_pixelTexture != null)
            {
                _spriteBatch.Draw(_pixelTexture, rect, color);
            }
        }

        /// <summary>
        /// Dibuja un borde de rectángulo.
        /// </summary>
        public void DrawBoxBorder(Rectangle rect, Color color, int thickness = 2)
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

        public void Dispose()
        {
            _pixelTexture?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
