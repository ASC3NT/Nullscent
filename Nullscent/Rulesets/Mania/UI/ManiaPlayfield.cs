#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Nullscent.Rulesets.Mania.Judgements;
using Nullscent.Rulesets.Mania.Objects;
using Nullscent.Rulesets.Mania.Scoring;
using Nullscent.Rulesets.Mania.UI.Drawables;
using Nullscent.Beatmap;
using Nullscent.UI;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Nullscent.Rulesets.Mania.UI
{
    /// <summary>
    /// Representa el área de juego principal para Mania. Contiene las columnas y gestiona el renderizado de notas.
    /// </summary>
    public class ManiaPlayfield : IDisposable
    {
        private Nullscent.Beatmap.Beatmap? _beatmap;
        public Nullscent.Beatmap.Beatmap? Beatmap
        {
            get => _beatmap;
            set
            {
                _beatmap = value;
                if (_columns != null)
                {
                    foreach (var col in _columns)
                    {
                        col.Beatmap = value;
                    }
                }
            }
        }
        private readonly GraphicsDevice _graphicsDevice;
        private readonly SpriteBatch _spriteBatch;
        private readonly List<Column> _columns;
        private readonly int _columnCount;
        private double _overallDifficulty = 5.0;

        // Propiedades visuales de la pista (stage)
        private int _stageX;
        private int _stageWidth;
        private int _screenHeight;
        public int ReceptorY { get; private set; }

        // Recursos compartidos de renderizado
        private Texture2D? _whitePixel;
        private Texture2D? _radialGlow;
        private Texture2D? _linearGradient;

        // Renderizador de fuentes y gestor de juicios
        private readonly TrueTypeFontRenderer? _fontRenderer;
        private JudgementDisplayManager? _judgementDisplayManager;

        // Propiedad expuesta para acceder al gestor de juicios desde el exterior
        public JudgementDisplayManager? JudgementDisplayManager => _judgementDisplayManager;

        private Texture2D CreateRadialGlowTexture(GraphicsDevice graphicsDevice, int size)
        {
            Texture2D texture = new Texture2D(graphicsDevice, size, size);
            Color[] data = new Color[size * size];
            float center = size / 2.0f;
            float maxDistance = size / 2.0f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - center;
                    float dy = y - center;
                    float distance = (float)Math.Sqrt(dx * dx + dy * dy);
                    float alpha = 1.0f - Math.Clamp(distance / maxDistance, 0.0f, 1.0f);
                    alpha = alpha * alpha; // Suavizar
                    data[y * size + x] = new Color(255, 255, 255, (int)(255 * alpha));
                }
            }

            texture.SetData(data);
            return texture;
        }

        private Texture2D CreateLinearGradientTexture(GraphicsDevice graphicsDevice, int width, int height)
        {
            Texture2D texture = new Texture2D(graphicsDevice, width, height);
            Color[] data = new Color[width * height];

            for (int y = 0; y < height; y++)
            {
                // El degradado disminuye hacia la parte superior (y = 0 es la parte de arriba del degradado)
                float alpha = 1.0f - ((float)y / height);
                alpha = alpha * alpha; // Suavizar
                for (int x = 0; x < width; x++)
                {
                    data[y * width + x] = new Color(255, 255, 255, (int)(255 * alpha));
                }
            }

            texture.SetData(data);
            return texture;
        }

        public ManiaPlayfield(
            GraphicsDevice graphicsDevice,
            SpriteBatch spriteBatch,
            int columnCount,
            int screenWidth,
            int screenHeight,
            TrueTypeFontRenderer? fontRenderer = null,
            double overallDifficulty = 5.0)
        {
            _graphicsDevice = graphicsDevice;
            _spriteBatch = spriteBatch;
            _columnCount = columnCount;
            _screenHeight = screenHeight;
            _fontRenderer = fontRenderer;
            _overallDifficulty = overallDifficulty;

            // Calcular dimensiones de la pista (centrada en pantalla)
            int columnWidth = 80;
            _stageWidth = columnWidth * columnCount;
            _stageX = (screenWidth - _stageWidth) / 2;

            // Inicializar textura base para primitivas y texturas generadas
            _whitePixel = new Texture2D(graphicsDevice, 1, 1);
            _whitePixel.SetData(new[] { Color.White });
            _radialGlow = CreateRadialGlowTexture(graphicsDevice, 64);
            _linearGradient = CreateLinearGradientTexture(graphicsDevice, 32, 256);

            // Inicializar las columnas
            _columns = new List<Column>();
            for (int i = 0; i < columnCount; i++)
            {
                int x = _stageX + (i * columnWidth);
                _columns.Add(new Column(i, x, columnWidth, screenHeight, _whitePixel, overallDifficulty, columnCount));
            }

            // Crear el gestor central de juicios y posicionarlo cerca de la zona de impacto
            _judgementDisplayManager = new JudgementDisplayManager(_graphicsDevice, _spriteBatch, _fontRenderer);
            _judgementDisplayManager.Position = new Vector2(_stageX + _stageWidth / 2f, (float)(_screenHeight * 0.45));

            // Asignar el gestor a todas las columnas
            foreach (var col in _columns)
            {
                col.SetJudgementDisplayManager(_judgementDisplayManager);
            }

            Console.WriteLine($"[ManiaPlayfield] Creadas {columnCount} columnas, ancho pista: {_stageWidth}px en x={_stageX}");
        }

        public void AddHitObject(ManiaHitObject hitObject)
        {
            // Convertir HoldNote genérico a LongNote inyectando el OD del mapa
            if (hitObject is HoldNote holdNote)
            {
                hitObject = new LongNote(holdNote.StartTime, holdNote.EndTime, holdNote.Column, _overallDifficulty)
                {
                    Samples = holdNote.Samples
                };
            }

            // Distribuir la nota a su columna correspondiente
            if (hitObject.Column >= 0 && hitObject.Column < _columns.Count)
            {
                _columns[hitObject.Column].AddHitObject(hitObject);
            }
        }

        public void Update(double currentTime, ManiaScoreProcessor scoreProcessor, double scrollSpeed, float receptorPosition, bool downScroll)
        {
            int currentWidth = _graphicsDevice.Viewport.Width;
            int currentHeight = _graphicsDevice.Viewport.Height;

            // Check if window was resized and update layout
            if (_screenHeight != currentHeight || _stageWidth != 80 * _columnCount || _stageX != (currentWidth - _stageWidth) / 2)
            {
                _screenHeight = currentHeight;
                int columnWidth = 80;
                _stageWidth = columnWidth * _columnCount;
                _stageX = (currentWidth - _stageWidth) / 2;

                for (int i = 0; i < _columns.Count; i++)
                {
                    _columns[i].X = _stageX + (i * columnWidth);
                    _columns[i].ScreenHeight = currentHeight;
                }

                if (_judgementDisplayManager != null)
                {
                    _judgementDisplayManager.Position = new Vector2(_stageX + _stageWidth / 2f, (float)(currentHeight * 0.45));
                }
            }

            // Actualizar la posición de los receptores en el eje Y
            ReceptorY = (int)(receptorPosition * _screenHeight);

            // Propagar actualización a cada columna
            foreach (var column in _columns)
            {
                column.Update(currentTime, scoreProcessor, scrollSpeed, receptorPosition, downScroll, ReceptorY);
            }

            // Actualizar el visualizador de juicios
            _judgementDisplayManager?.Update(currentTime);
        }

        public void HandleKeyPress(int columnIndex, double currentTime, ManiaScoreProcessor scoreProcessor)
        {
            if (columnIndex >= 0 && columnIndex < _columns.Count)
            {
                _columns[columnIndex].OnKeyPress(currentTime, scoreProcessor);
            }
        }

        public void HandleKeyRelease(int columnIndex, double currentTime, ManiaScoreProcessor scoreProcessor)
        {
            if (columnIndex >= 0 && columnIndex < _columns.Count)
            {
                _columns[columnIndex].OnKeyRelease(currentTime, scoreProcessor);
            }
        }

        public void Draw(double currentTime, double scrollSpeed, float receptorPosition, bool downScroll)
        {
            if (_whitePixel == null || _radialGlow == null || _linearGradient == null) return;

            _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied);

            DrawStageBackground();

            // Dibujar las columnas
            foreach (var column in _columns)
            {
                column.Draw(_spriteBatch, _whitePixel, _radialGlow, _linearGradient, _fontRenderer, currentTime, scrollSpeed, receptorPosition, downScroll, ReceptorY);
            }

            // Dibujar el gestor de juicios por encima de la pista
            _judgementDisplayManager?.Draw(currentTime);

            _spriteBatch.End();
        }

        private void DrawStageBackground()
        {
            if (_whitePixel == null) return;

            // Fondo oscuro para la pista
            var stageRect = new Rectangle(_stageX, 0, _stageWidth, _screenHeight);
            _spriteBatch.Draw(_whitePixel, stageRect, new Color(15, 15, 20, 220));

            // Dibujar separadores de columnas
            for (int i = 1; i < _columnCount; i++)
            {
                int separatorX = _columns[i].X;
                var separatorRect = new Rectangle(separatorX, 0, 2, _screenHeight);
                _spriteBatch.Draw(_whitePixel, separatorRect, new Color(40, 40, 50, 255));
            }

            // Dibujar bordes laterales de pista (Stage Sidebars) - estilo neón brillante (rosa)
            var leftBorderRect = new Rectangle(_stageX - 4, 0, 4, _screenHeight);
            var rightBorderRect = new Rectangle(_stageX + _stageWidth, 0, 4, _screenHeight);
            Color neonColor = new Color(255, 102, 171); // Magenta de osu!

            _spriteBatch.Draw(_whitePixel, leftBorderRect, neonColor);
            _spriteBatch.Draw(_whitePixel, rightBorderRect, neonColor);
        }

        public void Dispose()
        {
            _whitePixel?.Dispose();
            _radialGlow?.Dispose();
            _linearGradient?.Dispose();
            foreach (var column in _columns)
            {
                column.Dispose();
            }
        }
    }

    /// <summary>
    /// Representa una única columna dentro del playfield.
    /// Gestiona de forma individual el ciclo de vida de los objetos interactuables que caen por ella.
    /// </summary>
    public class Column : IDisposable
    {
        public Nullscent.Beatmap.Beatmap? Beatmap { get; set; }
        private readonly int _index;
        private int _x;
        private readonly int _width;
        private int _screenHeight;
        private readonly Texture2D _pixel;

        // Contenedores de elementos visuales (drawables)
        private readonly List<DrawableManiaHitObject> _drawableHitObjects = new();
        private readonly List<HitExplosion> _explosions = new();
        private JudgementDisplayManager? _judgementDisplayManager;

        private readonly ManiaHitWindows _hitWindows;

        private bool _isPressed;
        private double _lightingEndTime;
        private int _totalColumnCount;

        public int X { get => _x; set => _x = value; }
        public int ScreenHeight { get => _screenHeight; set => _screenHeight = value; }

        public Column(int index, int x, int width, int screenHeight, Texture2D pixel, double overallDifficulty, int totalColumnCount = 4)
        {
            _index = index;
            _x = x;
            _width = width;
            _screenHeight = screenHeight;
            _pixel = pixel;
            _hitWindows = new ManiaHitWindows(overallDifficulty);
            _totalColumnCount = totalColumnCount;
        }

        public void SetJudgementDisplayManager(JudgementDisplayManager? manager)
        {
            _judgementDisplayManager = manager;
        }

        public void AddHitObject(ManiaHitObject hitObject)
        {
            DrawableManiaHitObject? drawable = null;

            // Envolver la lógica del HitObject puro en una clase Drawable según su tipo
            if (hitObject is LongNote longNote)
            {
                drawable = new DrawableLongNote(longNote);
            }
            else if (hitObject is Note note)
            {
                drawable = new DrawableNote(note);
            }
            else if (hitObject is HoldNote holdNote)
            {
                drawable = new DrawableHoldNote(holdNote);
            }

            if (drawable != null)
            {
                drawable.Beatmap = this.Beatmap;
                _drawableHitObjects.Add(drawable);
            }
        }

        public void Update(double currentTime, ManiaScoreProcessor scoreProcessor, double scrollSpeed, float receptorPosition, bool downScroll, int receptorY)
        {
            foreach (var drawable in _drawableHitObjects.ToList())
            {
                drawable.Update(currentTime, scrollSpeed, receptorPosition, downScroll, _screenHeight);

                // Manejo especial para el estado de presión de las notas largas (LN)
                if (drawable is DrawableLongNote drawableLN)
                {
                    drawableLN.UpdateHold(currentTime, _isPressed);
                    drawableLN.CheckAutoMiss(currentTime, _hitWindows);

                    if (drawableLN.HitObject is LongNote ln)
                    {
                        // Check if head was judged but not yet applied
                        if (ln.HeadJudged && !drawableLN.IsHeadApplied)
                        {
                            scoreProcessor.ApplyResult(ln.HeadResult);
                            
                            // Only add explosion if it's not a Miss
                            if (ln.HeadResult != HitResult.Miss)
                            {
                                _explosions.Add(new HitExplosion(ln.HeadResult, currentTime));
                            }
                            
                            int headOffset = (int)Math.Truncate(ln.HeadHitTime - ln.StartTime);
                            _judgementDisplayManager?.EnqueueJudgement(ln.HeadResult, _index, headOffset, scoreProcessor.Combo, currentTime);
                            
                            drawableLN.IsHeadApplied = true;
                        }

                        // Check if tail was judged but not yet applied
                        if (ln.TailJudged && !drawableLN.IsJudged)
                        {
                            scoreProcessor.ApplyResult(ln.TailResult);
                            
                            if (ln.TailResult != HitResult.Miss)
                            {
                                _explosions.Add(new HitExplosion(ln.TailResult, currentTime));
                            }

                            int tailOffset = (int)Math.Truncate(ln.TailReleaseTime - ln.EndTime);
                            _judgementDisplayManager?.EnqueueJudgement(ln.TailResult, _index, tailOffset, scoreProcessor.Combo, currentTime);

                            drawableLN.IsJudged = true;
                            _drawableHitObjects.Remove(drawable);
                            drawable.Dispose();
                        }
                    }
                }
                else
                {
                    // Check for auto-miss for regular notes (and other non-LN drawable hit objects)
                    drawable.CheckForResult(currentTime, _hitWindows);

                    if (drawable.HasResult)
                    {
                        if (drawable.Result.HasValue && drawable.Result.Value == HitResult.Miss)
                        {
                            _judgementDisplayManager?.EnqueueJudgement(drawable.Result.Value, _index, 0, scoreProcessor.Combo, currentTime);
                        }

                        OnHitObjectJudged(drawable, currentTime, scoreProcessor);
                        _drawableHitObjects.Remove(drawable);
                        drawable.Dispose();
                    }
                }
            }

            // Actualizar ciclo de vida de explosiones visuales
            foreach (var explosion in _explosions.ToList())
            {
                explosion.Update(currentTime);
                if (!explosion.IsActive)
                    _explosions.Remove(explosion);
            }
        }

        public void OnKeyPress(double currentTime, ManiaScoreProcessor scoreProcessor)
        {
            _isPressed = true;

            // Localizar el objeto interactuable más cercano a la zona de impacto
            DrawableManiaHitObject? closest = null;
            double closestDistance = double.MaxValue;

            foreach (var drawable in _drawableHitObjects)
            {
                if (drawable.HasResult) continue;

                double distance = Math.Abs(currentTime - drawable.HitObject.StartTime);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closest = drawable;
                }
            }

            if (closest != null)
            {
                if (closest is DrawableLongNote drawableLN)
                {
                    closest.CheckForResult(currentTime, _hitWindows);

                    if (drawableLN.HitObject is LongNote ln && ln.HeadJudged && ln.HeadResult != HitResult.Miss)
                    {
                        // Activar iluminación de la columna al acertar la cabeza de la LN
                        _lightingEndTime = currentTime + 150;
                    }
                }
                else
                {
                    // Lógica para notas normales
                    double offset = currentTime - closest.HitObject.StartTime;
                    var result = _hitWindows.JudgeHit(offset);

                    // Descartar inputs fuera de la ventana de tiempo válida
                    if (result != HitResult.None && result != HitResult.Miss)
                    {
                        closest.CheckForResult(currentTime, _hitWindows);

                        if (closest.HasResult)
                        {
                            if (closest.Result.HasValue)
                            {
                                _judgementDisplayManager?.EnqueueJudgement(closest.Result.Value, _index, (int)Math.Truncate(currentTime - closest.HitObject.StartTime), scoreProcessor.Combo, currentTime);
                            }

                            OnHitObjectJudged(closest, currentTime, scoreProcessor);
                            _drawableHitObjects.Remove(closest);

                            _lightingEndTime = currentTime + 150;
                        }
                    }
                }
            }
        }

        public void OnKeyRelease(double currentTime, ManiaScoreProcessor scoreProcessor)
        {
            _isPressed = false;

            // Evaluar los resultados de soltar el teclado para las colas de las LN
            foreach (var drawable in _drawableHitObjects.ToList())
            {
                if (drawable is DrawableLongNote drawableLN)
                {
                    drawableLN.OnKeyRelease(currentTime, _hitWindows);
                }
            }
        }

        private void OnHitObjectJudged(DrawableManiaHitObject drawable, double currentTime, ManiaScoreProcessor scoreProcessor)
        {
            if (!drawable.Result.HasValue) return;

            var result = drawable.Result.Value;

            scoreProcessor.ApplyResult(result);
            if (result != HitResult.Miss)
            {
                _explosions.Add(new HitExplosion(result, currentTime));
            }

            Console.WriteLine($"[Columna {_index}] Juicio: {result} a los {currentTime:F2}ms");
        }

        private Color GetLaneColor(int index)
        {
            if (_totalColumnCount == 4)
            {
                if (index == 0 || index == 3) return new Color(84, 200, 255); // Cyan
                return new Color(255, 102, 171); // Pink
            }
            else if (_totalColumnCount == 7)
            {
                if (index == 3) return Color.White;
                if (index == 1 || index == 5) return new Color(84, 200, 255); // Cyan
                return new Color(255, 102, 171); // Pink
            }
            else
            {
                return index % 2 == 0 ? new Color(255, 102, 171) : new Color(84, 200, 255);
            }
        }

        public void Draw(SpriteBatch spriteBatch, Texture2D pixel, Texture2D radialGlow, Texture2D linearGradient, TrueTypeFontRenderer? fontRenderer,
            double currentTime, double scrollSpeed, float receptorPosition, bool downScroll, int receptorY)
        {
            // Fondo de la columna alternando colores para distinguir separación
            Color columnBg = _index % 2 == 0 ? new Color(20, 20, 25, 180) : new Color(25, 25, 30, 180);
            var columnRect = new Rectangle(_x, 0, _width, _screenHeight);
            spriteBatch.Draw(pixel, columnRect, columnBg);

            // Ajuste de matriz para renderizado relativo a las coordenadas de la columna
            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied,
                transformMatrix: Matrix.CreateTranslation(_x, 0, 0));

            foreach (var drawable in _drawableHitObjects)
            {
                drawable.Draw(spriteBatch, pixel, currentTime, scrollSpeed, receptorY, downScroll, _screenHeight, _width);
            }

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied);

            // Dibujar un receptor con borde y fondo semi-transparente (estilo osu!)
            var keyRect = new Rectangle(_x + 3, receptorY - 10, _width - 6, 20);
            Color keyBgColor = _isPressed ? GetLaneColor(_index) * 0.4f : Color.Black * 0.5f;
            spriteBatch.Draw(pixel, keyRect, keyBgColor);
            
            Color keyBorderColor = _isPressed ? GetLaneColor(_index) : Color.White * 0.7f;
            // Dibujar bordes de receptor mecánicos
            spriteBatch.Draw(pixel, new Rectangle(keyRect.X, keyRect.Y, keyRect.Width, 2), keyBorderColor);
            spriteBatch.Draw(pixel, new Rectangle(keyRect.X, keyRect.Y + keyRect.Height - 2, keyRect.Width, 2), keyBorderColor);
            spriteBatch.Draw(pixel, new Rectangle(keyRect.X, keyRect.Y, 2, keyRect.Height), keyBorderColor);
            spriteBatch.Draw(pixel, new Rectangle(keyRect.X + keyRect.Width - 2, keyRect.Y, 2, keyRect.Height), keyBorderColor);

            // Renderizar iluminación de tecla presionada (haz de luz degradado)
            if (_isPressed || currentTime < _lightingEndTime)
            {
                float age = (float)(_lightingEndTime - currentTime);
                float alpha = _isPressed ? 0.35f : Math.Clamp(age / 150f * 0.35f, 0.0f, 0.35f);
                Color laneColor = GetLaneColor(_index);
                
                Rectangle lightRect;
                SpriteEffects effects;
                if (downScroll)
                {
                    lightRect = new Rectangle(_x + 2, receptorY - 220, _width - 4, 220);
                    effects = SpriteEffects.FlipVertically;
                }
                else
                {
                    lightRect = new Rectangle(_x + 2, receptorY, _width - 4, 220);
                    effects = SpriteEffects.None;
                }

                spriteBatch.Draw(linearGradient, lightRect, null, laneColor * alpha, 0f, Vector2.Zero, effects, 0f);
            }

            // Renderizar explosiones de acierto (usando el orbe de brillo radial)
            foreach (var explosion in _explosions.ToArray())
            {
                explosion.Draw(spriteBatch, pixel, radialGlow, _x, _width, receptorY, currentTime);
            }
        }

        public void Dispose()
        {
            foreach (var drawable in _drawableHitObjects)
            {
                drawable.Dispose();
            }
            _drawableHitObjects.Clear();
        }
    }
}