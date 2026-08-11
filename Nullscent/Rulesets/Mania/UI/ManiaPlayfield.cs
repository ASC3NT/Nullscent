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
using Nullscent.Skin;
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
        public System.Collections.Generic.IReadOnlyList<Column> Columns => _columns;
        private readonly GraphicsDevice _graphicsDevice;
        private readonly SpriteBatch _spriteBatch;
        private readonly List<Column> _columns;
        private readonly int _columnCount;
        private double _overallDifficulty = 5.0;
        private readonly bool _isHiddenActive;
        private readonly bool _isFlashlightActive;
        private int _currentCombo;
        private readonly SkinManager? _skinManager;

        // Propiedades visuales de la pista (stage)
        private int _stageX;
        private int _stageWidth;
        private int _screenHeight;
        private int _screenWidth;
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
            List<ManiaMod>? activeMods = null,
            SkinManager? skinManager = null)
        {
            _graphicsDevice = graphicsDevice;
            _spriteBatch = spriteBatch;
            _columnCount = columnCount;
            _screenHeight = screenHeight;
            _screenWidth = screenWidth;
            _fontRenderer = fontRenderer;
            _overallDifficulty = overallDifficulty;
            _skinManager = skinManager;

            _isHiddenActive = activeMods != null && activeMods.Any(m => m is ManiaModHidden);
            _isFlashlightActive = activeMods != null && activeMods.Any(m => m is ManiaModFlashlight);

            // Inicializar textura base para primitivas y texturas generadas
            _whitePixel = new Texture2D(graphicsDevice, 1, 1);
            _whitePixel.SetData(new[] { Color.White });
            _radialGlow = CreateRadialGlowTexture(graphicsDevice, 64);
            _linearGradient = CreateLinearGradientTexture(graphicsDevice, 32, 256);

            // Inicializar las columnas temporales (serán reposicionadas en CalculateLayout)
            _columns = new List<Column>();
            for (int i = 0; i < columnCount; i++)
            {
                var col = new Column(i, 0, 80, screenHeight, _whitePixel, overallDifficulty, hitSoundPlayer, isScoreV2, columnCount, skinManager);
                col.IsHiddenActive = _isHiddenActive;
                _columns.Add(col);
            }

            // Crear el gestor central de juicios
            _judgementDisplayManager = new JudgementDisplayManager(_graphicsDevice, _spriteBatch, _fontRenderer);

            // Calcular layout con anchos y espaciados de la skin
            CalculateLayout(screenWidth, screenHeight);

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

        private void CalculateLayout(int screenWidth, int screenHeight)
        {
            _screenHeight = screenHeight;

            float[]? customWidths = null;
            float? customStart = null;
            float[]? customSpacing = null;

            if (_skinManager != null && _skinManager.CurrentSkinConfig != null)
            {
                var config = _skinManager.CurrentSkinConfig.GetConfigForKeyCount(_columnCount);
                customWidths = config.ColumnWidths;
                customStart = config.ColumnStart;
                customSpacing = config.ColumnSpacing;
            }

            // La escala base de osu!mania se calcula siempre en base al alto de la pantalla (480p virtual)
            float scale = (float)screenHeight / 480f;

            // Determinar anchos
            float[] colWidths = new float[_columnCount];
            float totalWidth = 0;
            for (int i = 0; i < _columnCount; i++)
            {
                // Si la skin no provee anchos, usamos el fallback de osu! (30) para mantener proporción, o 80px físicos si no hay skin activa
                float w = (customWidths != null) ? 30f : 80f / scale;
                if (customWidths != null && customWidths.Length > i)
                {
                    w = customWidths[i];
                }
                else if (customWidths != null && customWidths.Length > 0)
                {
                    w = customWidths[customWidths.Length - 1];
                }
                w = Math.Max(5f, w);
                colWidths[i] = w * scale;
                totalWidth += colWidths[i];
            }

            // Determinar espaciados
            float[] colSpacings = new float[Math.Max(0, _columnCount - 1)];
            float totalSpacing = 0;
            for (int i = 0; i < colSpacings.Length; i++)
            {
                float s = 0f;
                if (customSpacing != null && customSpacing.Length > i)
                {
                    s = customSpacing[i];
                }
                s = Math.Max(0f, s);
                colSpacings[i] = s * scale;
                totalSpacing += colSpacings[i];
            }

            _stageWidth = (int)(totalWidth + totalSpacing);

            // Centrado de la caja virtual de 640x480 en pantallas widescreen
            float playfieldOffset = (screenWidth - 640f * scale) / 2f;

            if (customStart.HasValue)
            {
                _stageX = (int)(playfieldOffset + customStart.Value * scale);
            }
            else
            {
                _stageX = (int)((screenWidth - _stageWidth) / 2f);
            }

            float currentX = _stageX;
            for (int i = 0; i < _columnCount; i++)
            {
                int w = (int)colWidths[i];
                if (i < _columns.Count)
                {
                    _columns[i].X = (int)currentX;
                    _columns[i].Width = w;
                    _columns[i].ScreenHeight = screenHeight;
                }
                currentX += w;
                if (i < _columnCount - 1 && i < colSpacings.Length)
                {
                    currentX += colSpacings[i];
                }
            }

            if (_judgementDisplayManager != null)
            {
                float comboY = screenHeight * 0.45f; // Fallback por defecto (45% de la pantalla)
                if (_skinManager != null && _skinManager.CurrentSkinConfig != null)
                {
                    var config = _skinManager.CurrentSkinConfig.GetConfigForKeyCount(_columnCount);
                    if (config.ComboPosition.HasValue)
                    {
                        // Escalar el combo Y según la resolución (osu!mania utiliza alto de 480 virtual)
                        comboY = config.ComboPosition.Value * scale;
                    }
                }
                _judgementDisplayManager.Position = new Vector2(_stageX + _stageWidth / 2f, comboY);
            }
        }

        public void Update(double currentTime, ManiaScoreProcessor scoreProcessor, double scrollSpeed, float receptorPosition, bool downScroll)
        {
            _currentCombo = scoreProcessor.Combo;
            int currentWidth = _graphicsDevice.Viewport.Width;
            int currentHeight = _graphicsDevice.Viewport.Height;

            // Check if window was resized and update layout
            if (_screenHeight != currentHeight || _screenWidth != currentWidth)
            {
                _screenWidth = currentWidth;
                CalculateLayout(currentWidth, currentHeight);
            }

            // Actualizar la posición de los receptores en el eje Y (con soporte de HitPosition absoluto del skin)
            int hitPos = _skinManager?.GetHitPosition(_columnCount) ?? -1;
            if (hitPos > 0)
            {
                float scale = (float)_screenHeight / 480f;
                if (downScroll)
                {
                    ReceptorY = (int)(hitPos * scale);
                }
                else
                {
                    ReceptorY = (int)((480 - hitPos) * scale);
                }
            }
            else
            {
                ReceptorY = (int)(receptorPosition * _screenHeight);
            }

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

            DrawStageBackground(downScroll);

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
            _judgementDisplayManager?.Draw(currentTime, _skinManager);

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

        private void DrawStageBackground(bool downScroll)
        {
            if (_whitePixel == null || _spriteBatch == null) return;

            // Fondo oscuro para la pista
            var stageRect = new Rectangle(_stageX, 0, _stageWidth, _screenHeight);
            _spriteBatch.Draw(_whitePixel, stageRect, new Color(15, 15, 20, 220));

            // Dibujar StageBottom si está disponible en la skin (sin estirar horizontalmente, centrado)
            Texture2D? stageBottom = _skinManager?.GetStageTexture(_columnCount, "bottom");
            if (stageBottom != null)
            {
                float texScale = _skinManager != null ? _skinManager.GetTextureScale(stageBottom) : 1.0f;
                int bottomW = (int)(stageBottom.Width * texScale);
                int bottomH = (int)(stageBottom.Height * texScale);
                
                int bottomX = _stageX + (_stageWidth - bottomW) / 2;
                int bottomY = downScroll ? _screenHeight - bottomH : 0;
                var bottomRect = new Rectangle(bottomX, bottomY, bottomW, bottomH);
                SpriteEffects effects = downScroll ? SpriteEffects.None : SpriteEffects.FlipVertically;
                
                _spriteBatch.Draw(stageBottom, bottomRect, null, Color.White, 0f, Vector2.Zero, effects, 0f);
            }

            // Dibujar StageHint (línea de impacto visual)
            Texture2D? stageHint = _skinManager?.GetStageTexture(_columnCount, "hint");
            if (stageHint != null)
            {
                float aspect = (float)stageHint.Height / stageHint.Width;
                int hintH = (int)(_stageWidth * aspect);
                var hintRect = new Rectangle(_stageX, ReceptorY - hintH / 2, _stageWidth, hintH);
                _spriteBatch.Draw(stageHint, hintRect, Color.White);
            }

            // Dibujar Judgement Line si está activa (blanca por defecto o tintada según skin.ini)
            bool drawJudgementLine = _skinManager?.CurrentSkinConfig?.GetConfigForKeyCount(_columnCount).JudgementLine ?? true;
            if (drawJudgementLine && stageHint == null)
            {
                var lineRect = new Rectangle(_stageX, ReceptorY - 1, _stageWidth, 2);
                Color lineColor = Color.White;
                
                var customJudgementColor = _skinManager?.GetColourJudgementLine(_columnCount);
                if (customJudgementColor.HasValue)
                {
                    lineColor = new Color(customJudgementColor.Value.R, customJudgementColor.Value.G, customJudgementColor.Value.B, customJudgementColor.Value.A);
                }
                
                _spriteBatch.Draw(_whitePixel, lineRect, lineColor * 0.8f);
            }

            // Dibujar separadores de columnas y bordes laterales sin estiramiento vertical (Vertical Tiling)
            Texture2D? stageLeft = _skinManager?.GetStageTexture(_columnCount, "left");
            Texture2D? stageRight = _skinManager?.GetStageTexture(_columnCount, "right");
            float scale = (float)_screenHeight / 480f;

            if (stageLeft != null)
            {
                float texScale = _skinManager != null ? _skinManager.GetTextureScale(stageLeft) : 1.0f;
                int borderW = (int)(stageLeft.Width * texScale * scale);
                int texHeight = (int)(stageLeft.Height * texScale * scale);
                
                if (texHeight > 0)
                {
                    for (int drawY = 0; drawY < _screenHeight; drawY += texHeight)
                    {
                        int drawH = Math.Min(texHeight, _screenHeight - drawY);
                        var destRect = new Rectangle(_stageX - borderW, drawY, borderW, drawH);
                        int srcH = (int)(drawH / (texScale * scale));
                        var srcRect = new Rectangle(0, 0, stageLeft.Width, srcH);
                        _spriteBatch.Draw(stageLeft, destRect, srcRect, Color.White);
                    }
                }
                else
                {
                    _spriteBatch.Draw(stageLeft, new Rectangle(_stageX - borderW, 0, borderW, _screenHeight), Color.White);
                }
            }
            if (stageRight != null)
            {
                float texScale = _skinManager != null ? _skinManager.GetTextureScale(stageRight) : 1.0f;
                int borderW = (int)(stageRight.Width * texScale * scale);
                int texHeight = (int)(stageRight.Height * texScale * scale);
                
                if (texHeight > 0)
                {
                    for (int drawY = 0; drawY < _screenHeight; drawY += texHeight)
                    {
                        int drawH = Math.Min(texHeight, _screenHeight - drawY);
                        var destRect = new Rectangle(_stageX + _stageWidth, drawY, borderW, drawH);
                        int srcH = (int)(drawH / (texScale * scale));
                        var srcRect = new Rectangle(0, 0, stageRight.Width, srcH);
                        _spriteBatch.Draw(stageRight, destRect, srcRect, Color.White);
                    }
                }
                else
                {
                    _spriteBatch.Draw(stageRight, new Rectangle(_stageX + _stageWidth, 0, borderW, _screenHeight), Color.White);
                }
            }

            // Dibujar lineas de columna (Column Lines)
            var columnLines = _skinManager?.GetColumnLines(_columnCount);
            if (columnLines != null)
            {
                for (int i = 0; i < columnLines.Length && i < _columns.Count - 1; i++)
                {
                    if (columnLines[i])
                    {
                        int lineX = _columns[i].X + _columns[i].Width;
                        var lineRect = new Rectangle(lineX, 0, 2, _screenHeight);
                        Color lineColor = new Color(150, 150, 150, 150);
                        
                        var customLineColor = _skinManager?.GetColourColumnLine(_columnCount);
                        if (customLineColor.HasValue)
                        {
                            lineColor = new Color(customLineColor.Value.R, customLineColor.Value.G, customLineColor.Value.B, customLineColor.Value.A);
                        }
                        else
                        {
                            var barlineColor = _skinManager?.GetColourBarline(_columnCount);
                            if (barlineColor.HasValue)
                            {
                                lineColor = new Color(barlineColor.Value.R, barlineColor.Value.G, barlineColor.Value.B, barlineColor.Value.A);
                            }
                        }
                        
                        _spriteBatch.Draw(_whitePixel, lineRect, lineColor);
                    }
                }
            }
            else if (stageLeft == null && stageRight == null)
            {
                // Dibujar separadores predeterminados si no hay texturas de borde ni configuración específica
                for (int i = 1; i < _columnCount; i++)
                {
                    int separatorX = _columns[i].X;
                    var separatorRect = new Rectangle(separatorX, 0, 2, _screenHeight);
                    _spriteBatch.Draw(_whitePixel, separatorRect, new Color(40, 40, 50, 255));
                }
            }

            if (stageLeft == null && stageRight == null)
            {
                // Dibujar bordes laterales de pista predeterminados (Stage Sidebars) - estilo neón brillante (rosa)
                var leftBorderRect = new Rectangle(_stageX - 4, 0, 4, _screenHeight);
                var rightBorderRect = new Rectangle(_stageX + _stageWidth, 0, 4, _screenHeight);
                Color neonColor = new Color(255, 102, 171); // Magenta de osu!

                _spriteBatch.Draw(_whitePixel, leftBorderRect, neonColor);
                _spriteBatch.Draw(_whitePixel, rightBorderRect, neonColor);
            }
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
        private int _width;
        private int _screenHeight;
        private readonly Texture2D _pixel;

        // Contenedores de elementos visuales (drawables)
        private readonly List<DrawableManiaHitObject> _drawableHitObjects = new();
        private readonly List<HitExplosion> _explosions = new();
        private readonly List<HitExplosion> _explosionPool = new();
        private JudgementDisplayManager? _judgementDisplayManager;

        private readonly ManiaHitWindows _hitWindows;

        private bool _isPressed;
        private bool _isHolding;
        private double _lightingEndTime;
        private int _totalColumnCount;
        private readonly HitSoundPlayer? _hitSoundPlayer;

        public int X { get => _x; set => _x = value; }
        public int Width { get => _width; set => _width = value; }
        public int ScreenHeight { get => _screenHeight; set => _screenHeight = value; }
        public bool IsHiddenActive { get; set; }
        public bool IsPressed => _isPressed;

        private void AddExplosion(HitResult result, double currentTime)
        {
            bool showHitLighting = Core.SettingsService.Instance.ShowHitLighting;
            bool isLightingEnabled = _skinManager?.IsLightingEnabled(_totalColumnCount) ?? true;
            if (!showHitLighting || !isLightingEnabled)
                return;

            HitExplosion? pooled = null;
            for (int i = 0; i < _explosionPool.Count; i++)
            {
                if (!_explosionPool[i].IsActive)
                {
                    pooled = _explosionPool[i];
                    break;
                }
            }

            if (pooled == null)
            {
                pooled = new HitExplosion(result, currentTime);
                _explosionPool.Add(pooled);
            }
            else
            {
                pooled.Reset(result, currentTime);
            }

            _explosions.Add(pooled);
        }

        private readonly SkinManager? _skinManager;

        public Column(int index, int x, int width, int screenHeight, Texture2D pixel, double overallDifficulty, HitSoundPlayer? hitSoundPlayer = null, bool isScoreV2 = false, int totalColumnCount = 4, SkinManager? skinManager = null)
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
            _skinManager = skinManager;
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
            _isHolding = false;

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
                        if (ln.State == LongNoteState.Holding)
                        {
                            _isHolding = true;
                        }
                        // Check if head was judged but not yet applied
                        if (ln.HeadJudged && !drawableLN.IsHeadApplied)
                        {
                            scoreProcessor.ApplyResult(ln.HeadResult);
                            
                            // Only add explosion if it's not a Miss
                            if (ln.HeadResult != HitResult.Miss)
                            {
                                AddExplosion(ln.HeadResult, currentTime);
                            }
                            
                            int headOffset = (int)Math.Truncate(ln.HeadHitTime - ln.StartTime);
                            _judgementDisplayManager?.EnqueueJudgement(ln.HeadResult, _index, headOffset, scoreProcessor.Combo, currentTime);
                            
                            drawableLN.IsHeadApplied = true;
                        }

                        // Check if tail was judged but not yet applied
                        if (ln.TailJudged && !drawableLN.IsTailApplied)
                        {
                            scoreProcessor.ApplyResult(ln.TailResult);
                            
                            // Only add explosion if it's not a Miss
                            if (ln.TailResult != HitResult.Miss)
                            {
                                AddExplosion(ln.TailResult, currentTime);
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
                AddExplosion(result, currentTime);
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
            
            // Si la skin define un color específico para esta columna, lo usamos
            var customColColor = _skinManager?.GetColumnColorMap(_totalColumnCount, _index);
            if (customColColor.HasValue)
            {
                columnBg = new Color(customColColor.Value.R, customColColor.Value.G, customColColor.Value.B, (byte)(customColColor.Value.A * 0.7f));
            }
            
            var columnRect = new Rectangle(_x, 0, _width, _screenHeight);
            spriteBatch.Draw(pixel, columnRect, columnBg);

            // DIBUJAR STAGELIGHT (ILUMINACIÓN DE CARRIL DEBAJO DE LAS NOTAS) AL PULSAR LA TECLA
            if (_isPressed)
            {
                Texture2D? stageLight = _skinManager?.GetStageTexture(_totalColumnCount, "light");
                if (stageLight != null)
                {
                    Color lightTint = Color.White;
                    var customLightColor = _skinManager?.GetColourLight(_totalColumnCount, _index);
                    if (customLightColor.HasValue)
                    {
                        lightTint = new Color(customLightColor.Value.R, customLightColor.Value.G, customLightColor.Value.B, customLightColor.Value.A);
                    }
                    
                    int lightPos = _skinManager?.GetLightPosition(_totalColumnCount) ?? (receptorY + 11);
                    float aspect = (float)stageLight.Height / stageLight.Width;
                    int lightW = _width;
                    int lightH = (int)(_width * aspect);
                    
                    int lightY = downScroll ? lightPos - lightH : lightPos;
                    var lightRect = new Rectangle(_x, lightY, lightW, lightH);
                    
                    SpriteEffects effects = downScroll ? SpriteEffects.FlipVertically : SpriteEffects.None;
                    spriteBatch.Draw(stageLight, lightRect, null, lightTint, 0f, Vector2.Zero, effects, 0f);
                }
            }

            bool keysUnderNotes = _skinManager?.AreKeysUnderNotes(_totalColumnCount) ?? false;

            // Local function to draw Notes
            void DrawNotes()
            {
                double scrollTime = 2000.0 - (scrollSpeed - 1.0) * 45.0;
                scrollTime = Math.Clamp(scrollTime, 150.0, 2000.0);
                double limitTime = currentTime + scrollTime + 1000;
                int maxIndex = 0;
                for (int i = 0; i < _drawableHitObjects.Count; i++)
                {
                    if (_drawableHitObjects[i].HitObject.StartTime > limitTime)
                    {
                        maxIndex = i;
                        break;
                    }
                    maxIndex = i + 1;
                }

                for (int i = maxIndex - 1; i >= 0; i--)
                {
                    var drawable = _drawableHitObjects[i];
                    if (drawable.IsJudged)
                    {
                        continue;
                    }
                    drawable.Draw(spriteBatch, pixel, _skinManager, _totalColumnCount, _index, _x, currentTime, scrollSpeed, receptorY, downScroll, _screenHeight, _width);
                }
            }

            // Local function to draw Receptor
            void DrawReceptor()
            {
                var keyAnim = _skinManager?.GetKeyAnimation(_totalColumnCount, _index, _isPressed);
                Texture2D? keyTexture = _skinManager?.GetAnimationFrame(keyAnim, currentTime);
                if (keyTexture != null)
                {
                    float userScale = (float)Core.SettingsService.Instance.SkinReceptorHeightScale;
                    float scale = (float)_screenHeight / 480f;
                    int keyHeight = (int)(keyTexture.Height * scale * userScale);

                    int keyY = downScroll ? receptorY - keyHeight : receptorY;
                    var keyRect = new Rectangle(_x, keyY, _width, keyHeight);
                    SpriteEffects effects = downScroll ? SpriteEffects.None : SpriteEffects.FlipVertically;
                    spriteBatch.Draw(keyTexture, keyRect, null, Color.White, 0f, Vector2.Zero, effects, 0f);
                }
                else
                {
                    int keyHeight = 20;
                    int keyY = downScroll ? receptorY - keyHeight : receptorY;
                    var keyRect = new Rectangle(_x + 3, keyY, _width - 6, keyHeight);
                    Color keyBgColor = _isPressed ? GetLaneColor(_index) * 0.4f : Color.Black * 0.5f;
                    spriteBatch.Draw(pixel, keyRect, keyBgColor);
                    
                    Color keyBorderColor = _isPressed ? GetLaneColor(_index) : Color.White * 0.7f;
                    spriteBatch.Draw(pixel, new Rectangle(keyRect.X, keyRect.Y, keyRect.Width, 2), keyBorderColor);
                    spriteBatch.Draw(pixel, new Rectangle(keyRect.X, keyRect.Y + keyRect.Height - 2, keyRect.Width, 2), keyBorderColor);
                    spriteBatch.Draw(pixel, new Rectangle(keyRect.X, keyRect.Y, 2, keyRect.Height), keyBorderColor);
                    spriteBatch.Draw(pixel, new Rectangle(keyRect.X + keyRect.Width - 2, keyRect.Y, 2, keyRect.Height), keyBorderColor);
                }
            }

            // Local function to draw Lighting
            void DrawLighting()
            {
                var lightingAnim = _isHolding
                    ? _skinManager?.GetLightingLAnimation(_totalColumnCount, _index)
                    : _skinManager?.GetLightingAnimation(_totalColumnCount, _index);
                Texture2D? lightingTexture = _skinManager?.GetAnimationFrame(lightingAnim, currentTime);
                bool isLightingEnabled = _skinManager?.IsLightingEnabled(_totalColumnCount) ?? true;
                if (lightingTexture != null && isLightingEnabled)
                {
                    if (_isPressed || currentTime < _lightingEndTime)
                    {
                        float age = (float)(_lightingEndTime - currentTime);
                        float alpha = _isPressed ? 0.4f : Math.Clamp(age / 150f * 0.4f, 0.0f, 0.4f);
                        
                        float texScale = _skinManager != null ? _skinManager.GetTextureScale(lightingTexture) : 1.0f;
                        float aspect = (float)lightingTexture.Height / lightingTexture.Width;
                        
                        int lightX = _x;
                        int lightW = _width;
                        
                        float[]? lightingWidths = _isHolding
                            ? _skinManager?.GetLightingLWidths(_totalColumnCount)
                            : _skinManager?.GetLightingNWidths(_totalColumnCount);
                            
                        if (lightingWidths != null && _index < lightingWidths.Length && lightingWidths[_index] > 0)
                        {
                            lightW = (int)lightingWidths[_index];
                            lightX = _x + (_width - lightW) / 2;
                        }
                        
                        int lightH = (int)(lightW * aspect * texScale);
                        if (lightH <= 0) lightH = 220;
                        
                        int lightY = downScroll ? receptorY - lightH : receptorY;
                        Rectangle lightRect = new Rectangle(lightX, lightY, lightW, lightH);
                        
                        SpriteEffects effects = downScroll ? SpriteEffects.FlipVertically : SpriteEffects.None;

                        // Apply custom light color if defined in the skin
                        Color lightTint = Color.White;
                        var customLightColor = _skinManager?.GetColourLight(_totalColumnCount, _index);
                        if (customLightColor.HasValue)
                        {
                            lightTint = new Color(customLightColor.Value.R, customLightColor.Value.G, customLightColor.Value.B, customLightColor.Value.A);
                        }
                        
                        spriteBatch.Draw(lightingTexture, lightRect, null, lightTint * alpha, 0f, Vector2.Zero, effects, 0f);
                    }
                }
                else
                {
                    if (_isPressed || currentTime < _lightingEndTime)
                    {
                        float age = (float)(_lightingEndTime - currentTime);
                        float alpha = _isPressed ? 0.35f : Math.Clamp(age / 150f * 0.35f, 0.0f, 0.35f);
                        
                        Color laneColor = GetLaneColor(_index);
                        var customLightColor = _skinManager?.GetColourLight(_totalColumnCount, _index);
                        if (customLightColor.HasValue)
                        {
                            laneColor = new Color(customLightColor.Value.R, customLightColor.Value.G, customLightColor.Value.B, customLightColor.Value.A);
                        }
                        
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
                }
            }

            if (keysUnderNotes)
            {
                DrawReceptor();
                DrawNotes();
            }
            else
            {
                DrawNotes();
                DrawReceptor();
            }

            DrawLighting();

            // Renderizar explosiones de acierto
            var skinnedExplosionFrames = _skinManager?.GetHitExplosionAnimation(_totalColumnCount, _index);
            int explosionCount = _explosions.Count;
            for (int i = 0; i < explosionCount; i++)
            {
                _explosions[i].Draw(spriteBatch, pixel, radialGlow, _x, _width, receptorY, currentTime, skinnedExplosionFrames);
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