#nullable enable

using System;

namespace Nullscent.Gameplay
{
    /// <summary>
    /// Gestiona la barra de salud (HP) durante el gameplay.
    /// Implementa drenaje pasivo y ganancia/pérdida por juicios según osu!mania.
    /// </summary>
    public class HealthBar
    {
        /// <summary>
        /// Salud actual (0.0 - 1.0).
        /// </summary>
        public double Health { get; private set; } = 1.0;

        /// <summary>
        /// HP Drain Rate del beatmap (0-10).
        /// </summary>
        public float HPDrainRate { get; set; }

        /// <summary>
        /// Indica si el health drain está habilitado (típicamente false en modo práctica).
        /// </summary>
        public bool HealthDrainEnabled { get; set; } = false;

        /// <summary>
        /// Indica si el jugador ha fallado (HP llegó a 0).
        /// </summary>
        public bool IsFailed => Health <= 0.0;

        /// <summary>
        /// Tiempo acumulado de drenaje en segundos (usado para cálculo de drain).
        /// </summary>
        private double _drainAccumulator = 0.0;

        /// <summary>
        /// Inicializa la barra de salud.
        /// </summary>
        /// <param name="hpDrainRate">HP Drain Rate del beatmap</param>
        /// <param name="healthDrainEnabled">Si el drenaje de HP está habilitado</param>
        public HealthBar(float hpDrainRate, bool healthDrainEnabled = false)
        {
            HPDrainRate = hpDrainRate;
            HealthDrainEnabled = healthDrainEnabled;
            Health = 1.0; // Iniciar con HP lleno
        }

        /// <summary>
        /// Resetea la salud a su estado inicial.
        /// </summary>
        public void Reset()
        {
            Health = 1.0;
            _drainAccumulator = 0.0;
        }

        /// <summary>
        /// Actualiza el drenaje pasivo de HP (llamar cada frame).
        /// </summary>
        /// <param name="deltaTime">Tiempo transcurrido desde el último update en segundos</param>
        public void Update(double deltaTime)
        {
            if (!HealthDrainEnabled || IsFailed)
                return;

            // Drenaje pasivo basado en HP Drain Rate
            // Fórmula aproximada: drenar más rápido con HP más alto
            double drainRate = 0.01 * HPDrainRate;
            _drainAccumulator += deltaTime;

            // Aplicar drain cada 1 segundo (acumulado)
            if (_drainAccumulator >= 1.0)
            {
                Health -= drainRate;
                _drainAccumulator -= 1.0;

                // Clamp a 0
                if (Health < 0.0)
                    Health = 0.0;
            }
        }

        /// <summary>
        /// Aplica cambio de HP basado en un juicio.
        /// </summary>
        /// <param name="judgement">Juicio obtenido</param>
        public void ApplyJudgement(Judgement judgement)
        {
            double healthChange = GetHealthChangeForJudgement(judgement);
            Health += healthChange;

            // Clamp entre 0.0 y 1.0
            Health = Math.Clamp(Health, 0.0, 1.0);
        }

        /// <summary>
        /// Obtiene el cambio de HP correspondiente a un juicio.
        /// Valores aproximados basados en osu!mania (ajustables).
        /// </summary>
        /// <param name="judgement">Juicio</param>
        /// <returns>Cambio de HP (positivo = ganancia, negativo = pérdida)</returns>
        private double GetHealthChangeForJudgement(Judgement judgement)
        {
            // Escalado inverso por HP Drain Rate (HP más alto = cambios más pequeños)
            double scale = 1.0 / (1.0 + HPDrainRate * 0.1);

            return judgement switch
            {
                Judgement.Max => 0.015 * scale,      // +1.5% HP (escalado)
                Judgement.Perfect => 0.012 * scale,  // +1.2% HP
                Judgement.Great => 0.008 * scale,    // +0.8% HP
                Judgement.Ok => 0.002 * scale,       // +0.2% HP (mínima ganancia)
                Judgement.Meh => -0.005 * scale,     // -0.5% HP
                Judgement.Miss => -0.025 * scale,    // -2.5% HP (penalización grande)
                _ => 0.0
            };
        }

        /// <summary>
        /// Obtiene el color de la barra de salud según el nivel actual.
        /// </summary>
        /// <returns>Color como tupla RGB (0-255)</returns>
        public (byte R, byte G, byte B) GetHealthBarColor()
        {
            // Verde → Amarillo → Rojo según HP
            if (Health > 0.5)
            {
                // Verde a amarillo (1.0 → 0.5)
                byte red = (byte)((1.0 - Health) * 2.0 * 255);
                return (red, 255, 0);
            }
            else
            {
                // Amarillo a rojo (0.5 → 0.0)
                byte green = (byte)(Health * 2.0 * 255);
                return (255, green, 0);
            }
        }
    }
}
