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

        // Cola para manejar los juicios pendientes de renderizado
        private readonly List<QueuedJudgement> _queue = new();
        private readonly object _lock = new(); // Objeto para control de concurrencia

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
                _queue.Add(new QueuedJudgement
                {
                    Result = result,
                    Column = column,
                    Offset = offsetMs,
                    Combo = combo,
                    Time = time
                });

                // Limitar el tamaño de la cola para evitar desbordamientos
                if (_queue.Count > 8) _queue.RemoveAt(0);
            }

            // Notificar a los componentes externos (ej. HitErrorBar)
            JudgementOccurred?.Invoke(result, offsetMs, time);
        }

        public void Update(double currentTime)
        {
            lock (_lock)
            {
                // Eliminar juicios cuyo tiempo de vida visual haya expirado
                _queue.RemoveAll(j => currentTime - j.Time > DISPLAY_LIFETIME);
            }
        }

        public void Draw(double currentTime)
        {
            if (_fontRenderer == null) return;

            // Nota: No llamamos a Begin/End aquí porque esta función se llama
            // desde dentro de ManiaPlayfield.Draw() que ya tiene un Begin() activo.
            // Solo dibujamos el contenido.

            QueuedJudgement? latest = null;
            lock (_lock)
            {
                // Obtener el juicio más reciente para renderizarlo
                if (_queue.Count > 0) latest = _queue[^1];
            }

            if (latest != null)
            {
                // Calcular el progreso de la animación basado en la edad del juicio
                double age = currentTime - latest.Time;
                float progress = 1f - (float)(age / DISPLAY_LIFETIME);
                progress = Math.Clamp(progress, 0f, 1f);

                string text = latest.Result.ToString();
                int combo = latest.Combo;

                // Calcular escala y desvanecimiento (fade out)
                float scale = 1.0f + 0.5f * progress;
                float alpha = 0.6f + 0.4f * progress;

                _fontRenderer.SetFontSize(48);
                var color = GetColorForResult(latest.Result) * alpha;

                // Dibujar el texto del juicio centrado
                _fontRenderer.DrawTextCentered(text, (int)Position.X, (int)Position.Y - 20, color, scale);

                // Dibujar el contador de combo debajo del juicio si es mayor a 1
                if (combo > 1)
                {
                    _fontRenderer.SetFontSize(36);
                    _fontRenderer.DrawTextCentered(combo + "x", (int)Position.X, (int)Position.Y + 30, Color.Yellow * alpha, 1.0f + 0.3f * progress);
                }
            }
        }

        /// <summary>
        /// Retorna el color correspondiente según el tipo de juicio.
        /// </summary>
        private Color GetColorForResult(HitResult result)
        {
            return result switch
            {
                HitResult.Perfect => new Color(255, 215, 100),
                HitResult.Great => new Color(150, 255, 150),
                HitResult.Good => new Color(150, 200, 255),
                HitResult.Ok => new Color(200, 150, 255),
                HitResult.Meh => new Color(200, 200, 200),
                _ => Color.White
            };
        }

        public void Dispose()
        {
            _queue.Clear();
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
