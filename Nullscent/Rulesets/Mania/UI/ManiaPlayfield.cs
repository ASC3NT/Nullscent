#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Nullscent.Rulesets.Mania.Judgements;
using Nullscent.Rulesets.Mania.Objects;
using Nullscent.Rulesets.Mania.Scoring;
using Nullscent.Rulesets.Mania.UI.Drawables;
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
        private readonly GraphicsDevice _graphicsDevice;
        private readonly SpriteBatch _spriteBatch;
        private readonly List<Column> _columns;
        private readonly int _columnCount;
        private double _overallDifficulty = 5.0;

        // Propiedades visuales de la pista (stage)
        private readonly int _stageX;
        private readonly int _stageWidth;
        private readonly int _screenHeight;
        public int ReceptorY { get; private set; }

        // Recursos compartidos de renderizado
        private Texture2D? _whitePixel;

        // Renderizador de fuentes y gestor de juicios
        private readonly TrueTypeFontRenderer? _fontRenderer;
        private JudgementDisplayManager? _judgementDisplayManager;

        // Propiedad expuesta para acceder al gestor de juicios desde el exterior
        public JudgementDisplayManager? JudgementDisplayManager => _judgementDisplayManager;

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

            // Inicializar textura base para primitivas
            _whitePixel = new Texture2D(graphicsDevice, 1, 1);
            _whitePixel.SetData(new[] { Color.White });

            // Inicializar las columnas
            _columns = new List<Column>();
            for (int i = 0; i < columnCount; i++)
            {
                int x = _stageX + (i * columnWidth);
                _columns.Add(new Column(i, x, columnWidth, screenHeight, _whitePixel));
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
            if (_whitePixel == null) return;

            _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied);

            DrawStageBackground();

            // Dibujar las columnas
            foreach (var column in _columns)
            {
                column.Draw(_spriteBatch, _whitePixel, _fontRenderer, currentTime, scrollSpeed, receptorPosition, downScroll, ReceptorY);
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
        }

        public void Dispose()
        {
            _whitePixel?.Dispose();
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
        private readonly int _index;
        private readonly int _x;
        private readonly int _width;
        private readonly int _screenHeight;
        private readonly Texture2D _pixel;

        // Contenedores de elementos visuales (drawables)
        private readonly List<DrawableManiaHitObject> _drawableHitObjects = new();
        private readonly List<HitExplosion> _explosions = new();
        private JudgementDisplayManager? _judgementDisplayManager;

        private readonly ManiaHitWindows _hitWindows;

        private bool _isPressed;
        private double _lightingEndTime;

        public int X => _x;

        public Column(int index, int x, int width, int screenHeight, Texture2D pixel)
        {
            _index = index;
            _x = x;
            _width = width;
            _screenHeight = screenHeight;
            _pixel = pixel;
            _hitWindows = new ManiaHitWindows(5.0);
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

                    // Si la LN fue completamente evaluada, aplicar los resultados
                    if (drawableLN.HitObject is LongNote ln && ln.IsFullyJudged)
                    {
                        if (ln.HeadJudged && !drawableLN.IsJudged)
                        {
                            scoreProcessor.ApplyResult(ln.HeadResult);
                            _explosions.Add(new HitExplosion(ln.HeadResult, currentTime));
                        }

                        if (ln.TailJudged)
                        {
                            scoreProcessor.ApplyResult(ln.TailResult);
                            _explosions.Add(new HitExplosion(ln.TailResult, currentTime));

                            drawableLN.IsJudged = true;
                            _drawableHitObjects.Remove(drawable);
                            drawable.Dispose();
                        }
                    }
                }
                else if (drawable.HasResult) // Evaluar auto-miss para notas normales
                {
                    OnHitObjectJudged(drawable, currentTime, scoreProcessor);
                    _drawableHitObjects.Remove(drawable);
                    drawable.Dispose();
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

                    if (drawableLN.HitObject is LongNote ln && ln.HeadJudged)
                    {
                        // Activar iluminación de la columna al acertar la cabeza de la LN
                        _lightingEndTime = currentTime + 150;

                        // Enviar el resultado al gestor visual central
                        int headOffset = (int)Math.Truncate(ln.HeadHitTime - ln.StartTime);
                        _judgementDisplayManager?.EnqueueJudgement(ln.HeadResult, _index, headOffset, scoreProcessor.Combo, currentTime);
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

                    if (drawableLN.HitObject is LongNote ln && ln.TailJudged)
                    {
                        int tailOffset = (int)Math.Truncate(ln.TailReleaseTime - ln.EndTime);
                        _judgementDisplayManager?.EnqueueJudgement(ln.TailResult, _index, tailOffset, scoreProcessor.Combo, currentTime);
                    }
                }
            }
        }

        private void OnHitObjectJudged(DrawableManiaHitObject drawable, double currentTime, ManiaScoreProcessor scoreProcessor)
        {
            if (!drawable.Result.HasValue) return;

            var result = drawable.Result.Value;

            scoreProcessor.ApplyResult(result);
            _explosions.Add(new HitExplosion(result, currentTime));

            Console.WriteLine($"[Columna {_index}] Juicio: {result} a los {currentTime:F2}ms");
        }

        public void Draw(SpriteBatch spriteBatch, Texture2D pixel, TrueTypeFontRenderer? fontRenderer,
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

            // Renderizar el receptor (la línea de juicio inferior o superior según el modo)
            var receptorRect = new Rectangle(_x + 3, receptorY - 2, _width - 6, 4);
            Color receptorColor = _isPressed ? Color.Yellow : Color.White;
            spriteBatch.Draw(pixel, receptorRect, receptorColor);

            // Renderizar iluminación de tecla presionada
            if (_isPressed || currentTime < _lightingEndTime)
            {
                float alpha = _isPressed ? 0.4f : 0.3f;
                var lightRect = new Rectangle(_x + 2, receptorY - 40, _width - 4, 80);
                spriteBatch.Draw(pixel, lightRect, Color.White * alpha);
            }

            // Renderizar explosiones de acierto
            foreach (var explosion in _explosions.ToArray())
            {
                explosion.Draw(spriteBatch, pixel, _x, _width, receptorY, currentTime);
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