#nullable enable

using System;

namespace Nullscent.Gameplay
{
    /// <summary>
    /// Define los tipos de juicio posibles en osu!mania.
    /// </summary>
    public enum Judgement
    {
        /// <summary>
        /// Miss - fuera de todas las ventanas de timing (188ms).
        /// </summary>
        Miss = 0,

        /// <summary>
        /// 50 - ventana más amplia pero pobre.
        /// </summary>
        Meh = 50,

        /// <summary>
        /// 100 - OK/Good.
        /// </summary>
        Ok = 100,

        /// <summary>
        /// 200 - Great.
        /// </summary>
        Great = 200,

        /// <summary>
        /// 300 - Perfect.
        /// </summary>
        Perfect = 300,

        /// <summary>
        /// 300g (MAX) - Perfect con timing óptimo (rainbow 300).
        /// </summary>
        Max = 320
    }

    /// <summary>
    /// Sistema de juicio para osu!mania que implementa las ventanas de timing basadas en OD.
    /// Replica exactamente el comportamiento de osu!lazer para consistencia.
    /// </summary>
    public class HitJudge
    {
        /// <summary>
        /// Overall Difficulty del beatmap actual (0-10).
        /// </summary>
        public float OverallDifficulty { get; set; }

        /// <summary>
        /// Ventana de MAX (300g) en milisegundos (fija en 16ms).
        /// </summary>
        public const double MaxWindow = 16.0;

        /// <summary>
        /// Ventana de 300 (Perfect) en milisegundos (calculada según OD).
        /// </summary>
        public double PerfectWindow { get; private set; }

        /// <summary>
        /// Ventana de 200 (Great) en milisegundos (calculada según OD).
        /// </summary>
        public double GreatWindow { get; private set; }

        /// <summary>
        /// Ventana de 100 (OK) en milisegundos (calculada según OD).
        /// </summary>
        public double OkWindow { get; private set; }

        /// <summary>
        /// Ventana de 50 (Meh) en milisegundos (calculada según OD).
        /// </summary>
        public double MehWindow { get; private set; }

        /// <summary>
        /// Ventana de MISS en milisegundos (fija en 188ms).
        /// </summary>
        public const double MissWindow = 188.0;

        /// <summary>
        /// Inicializa el sistema de juicio con un OD específico.
        /// </summary>
        /// <param name="overallDifficulty">Overall Difficulty (0-10)</param>
        public HitJudge(float overallDifficulty)
        {
            OverallDifficulty = overallDifficulty;
            CalculateHitWindows();
        }

        /// <summary>
        /// Calcula las ventanas de timing según OD (fórmulas de osu!lazer).
        /// Ventanas:
        /// - MAX: 16ms (fijo)
        /// - 300: 64 - 3 * OD (clamp 16..64)
        /// - 200: 97 - 3 * OD (clamp 34..97)
        /// - 100: 127 - 3 * OD (clamp 46..127)
        /// - 50:  151 - 3 * OD (clamp 46..151)
        /// - MISS: 188ms (fijo)
        /// </summary>
        private void CalculateHitWindows()
        {
            float od = OverallDifficulty;

            // Fórmulas exactas de osu!lazer
            PerfectWindow = Math.Clamp(64.0 - 3.0 * od, 16.0, 64.0);
            GreatWindow = Math.Clamp(97.0 - 3.0 * od, 34.0, 97.0);
            OkWindow = Math.Clamp(127.0 - 3.0 * od, 46.0, 127.0);
            MehWindow = Math.Clamp(151.0 - 3.0 * od, 46.0, 151.0);
        }

        /// <summary>
        /// Juzga un hit basado en la diferencia de timing (hit error).
        /// </summary>
        /// <param name="hitError">Diferencia de timing en ms (positivo = tarde, negativo = temprano)</param>
        /// <returns>Juicio correspondiente</returns>
        public Judgement Judge(double hitError)
        {
            double absError = Math.Abs(hitError);

            // Evaluar de más estricto a menos estricto
            if (absError <= MaxWindow)
                return Judgement.Max;

            if (absError <= PerfectWindow)
                return Judgement.Perfect;

            if (absError <= GreatWindow)
                return Judgement.Great;

            if (absError <= OkWindow)
                return Judgement.Ok;

            if (absError <= MehWindow)
                return Judgement.Meh;

            // Fuera de todas las ventanas
            return Judgement.Miss;
        }

        /// <summary>
        /// Juzga una nota basada en el timestamp del hit y el timestamp de la nota.
        /// </summary>
        /// <param name="hitTimestamp">Timestamp cuando el jugador presionó la tecla (ms)</param>
        /// <param name="noteTimestamp">Timestamp de la nota (ms)</param>
        /// <returns>Juicio correspondiente</returns>
        public Judgement JudgeNote(double hitTimestamp, double noteTimestamp)
        {
            double hitError = hitTimestamp - noteTimestamp;
            return Judge(hitError);
        }

        /// <summary>
        /// Juzga el release de una long note (tail).
        /// El juicio de tail tiene restricciones:
        /// - Si se suelta antes de tiempo (early release), el mejor juicio posible es 200 (Great).
        /// - Si la head fue miss, el tail también es miss automáticamente.
        /// </summary>
        /// <param name="releaseTimestamp">Timestamp cuando el jugador soltó la tecla (ms)</param>
        /// <param name="tailTimestamp">Timestamp del tail de la nota (ms)</param>
        /// <param name="headJudgement">Juicio que recibió la head</param>
        /// <returns>Juicio correspondiente al tail</returns>
        public Judgement JudgeTail(double releaseTimestamp, double tailTimestamp, Judgement headJudgement)
        {
            // Si la head fue miss, el tail es miss automático
            if (headJudgement == Judgement.Miss)
                return Judgement.Miss;

            double hitError = releaseTimestamp - tailTimestamp;

            // Early release (antes de 50ms del tail) = cap en 200 (Great)
            if (hitError < -50.0)
                return Judgement.Great;

            // Juzgar normalmente
            Judgement judgement = Judge(hitError);

            // Cap en Great si fue early release cercano
            if (hitError < 0 && judgement > Judgement.Great)
                return Judgement.Great;

            return judgement;
        }

        /// <summary>
        /// Obtiene el valor numérico de un juicio para cálculos de score/accuracy.
        /// </summary>
        /// <param name="judgement">Juicio</param>
        /// <returns>Valor numérico (MAX=320, Perfect=300, Great=200, OK=100, Meh=50, Miss=0)</returns>
        public static int GetJudgementValue(Judgement judgement)
        {
            return judgement switch
            {
                Judgement.Max => 320,
                Judgement.Perfect => 300,
                Judgement.Great => 200,
                Judgement.Ok => 100,
                Judgement.Meh => 50,
                Judgement.Miss => 0,
                _ => 0
            };
        }

        /// <summary>
        /// Obtiene el nombre legible de un juicio.
        /// </summary>
        public static string GetJudgementName(Judgement judgement)
        {
            return judgement switch
            {
                Judgement.Max => "MAX",
                Judgement.Perfect => "300",
                Judgement.Great => "200",
                Judgement.Ok => "100",
                Judgement.Meh => "50",
                Judgement.Miss => "MISS",
                _ => "?"
            };
        }
    }
}
