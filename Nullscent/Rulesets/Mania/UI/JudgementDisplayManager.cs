#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Nullscent.Rulesets.Mania.Judgements;
using Nullscent.UI;
using Nullscent.Skin;
using System;
using System.Collections.Generic;

namespace Nullscent.Rulesets.Mania.UI
{
    /// <summary>
    /// Gestor central para la visualización de juicios y combos (similar a osu!mania).
    /// Recibe eventos de juicio y renderiza un único elemento visual centrado.
    /// </summary> 
    public class JudgementDisplayManager : IDisposable
    {
        private readonly GraphicsDevice _graphicsDevice;
        private readonly SpriteBatch _spriteBatch;
        private readonly TrueTypeFontRenderer? _fontRenderer;

        // Juicio activo actualmente (se muestra un único juicio a la vez, estilo osu!stable)
        private readonly QueuedJudgement _activeJudgement = new();
        private bool _hasActiveJudgement;
        private readonly object _lock = new(); // Objeto para control de concurrencia
        private int _currentCombo;
        private int _cachedComboValue = -1;
        private string _cachedComboString = "";
        private double _lastJudgementTime;

        // Posición en pantalla (generalmente el centro del playfield)
        public Vector2 Position { get; set; }

        // Tiempo de vida visual de cada juicio en pantalla (en milisegundos)
        private const double DISPLAY_LIFETIME = 800;

        // =========================================================
        // EVENTOS
        // Evento disparado cada vez que se registra un juicio.
        // Parámetros: HitResult (resultado), double (desfase en ms), double (tiempo actual)
        // =========================================================
        public event Action<HitResult, double, double>? JudgementOccurred;

        public JudgementDisplayManager(GraphicsDevice graphicsDevice, SpriteBatch spriteBatch, TrueTypeFontRenderer? fontRenderer)
        {
            _graphicsDevice = graphicsDevice;
            _spriteBatch = spriteBatch;
            _fontRenderer = fontRenderer;
        }

        /// <summary>
        /// Encola un nuevo juicio para ser mostrado en pantalla y notifica a los suscriptores.
        /// </summary>
        public void EnqueueJudgement(HitResult result, int column, double offsetMs, int combo, double time)
        {
            lock (_lock)
            {
                _currentCombo = combo;
                _lastJudgementTime = time;
                _activeJudgement.Result = result;
                _activeJudgement.Column = column;
                _activeJudgement.Offset = offsetMs;
                _activeJudgement.Combo = combo;
                _activeJudgement.Time = time;
                _hasActiveJudgement = true;
            }

            // Notificar a los componentes externos (ej. HitErrorBar)
            JudgementOccurred?.Invoke(result, offsetMs, time);
        }

        public void Update(double currentTime)
        {
            lock (_lock)
            {
                if (_hasActiveJudgement && currentTime - _activeJudgement.Time > DISPLAY_LIFETIME)
                {
                    _hasActiveJudgement = false;
                }
            }
        }

        public void Draw(double currentTime, SkinManager? skinManager)
        {
            // Dibujar el juicio activo
            lock (_lock)
            {
                if (_hasActiveJudgement)
                {
                    double age = currentTime - _activeJudgement.Time;
                    if (age >= 0 && age <= DISPLAY_LIFETIME)
                    {
                        float scale = 1.0f;
                        float alpha = 1.0f;

                        // Animación de pop-up con rebote
                        if (age < 80)
                        {
                            float t = (float)(age / 80.0);
                            scale = MathHelper.Lerp(0.7f, 1.3f, t);
                        }
                        else if (age < 150)
                        {
                            float t = (float)((age - 80.0) / 70.0);
                            scale = MathHelper.Lerp(1.3f, 1.0f, t);
                        }
                        else if (age < 500)
                        {
                            scale = 1.0f;
                        }
                        else
                        {
                            float t = (float)((age - 500.0) / (DISPLAY_LIFETIME - 500.0));
                            scale = MathHelper.Lerp(1.0f, 0.9f, t);
                            alpha = MathHelper.Lerp(1.0f, 0.0f, t);
                        }

                        // Desplazamiento vertical hacia arriba (drift de 40px)
                        float driftY = (float)(age / DISPLAY_LIFETIME) * 40.0f;
                        int textY = (int)(Position.Y + 15 - driftY);

                        Texture2D? judgementTexture = skinManager?.GetJudgementTexture(_activeJudgement.Result.ToString());
                        if (judgementTexture != null)
                        {
                            float texScale = skinManager != null ? skinManager.GetTextureScale(judgementTexture) : 1.0f;
                            int texW = (int)(judgementTexture.Width * scale * texScale);
                            int texH = (int)(judgementTexture.Height * scale * texScale);
                            var destRect = new Rectangle((int)Position.X - texW / 2, textY - texH / 2, texW, texH);
                            _spriteBatch.Draw(judgementTexture, destRect, Color.White * alpha);
                        }
                        else if (_fontRenderer != null)
                        {
                            string text = GetTextForResult(_activeJudgement.Result);
                            _fontRenderer.SetFontSize(48);
                            Color color = GetColorForResult(_activeJudgement.Result) * alpha;
                            _fontRenderer.DrawTextCentered(text, (int)Position.X, textY, color, scale);
                        }
                    }
                }
            }

            // Dibujar el contador de combo (si combo > 1)
            if (_currentCombo > 1)
            {
                double timeSinceLastHit = currentTime - _lastJudgementTime;
                float comboAlpha = 1.0f;
                float comboScale = 1.0f;

                // Animación de rebote de escala en combo
                if (timeSinceLastHit < 100)
                {
                    float t = (float)(timeSinceLastHit / 100.0);
                    comboScale = MathHelper.Lerp(1.3f, 1.0f, t);
                }

                // Desvanecer combo suave tras 1 segundo de inactividad
                if (timeSinceLastHit > 1000)
                {
                    float t = (float)((timeSinceLastHit - 1000.0) / 1000.0);
                    comboAlpha = MathHelper.Lerp(1.0f, 0.0f, Math.Clamp(t, 0.0f, 1.0f));
                }

                if (comboAlpha > 0f)
                {
                    if (_cachedComboValue != _currentCombo)
                    {
                        _cachedComboValue = _currentCombo;
                        _cachedComboString = _currentCombo.ToString();
                    }

                    bool drawn = skinManager != null && skinManager.DrawNumber(_spriteBatch, _cachedComboString, new Vector2(Position.X, Position.Y - 35), "default", comboScale * 0.75f, comboAlpha);
                    if (!drawn && _fontRenderer != null)
                    {
                        _fontRenderer.SetFontSize(36);
                        _fontRenderer.DrawTextCentered(_cachedComboString, (int)Position.X, (int)Position.Y - 30, Color.White * comboAlpha, comboScale);
                    }
                }
            }
        }

        private string GetTextForResult(HitResult result)
        {
            return result switch
            {
                HitResult.Perfect => "MAX",
                HitResult.Great => "300",
                HitResult.Good => "200",
                HitResult.Ok => "100",
                HitResult.Meh => "50",
                HitResult.Miss => "Miss",
                _ => ""
            };
        }

        /// <summary>
        /// Retorna el color correspondiente según el tipo de juicio.
        /// </summary>
        private Color GetColorForResult(HitResult result)
        {
            return result switch
            {
                HitResult.Perfect => new Color(84, 200, 255),   // Cyan / Celeste MAX
                HitResult.Great => new Color(255, 220, 0),      // Yellow / Gold 300
                HitResult.Good => new Color(100, 255, 100),     // Green 200
                HitResult.Ok => new Color(255, 150, 50),        // Orange 100
                HitResult.Meh => new Color(180, 100, 255),      // Purple 50
                HitResult.Miss => new Color(255, 50, 50),       // Red Miss
                _ => Color.White
            };
        }

        public void Dispose()
        {
            _hasActiveJudgement = false;
        }

        // Estructura interna para almacenar datos de juicios encolados
        private class QueuedJudgement
        {
            public HitResult Result { get; set; }
            public int Column { get; set; }
            public double Offset { get; set; }
            public int Combo { get; set; }
            public double Time { get; set; }
        }
    }
}
