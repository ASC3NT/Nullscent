#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Nullscent.Beatmap;
using System;

namespace Nullscent.Gameplay
{
    /// <summary>
    /// Renderiza notas y columnas en la pantalla de gameplay.
    /// Maneja la visualización de notas normales, long notes (hold bodies), receptores y efectos visuales.
    /// </summary>
    public class NoteRenderer
    {
        private readonly GraphicsDevice _graphicsDevice;
        private readonly SpriteBatch _spriteBatch;
        private Texture2D? _pixelTexture;

        /// <summary>
        /// Paleta de colores para columnas (cicla según índice de columna).
        /// </summary>
        private readonly Color[] _columnColors = new[]
        {
            new Color(70, 70, 90),      // Gris azulado
            new Color(90, 70, 70),      // Gris rojizo
            new Color(70, 90, 70),      // Gris verdoso
            new Color(90, 90, 70),      // Gris amarillento
        };

        public NoteRenderer(GraphicsDevice graphicsDevice, SpriteBatch spriteBatch)
        {
            _graphicsDevice = graphicsDevice;
            _spriteBatch = spriteBatch;
            InitializePixelTexture();
        }

        /// <summary>
        /// Inicializa una textura de 1x1 píxel para dibujar rectángulos sólidos.
        /// </summary>
        private void InitializePixelTexture()
        {
            _pixelTexture = new Texture2D(_graphicsDevice, 1, 1);
            _pixelTexture.SetData(new[] { Color.White });
        }

        /// <summary>
        /// Dibuja todas las columnas y sus notas.
        /// </summary>
        public void DrawColumns(Column[] columns, double currentTimeMs, int scrollSpeed, float receptorY, int screenHeight)
        {
            if (_pixelTexture == null) return;

            // Calcular visible range (tiempo de notas visible en pantalla)
            double visibleMs = 3000.0 / scrollSpeed;

            _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);

            foreach (var column in columns)
            {
                // Dibujar fondo de columna
                DrawColumnBackground(column, screenHeight);

                // Dibujar receptor
                DrawReceptor(column, receptorY);

                // Dibujar notas de esta columna
                var visibleNotes = column.GetVisibleNotes(currentTimeMs, visibleMs);
                foreach (var note in visibleNotes)
                {
                    DrawNote(note, column, currentTimeMs, visibleMs, receptorY);
                }
            }

            _spriteBatch.End();
        }

        /// <summary>
        /// Dibuja el fondo de una columna (alternando colores).
        /// </summary>
        private void DrawColumnBackground(Column column, int screenHeight)
        {
            if (_pixelTexture == null) return;

            Color bgColor = _columnColors[column.Index % _columnColors.Length];
            var rect = new Rectangle((int)column.X, 0, (int)column.Width, screenHeight);
            _spriteBatch.Draw(_pixelTexture, rect, bgColor);

            // Borde derecho de la columna
            var borderRect = new Rectangle((int)(column.X + column.Width - 2), 0, 2, screenHeight);
            _spriteBatch.Draw(_pixelTexture, borderRect, new Color(50, 50, 50));
        }

        /// <summary>
        /// Dibuja el receptor (hit position) de una columna.
        /// </summary>
        private void DrawReceptor(Column column, float receptorY)
        {
            if (_pixelTexture == null) return;

            Color receptorColor = column.IsPressed ? Color.White : new Color(200, 200, 255);
            int receptorHeight = 8;

            var rect = new Rectangle(
                (int)column.X,
                (int)receptorY - receptorHeight / 2,
                (int)column.Width,
                receptorHeight
            );

            _spriteBatch.Draw(_pixelTexture, rect, receptorColor);

            // Highlight si está presionado
            if (column.IsPressed)
            {
                var highlightRect = new Rectangle(
                    (int)column.X,
                    (int)receptorY - 30,
                    (int)column.Width,
                    60
                );
                _spriteBatch.Draw(_pixelTexture, highlightRect, new Color(255, 255, 255, 50));
            }
        }

        /// <summary>
        /// Dibuja una nota (normal o long note).
        /// </summary>
        private void DrawNote(HitObject note, Column column, double currentTimeMs, double visibleMs, float receptorY)
        {
            if (_pixelTexture == null) return;

            // Calcular posición Y de la nota
            float noteY = CalculateNoteY(note.Time, currentTimeMs, visibleMs, receptorY);

            if (note.IsLongNote)
            {
                DrawLongNote(note, column, currentTimeMs, visibleMs, receptorY, noteY);
            }
            else
            {
                DrawNormalNote(note, column, noteY);
            }
        }

        /// <summary>
        /// Dibuja una nota normal (tap).
        /// </summary>
        private void DrawNormalNote(HitObject note, Column column, float noteY)
        {
            if (_pixelTexture == null) return;

            int noteHeight = 12;
            Color noteColor = note.IsJudged ? new Color(100, 100, 100, 100) : Color.Cyan;

            var rect = new Rectangle(
                (int)column.X + 4,
                (int)noteY - noteHeight / 2,
                (int)column.Width - 8,
                noteHeight
            );

            _spriteBatch.Draw(_pixelTexture, rect, noteColor);

            // Borde
            DrawRectangleBorder(rect, Color.White, 1);
        }

        /// <summary>
        /// Dibuja una long note (hold).
        /// </summary>
        private void DrawLongNote(HitObject note, Column column, double currentTimeMs, double visibleMs, float receptorY, float headY)
        {
            if (_pixelTexture == null) return;

            float tailY = CalculateNoteY(note.EndTime, currentTimeMs, visibleMs, receptorY);

            // Body
            float bodyTop = Math.Min(headY, tailY);
            float bodyHeight = Math.Abs(headY - tailY);

            Color bodyColor = note.IsTailJudged ? new Color(100, 100, 100, 100) : new Color(255, 165, 0, 150);

            var bodyRect = new Rectangle(
                (int)column.X + 8,
                (int)bodyTop,
                (int)column.Width - 16,
                (int)bodyHeight
            );

            _spriteBatch.Draw(_pixelTexture, bodyRect, bodyColor);

            // Head
            if (!note.IsJudged)
            {
                int headHeight = 12;
                var headRect = new Rectangle(
                    (int)column.X + 4,
                    (int)headY - headHeight / 2,
                    (int)column.Width - 8,
                    headHeight
                );
                _spriteBatch.Draw(_pixelTexture, headRect, Color.Orange);
                DrawRectangleBorder(headRect, Color.White, 1);
            }

            // Tail
            if (!note.IsTailJudged)
            {
                int tailHeight = 10;
                var tailRect = new Rectangle(
                    (int)column.X + 4,
                    (int)tailY - tailHeight / 2,
                    (int)column.Width - 8,
                    tailHeight
                );
                _spriteBatch.Draw(_pixelTexture, tailRect, Color.Yellow);
                DrawRectangleBorder(tailRect, Color.White, 1);
            }
        }

        /// <summary>
        /// Calcula la posición Y de una nota en pantalla según su tiempo.
        /// </summary>
        private float CalculateNoteY(int noteTime, double currentTimeMs, double visibleMs, float receptorY)
        {
            double timeDiff = noteTime - currentTimeMs;
            float progress = (float)(timeDiff / visibleMs);
            return receptorY - (progress * receptorY);
        }

        /// <summary>
        /// Dibuja el borde de un rectángulo.
        /// </summary>
        private void DrawRectangleBorder(Rectangle rect, Color color, int thickness)
        {
            if (_pixelTexture == null) return;

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
        }
    }
}
