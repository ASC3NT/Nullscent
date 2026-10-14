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
        private bool _layoutDirty;
        private ManiaLayoutResult? _layoutResult;
        private bool _layoutDownScroll;
        private float? _layoutReceptorPositionOverride;
    private float _layoutNoteHeightScale = 1f;
    private float _layoutReceptorHeightScale = 1f;

        // Propiedades visuales de la pista (stage)
        private int _stageX;
        private int _stageWidth;
        private int _screenHeight;
        private int _screenWidth;
        public int ReceptorY { get; private set; }
        public int ReceptorSpriteY { get; private set; }

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
            if (_skinManager != null) _skinManager.SkinChanged += OnSkinChanged;

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
            _judgementDisplayManager.KeyCount = _columnCount;

            // Calcular layout con anchos y espaciados de la skin
            bool initialDownScroll = ManiaSkinRules.ResolveDownScroll(Core.SettingsService.Instance.DownScroll,
                Core.SettingsService.Instance.UseSkinScrollDirection,
                _skinManager?.GetUpsideDownPreference(columnCount));
            CalculateLayout(screenWidth, screenHeight, initialDownScroll, null,
                Core.SettingsService.Instance.SkinNoteHeightScale, Core.SettingsService.Instance.SkinReceptorHeightScale);

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

        private void CalculateLayout(int screenWidth, int screenHeight, bool downScroll, float? receptorPositionOverride,
            float noteHeightScale, float receptorHeightScale)
        {
            if (screenWidth <= 0 || screenHeight <= 0) return;
            _screenHeight = screenHeight;
            _screenWidth = screenWidth;
            _layoutDownScroll = downScroll;
            _layoutReceptorPositionOverride = receptorPositionOverride;
            _layoutNoteHeightScale = noteHeightScale;
            _layoutReceptorHeightScale = receptorHeightScale;
            ManiaKeymodeDefinition? profile = _skinManager?.GetManiaProfile(_columnCount);
            _layoutResult = ManiaLayoutEngine.Calculate(profile, _columnCount, screenWidth, screenHeight, downScroll,
                new ManiaLayoutOverrides(ReceptorPositionRatio: receptorPositionOverride,
                    NoteScale: noteHeightScale, ReceptorScale: receptorHeightScale));
            if (_judgementDisplayManager != null) _judgementDisplayManager.LayoutScale = _layoutResult.Scale;
            _stageX = (int)MathF.Round(_layoutResult.Playfield.X);
            _stageWidth = (int)MathF.Round(_layoutResult.Playfield.Width);
            ReceptorY = (int)MathF.Round(_layoutResult.ReceptorY);
            ReceptorSpriteY = (int)MathF.Round(_layoutResult.ReceptorSpriteY);
            for (int i = 0; i < _columns.Count; i++)
            {
                SkinRect lane = _layoutResult.Lanes[i];
                _columns[i].X = (int)MathF.Round(lane.X);
                _columns[i].Width = Math.Max(1, (int)MathF.Round(lane.Width));
                _columns[i].ScreenHeight = screenHeight;
                _columns[i].LayoutScale = _layoutResult.Scale;
                _columns[i].NoteHeightScale = _layoutResult.NoteScale;
                _columns[i].ReceptorHeightScale = _layoutResult.ReceptorScale;
                _columns[i].ScrollTop = (int)MathF.Round(_layoutResult.OffsetY);
                _columns[i].ScrollBottom = (int)MathF.Round(_layoutResult.OffsetY + ManiaLayoutEngine.DesignHeight * _layoutResult.Scale);
            }
            if (_judgementDisplayManager != null)
            {
                float scoreLogicalY = profile?.ScorePosition ?? 480f;
                float comboLogicalY = profile?.ComboPosition ?? 177.6f;
                if (!downScroll)
                {
                    scoreLogicalY = ManiaLayoutEngine.DesignHeight - scoreLogicalY;
                    comboLogicalY = ManiaLayoutEngine.DesignHeight - comboLogicalY;
                }
                float centerX = _stageX + _stageWidth / 2f;
                _judgementDisplayManager.Position = new Vector2(centerX, _layoutResult.ToViewportY(scoreLogicalY));
                _judgementDisplayManager.ComboPosition = new Vector2(centerX, _layoutResult.ToViewportY(comboLogicalY));
            }
        }

        private void OnSkinChanged(long version) => _layoutDirty = true;

        public void Update(double currentTime, ManiaScoreProcessor scoreProcessor, double scrollSpeed, float? receptorPosition, bool downScroll,
            float noteHeightScale = 1f, float receptorHeightScale = 1f)
        {
            _currentCombo = scoreProcessor.Combo;
            int currentWidth = _graphicsDevice.Viewport.Width;
            int currentHeight = _graphicsDevice.Viewport.Height;

            // Check if window was resized and update layout
            if (_layoutDirty || _screenHeight != currentHeight || _screenWidth != currentWidth || _layoutDownScroll != downScroll
                || _layoutReceptorPositionOverride != receptorPosition || _layoutNoteHeightScale != noteHeightScale
                || _layoutReceptorHeightScale != receptorHeightScale)
            {
                _screenWidth = currentWidth;
                CalculateLayout(currentWidth, currentHeight, downScroll, receptorPosition, noteHeightScale, receptorHeightScale);
                _layoutDirty = false;
            }

            float effectiveReceptorRatio = receptorPosition ?? ReceptorY / (float)Math.Max(1, _screenHeight);
            ReceptorY = (int)MathF.Round(_layoutResult?.ReceptorY ?? effectiveReceptorRatio * _screenHeight);
            ReceptorSpriteY = (int)MathF.Round(_layoutResult?.ReceptorSpriteY ?? ReceptorY);

            // Propagar actualización a cada columna
            foreach (var column in _columns)
            {
                int scrollTop = (int)MathF.Round(_layoutResult?.OffsetY ?? 0);
                int scrollBottom = (int)MathF.Round((_layoutResult?.OffsetY ?? 0) + ManiaLayoutEngine.DesignHeight * (_layoutResult?.Scale ?? 1f));
                column.Update(currentTime, scoreProcessor, scrollSpeed, effectiveReceptorRatio, downScroll, ReceptorY, scrollTop, scrollBottom);
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

        public void Draw(double currentTime, double scrollSpeed, float? receptorPosition, bool downScroll,
            float noteHeightScale = 1f, float receptorHeightScale = 1f)
        {
            if (_whitePixel == null || _radialGlow == null || _linearGradient == null) return;

            int currentWidth = _graphicsDevice.Viewport.Width;
            int currentHeight = _graphicsDevice.Viewport.Height;
            if (_layoutDirty || _screenWidth != currentWidth || _screenHeight != currentHeight || _layoutDownScroll != downScroll
                || _layoutReceptorPositionOverride != receptorPosition || _layoutNoteHeightScale != noteHeightScale
                || _layoutReceptorHeightScale != receptorHeightScale)
            {
                CalculateLayout(currentWidth, currentHeight, downScroll, receptorPosition, noteHeightScale, receptorHeightScale);
                _layoutDirty = false;
            }

            _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied);

            DrawStageBackground(downScroll);

            // Stage background and scrolling notes/keys are separate from
            // top-level effects. osu! draws StageBottom between these layers.
            foreach (var column in _columns)
            {
                column.Draw(_spriteBatch, _whitePixel, currentTime, scrollSpeed, downScroll, ReceptorY, ReceptorSpriteY,
                    _layoutResult?.Scale ?? 1f, (int)MathF.Round(_layoutResult?.OffsetY ?? 0));
            }

            DrawStageBottom(downScroll, currentTime);

            // Dibujar el desvanecimiento de Flashlight si está activo
            if (_isFlashlightActive)
            {
                DrawFlashlightMask(downScroll);
            }

            // Dibujar el gestor de juicios por encima de la pista
            Color? holdComboColour = GetHoldComboColour();
            _judgementDisplayManager?.Draw(currentTime, _skinManager, holdComboColour);

            // osu! places column hit explosions and active LN lighting in the
            // top-level container after the judgement layer.
            foreach (var column in _columns)
                column.DrawForeground(_spriteBatch, _radialGlow, currentTime, ReceptorY);

            _spriteBatch.End();
        }

        private Color? GetHoldComboColour()
        {
            if (_skinManager == null) return null;
            Column? holdingColumn = _columns.FirstOrDefault(column => column.IsHolding);
            if (holdingColumn == null) return null;
            var configured = _skinManager.GetColourHold(_columnCount, holdingColumn.Index)
                ?? _skinManager.GetGlobalColourHold(_columnCount) ?? (255, 191, 51, 255);
            return new Color(configured.R, configured.G, configured.B, configured.A);
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
            ManiaKeymodeDefinition? profile = _skinManager?.GetManiaProfile(_columnCount);
            ManiaLayoutResult? layout = _layoutResult;

            // Fondo oscuro para la pista
            var stageRect = new Rectangle(_stageX, 0, _stageWidth, _screenHeight);
            if (profile == null)
                _spriteBatch.Draw(_whitePixel, stageRect, new Color(15, 15, 20, 220));

            ManiaStageStyleDefinition stageStyle = profile?.StageStyle ?? ManiaStageStyleDefinition.Default;
            Texture2D? stageLeft = _skinManager?.GetStageTexture(_columnCount, "left");
            Texture2D? stageRight = _skinManager?.GetStageTexture(_columnCount, "right");
            float layoutScale = layout?.Scale ?? 1f;

            // Legacy stage pieces are composed back-to-front: side artwork, lane
            // backgrounds/lines, then the centered hit target. See osu!'s legacy
            // StageBackground child order:
            // https://github.com/ppy/osu/blob/master/osu.Game.Rulesets.Mania/Skinning/Legacy/LegacyStageBackground.cs
            if (layout != null && _skinManager != null && stageLeft != null)
            {
                Vector2 size = _skinManager.GetLogicalSize(stageLeft);
                SkinRect rect = ManiaLayoutEngine.GetStageSideRect(layout, size.X, isRight: false, stageStyle);
                _spriteBatch.Draw(stageLeft, ToRectangle(rect), Color.White);
            }
            if (layout != null && _skinManager != null && stageRight != null)
            {
                Vector2 size = _skinManager.GetLogicalSize(stageRight);
                SkinRect rect = ManiaLayoutEngine.GetStageSideRect(layout, size.X, isRight: true, stageStyle);
                _spriteBatch.Draw(stageRight, ToRectangle(rect), Color.White);
            }

            foreach (var column in _columns)
                column.DrawBackground(_spriteBatch, _whitePixel);

            // Dibujar lineas de columna (Column Lines)
            if (profile != null && _skinManager != null)
            {
                float[]? widths = _skinManager.GetColumnLineWidths(_columnCount);
                bool[]? enabled = _skinManager.GetColumnLines(_columnCount);
                var colour = _skinManager.GetColourColumnLine(_columnCount);
                Color lineColor = colour.HasValue
                    ? new Color(colour.Value.R, colour.Value.G, colour.Value.B, colour.Value.A) : Color.White;
                bool drawBothEdges = profile.DrawColumnEdgesBothSides;
                float unscaledSpriteScale = layoutScale;
                for (int i = 0; i < _columns.Count; i++)
                {
                    if (enabled != null && i < enabled.Length && !enabled[i]) continue;
                    float leftWidth = widths != null && i < widths.Length && float.IsFinite(widths[i]) ? widths[i] : 2f;
                    int leftPixels = (int)MathF.Round(leftWidth * unscaledSpriteScale);
                    if (leftPixels > 0)
                        _spriteBatch.Draw(_whitePixel, new Rectangle(_columns[i].X, 0, leftPixels, _screenHeight), lineColor);

                    if (i == _columns.Count - 1 || drawBothEdges)
                    {
                        if (enabled != null && i + 1 < enabled.Length && !enabled[i + 1]) continue;
                        float rightWidth = widths != null && i + 1 < widths.Length && float.IsFinite(widths[i + 1]) ? widths[i + 1] : 2f;
                        int rightPixels = (int)MathF.Round(rightWidth * unscaledSpriteScale);
                        if (rightPixels > 0)
                            _spriteBatch.Draw(_whitePixel, new Rectangle(_columns[i].X + _columns[i].Width - rightPixels,
                                0, rightPixels, _screenHeight), lineColor);
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

            // osu! places the hit target after stage/column backgrounds and before
            // StageLight, notes and keys. StageHint is centered on the judgement line.
            Texture2D? stageHint = _skinManager?.GetStageTexture(_columnCount, "hint");
            if (layout != null && _skinManager != null && stageHint != null)
            {
                Vector2 size = _skinManager.GetLogicalSize(stageHint);
                SkinRect rect = ManiaLayoutEngine.GetStageHintRect(layout, size.X, size.Y, stageStyle);
                _spriteBatch.Draw(stageHint, ToRectangle(rect), Color.White);
            }

            bool drawJudgementLine = profile?.JudgementLine ?? true;
            if (drawJudgementLine)
            {
                int lineHeight = Math.Max(1, (int)MathF.Round(layoutScale));
                var lineRect = new Rectangle(_stageX, ReceptorY - lineHeight / 2, _stageWidth, lineHeight);
                Color lineColor = Color.White;
                var customJudgementColor = _skinManager?.GetColourJudgementLine(_columnCount);
                if (customJudgementColor.HasValue)
                    lineColor = new Color(customJudgementColor.Value.R, customJudgementColor.Value.G,
                        customJudgementColor.Value.B, customJudgementColor.Value.A);
                _spriteBatch.Draw(_whitePixel, lineRect, lineColor);
            }
        }

        private void DrawStageBottom(bool downScroll, double currentTime)
        {
            ResolvedManiaKeymode? mode = _skinManager?.GetResolvedKeymode(_columnCount);
            if (mode == null || !mode.StageAssets.TryGetValue("bottom", out ResolvedSkinAnimation? bottomAnimation)) return;
            Texture2D? stageBottom = bottomAnimation.FrameAt(currentTime);
            if (stageBottom == null || _layoutResult == null) return;
            Vector2 size = _skinManager!.GetLogicalSize(stageBottom);
            SkinRect rect = ManiaLayoutEngine.GetStageBottomRect(_layoutResult, size.X, size.Y, downScroll,
                mode.Definition.StageStyle);
            _spriteBatch.Draw(stageBottom, ToRectangle(rect), Color.White);
        }

        private static Rectangle ToRectangle(SkinRect rect) => new(
            (int)MathF.Round(rect.X), (int)MathF.Round(rect.Y),
            Math.Max(1, (int)MathF.Round(rect.Width)), Math.Max(1, (int)MathF.Round(rect.Height)));

        internal static Color GetDoubledAlphaColour(Color color)
        {
            byte alpha = (byte)Math.Clamp((int)MathF.Round(color.A * (color.A / 255f)), 0, 255);
            return new Color(color.R, color.G, color.B, alpha);
        }

        public void Dispose()
        {
            if (_skinManager != null) _skinManager.SkinChanged -= OnSkinChanged;
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
        private static readonly BlendState StageLightBlend = new()
        {
            ColorSourceBlend = Blend.DestinationColor,
            ColorDestinationBlend = Blend.InverseSourceAlpha,
            AlphaSourceBlend = Blend.One,
            AlphaDestinationBlend = Blend.InverseSourceAlpha
        };
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
        private double _stageLightEndTime;
        private double _holdLightingEndTime;
        private int _totalColumnCount;
        private readonly HitSoundPlayer? _hitSoundPlayer;

        public int X { get => _x; set => _x = value; }
        public int Index => _index;
        public int Width { get => _width; set => _width = value; }
        public int ScreenHeight { get => _screenHeight; set => _screenHeight = value; }
        public float LayoutScale { get; set; } = 1f;
        public float NoteHeightScale { get; set; } = 1f;
        public float ReceptorHeightScale { get; set; } = 1f;
        public int ScrollTop { get; set; }
        public int ScrollBottom { get; set; }
        public bool IsHiddenActive { get; set; }
        public bool IsPressed => _isPressed;
        public bool IsHolding => _isHolding;

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

        public void Update(double currentTime, ManiaScoreProcessor scoreProcessor, double scrollSpeed, float receptorPosition,
            bool downScroll, int receptorY, int scrollTop, int scrollBottom)
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
                drawable.Update(currentTime, scrollSpeed, receptorPosition, downScroll, _screenHeight, receptorY, scrollTop, scrollBottom);

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
            _stageLightEndTime = 0;
            _holdLightingEndTime = 0;
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
            _stageLightEndTime = currentTime + 250;
            _holdLightingEndTime = currentTime + 120;

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

        public void DrawBackground(SpriteBatch spriteBatch, Texture2D pixel)
        {
            // Fondo de la columna alternando colores para distinguir separación
            Color columnBg = _index % 2 == 0 ? new Color(20, 20, 25, 180) : new Color(25, 25, 30, 180);
            if (_skinManager?.GetManiaProfile(_totalColumnCount) != null)
                columnBg = Color.Black;
            
            // Si la skin define un color específico para esta columna, lo usamos
            var customColColor = _skinManager?.GetColumnColorMap(_totalColumnCount, _index);
            if (customColColor.HasValue)
            {
                columnBg = new Color(customColColor.Value.R, customColColor.Value.G,
                    customColColor.Value.B, customColColor.Value.A);
            }
            
            var columnRect = new Rectangle(_x, 0, _width, _screenHeight);
            spriteBatch.Draw(pixel, columnRect, columnBg);
        }

        public void Draw(SpriteBatch spriteBatch, Texture2D pixel, double currentTime, double scrollSpeed,
            bool downScroll, int receptorY, int receptorSpriteY, float layoutScale, int layoutOffsetY)
        {

            // StageLight is part of the column background, below notes and keys.
            float stageLightProgress = _isPressed ? 1f : (float)Math.Clamp((_stageLightEndTime - currentTime) / 250d, 0, 1);
            if (stageLightProgress > 0)
            {
                ResolvedManiaKeymode? resolvedMode = _skinManager?.GetResolvedKeymode(_totalColumnCount);
                Texture2D? stageLight = resolvedMode != null && resolvedMode.StageAssets.TryGetValue("light", out ResolvedSkinAnimation? stageLightAnimation)
                    ? stageLightAnimation.FrameAt(currentTime) : null;
                if (stageLight != null)
                {
                    Color lightTint = Color.White;
                    var customLightColor = _skinManager?.GetStageLightColour(_totalColumnCount, _index);
                    if (customLightColor.HasValue)
                    {
                        lightTint = new Color(customLightColor.Value.R, customLightColor.Value.G, customLightColor.Value.B, customLightColor.Value.A);
                    }

                    float logicalLightPosition = resolvedMode?.Definition.LightPosition
                        ?? (downScroll ? ManiaLayoutEngine.DesignHeight : 0f);
                    if (!downScroll) logicalLightPosition = ManiaLayoutEngine.DesignHeight - logicalLightPosition;
                    int lightPos = (int)MathF.Round(layoutOffsetY + logicalLightPosition * layoutScale);
                    Vector2 lightSize = _skinManager!.GetLogicalSize(stageLight);
                    float aspect = lightSize.Y / lightSize.X;
                    int lightW = _width;
                    int fullLightHeight = Math.Max(1, (int)MathF.Round(_width * aspect));
                    int lightY = downScroll ? lightPos - fullLightHeight : lightPos;
                    var lightRect = new Rectangle(_x, lightY, lightW, fullLightHeight);
                    
                    SpriteEffects effects = downScroll ? SpriteEffects.None : SpriteEffects.FlipVertically;
                    spriteBatch.End();
                    spriteBatch.Begin(SpriteSortMode.Deferred, StageLightBlend);
                    spriteBatch.Draw(stageLight, lightRect, null, lightTint * stageLightProgress, 0f, Vector2.Zero, effects, 0f);
                    spriteBatch.End();
                    spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied);
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
                    drawable.Draw(spriteBatch, pixel, _skinManager, _totalColumnCount, _index, _x, currentTime, scrollSpeed,
                        receptorY, downScroll, _screenHeight, _width, LayoutScale, NoteHeightScale);
                }
            }

            // Local function to draw Receptor
            void DrawReceptor()
            {
                ResolvedManiaKeymode? resolvedMode = _skinManager?.GetResolvedKeymode(_totalColumnCount);
                ResolvedManiaLane? resolvedLane = resolvedMode != null && _index < resolvedMode.Lanes.Count
                    ? resolvedMode.Lanes[_index] : null;
                ResolvedSkinAnimation? keyAnimation = _isPressed
                    ? resolvedLane?.PressedReceptor ?? resolvedLane?.Receptor : resolvedLane?.Receptor;
                Texture2D? keyTexture = keyAnimation?.FrameAt(currentTime);
                if (keyTexture != null)
                {
                    SkinSpriteMetrics metrics = _skinManager!.GetSpriteMetrics(_totalColumnCount, _index, "receptor");
                    Vector2 keySize = _skinManager!.GetLogicalSize(keyTexture);
                    float keyWidth = Math.Max(1f, _width * metrics.WidthRatio);
                    float keyHeight = metrics.HeightMode switch
                    {
                        SpriteHeightMode.ReferenceWidthAspect when metrics.ReferenceWidth > 0f => metrics.ReferenceWidth * LayoutScale * keySize.Y / keySize.X,
                        SpriteHeightMode.LaneWidthFraction => _width * metrics.HeightValue,
                        _ => keyWidth * keySize.Y / keySize.X
                    } * ReceptorHeightScale;

                    int keyY = (int)MathF.Round(downScroll ? receptorSpriteY - keyHeight + metrics.OffsetY * LayoutScale : receptorSpriteY + metrics.OffsetY * LayoutScale);
                    int keyX = (int)MathF.Round(_x + (_width - keyWidth) / 2f + _width * metrics.OffsetXRatio);
                    var keyRect = new Rectangle(keyX, keyY, Math.Max(1, (int)MathF.Round(keyWidth)), Math.Max(1, (int)MathF.Round(keyHeight)));
                    SpriteEffects effects = _skinManager.FlipKey(_totalColumnCount, _index, _isPressed, downScroll)
                        ? SpriteEffects.FlipVertically : SpriteEffects.None;
                    spriteBatch.Draw(keyTexture, keyRect, null, Color.White, 0f, Vector2.Zero, effects, 0f);
                }
                else
                {
                    int keyHeight = 20;
                    int keyY = downScroll ? receptorSpriteY - keyHeight : receptorSpriteY;
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
        }

        public void DrawForeground(SpriteBatch spriteBatch, Texture2D radialGlow, double currentTime, int receptorY)
        {
            DrawHoldLighting(spriteBatch, currentTime, receptorY);

            ResolvedManiaLane? resolvedLane = _skinManager?.GetResolvedKeymode(_totalColumnCount).Lanes[_index];
            ResolvedSkinAnimation? skinnedExplosion = resolvedLane?.NoteLighting;
            int explosionCount = _explosions.Count;
            bool additiveExplosions = skinnedExplosion != null && explosionCount > 0;
            if (additiveExplosions)
            {
                spriteBatch.End();
                spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive);
            }
            for (int i = 0; i < explosionCount; i++)
            {
                float[]? normalLightingWidths = _skinManager?.GetLightingNWidths(_totalColumnCount);
                float? normalLightingWidth = normalLightingWidths != null && _index < normalLightingWidths.Length
                    ? normalLightingWidths[_index] : null;
                var lightColour = _skinManager?.GetColourLight(_totalColumnCount, _index);
                Color? lightTint = lightColour is { } c ? new Color(c.R, c.G, c.B, c.A) : null;
                _explosions[i].Draw(spriteBatch, _pixel, radialGlow, _x, _width, receptorY, currentTime,
                    skinnedExplosion, _skinManager, LayoutScale, normalLightingWidth, lightTint);
            }
            if (additiveExplosions)
            {
                spriteBatch.End();
                spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied);
            }
        }

        private void DrawHoldLighting(SpriteBatch spriteBatch, double currentTime, int receptorY)
        {
            if (!_isHolding && currentTime >= _holdLightingEndTime) return;

            ResolvedManiaLane? lane = _skinManager?.GetResolvedKeymode(_totalColumnCount).Lanes[_index];
            ResolvedSkinAnimation? lightingAnimation = lane?.HoldLighting;
            Texture2D? lightingTexture = lightingAnimation?.FrameAt(currentTime);
            bool isLightingEnabled = _skinManager?.IsLightingEnabled(_totalColumnCount) ?? true;
            if (lightingTexture == null || !isLightingEnabled) return;

            float alpha = _isHolding ? 0.4f : Math.Clamp((float)((_holdLightingEndTime - currentTime) / 120d) * 0.4f, 0f, 0.4f);
            Vector2 logicalSize = _skinManager!.GetLogicalSize(lightingTexture);
            float targetWidth = logicalSize.X * LayoutScale;
            float[]? configuredWidths = _skinManager.GetLightingLWidths(_totalColumnCount);
            if (configuredWidths != null && _index < configuredWidths.Length && configuredWidths[_index] > 0)
                targetWidth = configuredWidths[_index] * LayoutScale;

            int lightW = Math.Max(1, (int)MathF.Round(targetWidth));
            int lightH = Math.Max(1, (int)MathF.Round(targetWidth * logicalSize.Y / logicalSize.X));
            int lightX = _x + (_width - lightW) / 2;
            int lightY = receptorY - lightH / 2;
            Rectangle lightRect = new Rectangle(lightX, lightY, lightW, lightH);

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive);
            var configuredTint = _skinManager.GetColourLight(_totalColumnCount, _index);
            Color tint = configuredTint is { } c ? new Color(c.R, c.G, c.B, c.A) : Color.White;
            spriteBatch.Draw(lightingTexture, lightRect, null, tint * alpha);
            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied);
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
