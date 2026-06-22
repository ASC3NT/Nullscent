#nullable enable

using System;
using System.Collections.Generic;

namespace Nullscent.Gameplay
{
    /// <summary>
    /// Motor de scoring que implementa ScoreV2 de osu!lazer.
    /// Calcula score, accuracy, combo y mantiene estadísticas de juicios.
    /// </summary>
    public class ScoreEngine
    {
        /// <summary>
        /// Score total actual.
        /// </summary>
        public long Score { get; private set; }

        /// <summary>
        /// Combo actual.
        /// </summary>
        public int Combo { get; private set; }

        /// <summary>
        /// Combo máximo alcanzado en esta partida.
        /// </summary>
        public int MaxCombo { get; private set; }

        /// <summary>
        /// Número total de objetos (notas + tails de LN).
        /// </summary>
        public int TotalObjects { get; private set; }

        /// <summary>
        /// Número máximo de combo posible (igual a TotalObjects).
        /// </summary>
        public int MaxPossibleCombo { get; private set; }

        /// <summary>
        /// Contadores de juicios por tipo.
        /// </summary>
        public Dictionary<Judgement, int> JudgementCounts { get; private set; } = new()
        {
            { Judgement.Max, 0 },
            { Judgement.Perfect, 0 },
            { Judgement.Great, 0 },
            { Judgement.Ok, 0 },
            { Judgement.Meh, 0 },
            { Judgement.Miss, 0 }
        };

        /// <summary>
        /// Accuracy actual (0.0 - 1.0).
        /// Calculado como: sum(judgementValues) / (totalObjects * 320)
        /// </summary>
        public double Accuracy
        {
            get
            {
                if (TotalObjects == 0) return 0.0;

                int totalValue = 0;
                foreach (var kvp in JudgementCounts)
                    totalValue += HitJudge.GetJudgementValue(kvp.Key) * kvp.Value;

                return (double)totalValue / (TotalObjects * 320.0);
            }
        }

        /// <summary>
        /// Accuracy en porcentaje (0-100).
        /// </summary>
        public double AccuracyPercent => Accuracy * 100.0;

        /// <summary>
        /// Grade actual basado en accuracy (SS/S/A/B/C/D).
        /// </summary>
        public string Grade
        {
            get
            {
                double acc = AccuracyPercent;

                // SS = 100%
                if (acc >= 100.0)
                    return "SS";

                // S = 95%+
                if (acc >= 95.0)
                    return "S";

                // A = 90%+
                if (acc >= 90.0)
                    return "A";

                // B = 80%+
                if (acc >= 80.0)
                    return "B";

                // C = 70%+
                if (acc >= 70.0)
                    return "C";

                // D = menos de 70%
                return "D";
            }
        }

        /// <summary>
        /// Inicializa el score engine con el número total de objetos.
        /// </summary>
        /// <param name="totalNotes">Total de notas (sin contar tails por separado)</param>
        /// <param name="totalLongNotes">Total de long notes (cada LN cuenta como 2 objetos: head + tail)</param>
        public void Initialize(int totalNotes, int totalLongNotes)
        {
            // Cada nota normal = 1 objeto
            // Cada LN = 2 objetos (head + tail)
            TotalObjects = totalNotes + totalLongNotes;
            MaxPossibleCombo = TotalObjects;

            Reset();
        }

        /// <summary>
        /// Resetea el score y estadísticas (para retry).
        /// </summary>
        public void Reset()
        {
            Score = 0;
            Combo = 0;
            MaxCombo = 0;

            JudgementCounts[Judgement.Max] = 0;
            JudgementCounts[Judgement.Perfect] = 0;
            JudgementCounts[Judgement.Great] = 0;
            JudgementCounts[Judgement.Ok] = 0;
            JudgementCounts[Judgement.Meh] = 0;
            JudgementCounts[Judgement.Miss] = 0;
        }

        /// <summary>
        /// Registra un juicio y actualiza score, combo y estadísticas.
        /// Implementa ScoreV2: score += judgementValue * (1 + combo * 0.5 / maxCombo)
        /// </summary>
        /// <param name="judgement">Juicio obtenido</param>
        public void AddJudgement(Judgement judgement)
        {
            // Incrementar contador de juicios
            JudgementCounts[judgement]++;

            // Obtener valor del juicio
            int judgementValue = HitJudge.GetJudgementValue(judgement);

            // Actualizar combo
            if (judgement == Judgement.Miss)
            {
                // Miss rompe el combo
                Combo = 0;
            }
            else
            {
                // Cualquier hit incrementa el combo
                Combo++;
                if (Combo > MaxCombo)
                    MaxCombo = Combo;
            }

            // Calcular score con fórmula ScoreV2
            // score += judgementValue * (1 + combo * 0.5 / maxCombo)
            double comboMultiplier = MaxPossibleCombo > 0 
                ? 1.0 + (Combo * 0.5 / MaxPossibleCombo) 
                : 1.0;

            long scoreGain = (long)(judgementValue * comboMultiplier);
            Score += scoreGain;
        }

        /// <summary>
        /// Obtiene el número total de hits (excluyendo misses).
        /// </summary>
        public int TotalHits =>
            JudgementCounts[Judgement.Max] +
            JudgementCounts[Judgement.Perfect] +
            JudgementCounts[Judgement.Great] +
            JudgementCounts[Judgement.Ok] +
            JudgementCounts[Judgement.Meh];

        /// <summary>
        /// Obtiene el número de objetos juzgados (hits + misses).
        /// </summary>
        public int JudgedObjects => TotalHits + JudgementCounts[Judgement.Miss];

        /// <summary>
        /// Indica si la partida ha terminado (todos los objetos juzgados).
        /// </summary>
        public bool IsComplete => JudgedObjects >= TotalObjects;

        /// <summary>
        /// Obtiene un resumen de la partida en formato legible.
        /// </summary>
        public string GetScoreSummary()
        {
            return $"Score: {Score:N0}\n" +
                   $"Accuracy: {AccuracyPercent:F2}%\n" +
                   $"Max Combo: {MaxCombo}/{MaxPossibleCombo}\n" +
                   $"Grade: {Grade}\n" +
                   $"---\n" +
                   $"MAX: {JudgementCounts[Judgement.Max]}\n" +
                   $"300: {JudgementCounts[Judgement.Perfect]}\n" +
                   $"200: {JudgementCounts[Judgement.Great]}\n" +
                   $"100: {JudgementCounts[Judgement.Ok]}\n" +
                   $"50:  {JudgementCounts[Judgement.Meh]}\n" +
                   $"MISS: {JudgementCounts[Judgement.Miss]}";
        }
    }
}
