#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Nullscent.Rulesets.Mania.Judgements;
using Nullscent.UI;
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
        private QueuedJudgement? _activeJudgement;
        private readonly object _lock = new(); // Objeto para control de concurrencia
        private int _currentCombo;
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
                _activeJudgement = new QueuedJudgement
                {
                    Result = result,
                    Column = column,
                    Offset = offsetMs,
                    Combo = combo,
                    Time = time
                };
            }

            // Notificar a los componentes externos (ej. HitErrorBar)
            JudgementOccurred?.Invoke(result, offsetMs, time);
        }

        public void Update(double currentTime)
        {
            lock (_lock)
            {
                if (_activeJudgement != null && currentTime - _activeJudgement.Time > DISPLAY_LIFETIME)
                {
                    _activeJudgement = null;
                }
            }
        }

        public void Draw(double currentTime)
        {
            if (_fontRenderer == null) return;

            // Dibujar el juicio activo
            lock (_lock)
            {
                if (_activeJudgement != null)
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

                        string text = GetTextForResult(_activeJudgement.Result);
                        _fontRenderer.SetFontSize(48);
                        Color color = GetColorForResult(_activeJudgement.Result) * alpha;

                        _fontRenderer.DrawTextCentered(text, (int)Position.X, textY, color, scale);
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
                    _fontRenderer.SetFontSize(36);
                    _fontRenderer.DrawTextCentered(_currentCombo.ToString(), (int)Position.X, (int)Position.Y - 30, Color.White * comboAlpha, comboScale);
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
            _activeJudgement = null;
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
