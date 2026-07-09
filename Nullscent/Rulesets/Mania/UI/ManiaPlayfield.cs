#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Nullscent.Rulesets.Mania.Judgements;
using Nullscent.Rulesets.Mania.Objects;
using Nullscent.Rulesets.Mania.Scoring;
using Nullscent.Rulesets.Mania.UI.Drawables;
using Nullscent.Beatmap;
using Nullscent.UI;
using Nullscent.Audio;
using Nullscent.Rulesets.Mania.Mods;
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
        private readonly bool _isHiddenActive;
        private readonly bool _isFlashlightActive;
        private int _currentCombo;

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
            double overallDifficulty = 5.0,
            HitSoundPlayer? hitSoundPlayer = null,
            bool isScoreV2 = false,
            List<ManiaMod>? activeMods = null)
        {
            _graphicsDevice = graphicsDevice;
            _spriteBatch = spriteBatch;
            _columnCount = columnCount;
            _screenHeight = screenHeight;
            _fontRenderer = fontRenderer;
            _overallDifficulty = overallDifficulty;

            _isHiddenActive = activeMods != null && activeMods.Any(m => m is ManiaModHidden);
            _isFlashlightActive = activeMods != null && activeMods.Any(m => m is ManiaModFlashlight);

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
                var col = new Column(i, x, columnWidth, screenHeight, _whitePixel, overallDifficulty, hitSoundPlayer, isScoreV2, columnCount);
                col.IsHiddenActive = _isHiddenActive;
                _columns.Add(col);
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
                    Samples = holdNote.Samples,
                    HitSound = holdNote.HitSound,
                    SampleFileName = holdNote.SampleFileName
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
            _currentCombo = scoreProcessor.Combo;
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

            // Dibujar el desvanecimiento de Flashlight si está activo
            if (_isFlashlightActive)
            {
                DrawFlashlightMask(downScroll);
            }

            // Dibujar el gestor de juicios por encima de la pista
            _judgementDisplayManager?.Draw(currentTime);

            _spriteBatch.End();
        }

        private void DrawFlashlightMask(bool downScroll)
        {
            if (_whitePixel == null || _linearGradient == null) return;

            // Calcular el radio visible escalando con el combo (más pequeño a mayor combo, de 240px a 140px)
            double comboFactor = Math.Min(1.0, _currentCombo / 200.0);
            int visibleRadius = (int)(240 - comboFactor * 100);
            int gradientHeight = 150;

            if (downScroll)
            {
                // Rectángulo superior completamente negro
                int topBlackHeight = Math.Max(0, ReceptorY - visibleRadius);
                if (topBlackHeight > 0)
                {
                    var topRect = new Rectangle(_stageX, 0, _stageWidth, topBlackHeight);
                    _spriteBatch.Draw(_whitePixel, topRect, Color.Black);
                }

                // Degradado superior (de negro a transparente)
                int topGradientY = topBlackHeight;
                if (gradientHeight > 0)
                {
                    var gradRect = new Rectangle(_stageX, topGradientY, _stageWidth, gradientHeight);
                    _spriteBatch.Draw(_linearGradient, gradRect, Color.Black);
                }

                // Rectángulo inferior completamente negro (debajo del receptor)
                int bottomBlackY = ReceptorY + 60;
                int bottomBlackHeight = _screenHeight - bottomBlackY;
                if (bottomBlackHeight > 0)
                {
                    var bottomRect = new Rectangle(_stageX, bottomBlackY, _stageWidth, bottomBlackHeight);
                    _spriteBatch.Draw(_whitePixel, bottomRect, Color.Black);
                }
            }
            else
            {
                // upScroll: las notas vienen desde abajo hacia ReceptorY (arriba).
                
                // Rectángulo inferior completamente negro
                int bottomBlackY = ReceptorY + visibleRadius;
                int bottomBlackHeight = _screenHeight - bottomBlackY;
                if (bottomBlackHeight > 0)
                {
                    var bottomRect = new Rectangle(_stageX, bottomBlackY, _stageWidth, bottomBlackHeight);
                    _spriteBatch.Draw(_whitePixel, bottomRect, Color.Black);
                }

                // Degradado inferior (de negro a transparente al subir)
                int bottomGradientY = bottomBlackY - gradientHeight;
                if (gradientHeight > 0)
                {
                    var gradRect = new Rectangle(_stageX, bottomGradientY, _stageWidth, gradientHeight);
                    _spriteBatch.Draw(_linearGradient, gradRect, null, Color.Black, 0f, Vector2.Zero, SpriteEffects.FlipVertically, 0f);
                }

                // Rectángulo superior completamente negro (encima del receptor)
                int topBlackHeight = ReceptorY - 60;
                if (topBlackHeight > 0)
                {
                    var topRect = new Rectangle(_stageX, 0, _stageWidth, topBlackHeight);
                    _spriteBatch.Draw(_whitePixel, topRect, Color.Black);
                }
            }
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
        private readonly HitSoundPlayer? _hitSoundPlayer;

        public int X { get => _x; set => _x = value; }
        public int ScreenHeight { get => _screenHeight; set => _screenHeight = value; }
        public bool IsHiddenActive { get; set; }

        public Column(int index, int x, int width, int screenHeight, Texture2D pixel, double overallDifficulty, HitSoundPlayer? hitSoundPlayer = null, bool isScoreV2 = false, int totalColumnCount = 4)
        {
            _index = index;
            _x = x;
            _width = width;
            _screenHeight = screenHeight;
            _pixel = pixel;
            _hitWindows = isScoreV2
                ? new ScoreV2HitWindows(overallDifficulty)
                : new ManiaHitWindows(overallDifficulty);
            _totalColumnCount = totalColumnCount;
            _hitSoundPlayer = hitSoundPlayer;
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
                drawable.IsHiddenActive = this.IsHiddenActive;
                _drawableHitObjects.Add(drawable);
            }
        }

        public void Update(double currentTime, ManiaScoreProcessor scoreProcessor, double scrollSpeed, float receptorPosition, bool downScroll, int receptorY)
        {
            double scrollTime = 2000.0 - (scrollSpeed - 1.0) * 45.0;
            scrollTime = Math.Clamp(scrollTime, 150.0, 2000.0);
            double limitTime = currentTime + scrollTime + 1000;

            for (int i = 0; i < _drawableHitObjects.Count; i++)
            {
                var drawable = _drawableHitObjects[i];
                if (drawable.HitObject.StartTime > limitTime)
                {
                    break;
                }

                drawable.CurrentCombo = scoreProcessor.Combo;
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
                        if (ln.TailJudged && !drawableLN.IsTailApplied)
                        {
                            scoreProcessor.ApplyResult(ln.TailResult);
                            
                            if (ln.TailResult != HitResult.Miss)
                            {
                                _explosions.Add(new HitExplosion(ln.TailResult, currentTime));
                            }

                            int tailOffset = (int)Math.Truncate(ln.TailReleaseTime - ln.EndTime);
                            _judgementDisplayManager?.EnqueueJudgement(ln.TailResult, _index, tailOffset, scoreProcessor.Combo, currentTime);

                            drawableLN.IsTailApplied = true;
                        }

                        // Remove and dispose only when drawableLN.IsJudged is true (after scrolling past)
                        if (drawableLN.IsJudged)
                        {
                            _drawableHitObjects.RemoveAt(i);
                            drawable.Dispose();
                            i--;
                            continue;
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
                        _drawableHitObjects.RemoveAt(i);
                        drawable.Dispose();
                        i--;
                        continue;
                    }
                }
            }

            // Actualizar ciclo de vida de explosiones visuales sin usar .ToList() para evitar recolector de basura (GC)
            for (int i = 0; i < _explosions.Count; i++)
            {
                var explosion = _explosions[i];
                explosion.Update(currentTime);
                if (!explosion.IsActive)
                {
                    _explosions.RemoveAt(i);
                    i--;
                }
            }
        }

        private void PlayNoteHitsound(ManiaHitObject hitObject)
        {
            if (_hitSoundPlayer == null) return;

            if (hitObject.Samples != null && hitObject.Samples.Length > 0)
            {
                foreach (var sample in hitObject.Samples)
                {
                    string fileName = $"{sample.Bank}-{sample.Name}";
                    _hitSoundPlayer.Play(fileName);
                }
            }
            else
            {
                _hitSoundPlayer.PlayHitsound(hitObject.HitSound, hitObject.SampleFileName);
            }
        }

        public void OnKeyPress(double currentTime, ManiaScoreProcessor scoreProcessor)
        {
            _isPressed = true;
            bool hitSuccess = false;

            // Encontrar la primera nota pendiente (o candidata válida para LN)
            DrawableManiaHitObject? candidate = null;
            foreach (var drawable in _drawableHitObjects)
            {
                if (drawable.HitObject.StartTime > currentTime + 500)
                {
                    break;
                }

                if (drawable.HasResult) continue;

                if (drawable is DrawableLongNote drawableLN)
                {
                    if (drawableLN.HitObject is LongNote ln && !ln.HeadJudged)
                    {
                        candidate = drawable;
                        break;
                    }
                }
                else
                {
                    candidate = drawable;
                    break;
                }
            }

            // Si hay una nota candidata, intentar juzgarla
            if (candidate != null)
            {
                if (candidate is DrawableLongNote drawableLN)
                {
                    if (drawableLN.HitObject is LongNote ln)
                    {
                        double offset = currentTime - ln.StartTime;
                        var result = _hitWindows.JudgeHit(offset);

                        // Si aún no ha entrado en la ventana de juicio, la pulsación no hace nada (se ignora)
                        if (result == HitResult.None)
                        {
                            _hitSoundPlayer?.Play("normal-hitnormal");
                            return;
                        }

                        // Procesar la pulsación de la cabeza de la LN
                        drawableLN.CheckForResult(currentTime, _hitWindows);

                        if (ln.HeadJudged && ln.HeadResult != HitResult.Miss)
                        {
                            _lightingEndTime = currentTime + 150;
                            PlayNoteHitsound(ln);
                            hitSuccess = true;
                        }
                    }
                }
                else
                {
                    double offset = currentTime - candidate.HitObject.StartTime;
                    var result = _hitWindows.JudgeHit(offset);

                    // Si aún no ha entrado en la ventana de juicio, la pulsación no hace nada (se ignora)
                    if (result == HitResult.None)
                    {
                        _hitSoundPlayer?.Play("normal-hitnormal");
                        return;
                    }

                    // Consumir el juicio de la nota normal
                    candidate.OnJudged(result);

                    if (candidate.HasResult)
                    {
                        if (candidate.Result.HasValue)
                        {
                            _judgementDisplayManager?.EnqueueJudgement(candidate.Result.Value, _index, (int)Math.Truncate(currentTime - candidate.HitObject.StartTime), scoreProcessor.Combo, currentTime);
                        }

                        OnHitObjectJudged(candidate, currentTime, scoreProcessor);

                        if (candidate.Result != HitResult.Miss)
                        {
                            PlayNoteHitsound(candidate.HitObject);
                            hitSuccess = true;
                        }

                        _drawableHitObjects.Remove(candidate);
                        _lightingEndTime = currentTime + 150;
                    }
                }
            }

            // Si no se golpeó ninguna nota con éxito, reproducir hitsound de feedback por defecto
            if (!hitSuccess)
            {
                _hitSoundPlayer?.Play("normal-hitnormal");
            }
        }

        public void OnKeyRelease(double currentTime, ManiaScoreProcessor scoreProcessor)
        {
            _isPressed = false;

            // Evaluar los resultados de soltar el teclado para las colas de las LN
            for (int i = 0; i < _drawableHitObjects.Count; i++)
            {
                var drawable = _drawableHitObjects[i];
                if (drawable.HitObject.StartTime > currentTime + 500)
                {
                    break;
                }

                if (drawable is DrawableLongNote drawableLN)
                {
                    if (drawableLN.HitObject is LongNote ln && !ln.TailJudged)
                    {
                        drawableLN.OnKeyRelease(currentTime, _hitWindows);
                        if (ln.TailJudged && ln.TailResult != HitResult.Miss)
                        {
                            PlayNoteHitsound(ln);
                        }
                    }
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

            double scrollTime = 2000.0 - (scrollSpeed - 1.0) * 45.0;
            scrollTime = Math.Clamp(scrollTime, 150.0, 2000.0);
            double limitTime = currentTime + scrollTime + 1000;

            foreach (var drawable in _drawableHitObjects)
            {
                if (drawable.HitObject.StartTime > limitTime)
                {
                    break;
                }
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