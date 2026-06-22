#nullable enable

using System;
using System.Collections.Generic;

namespace Nullscent.Beatmap
{
    /// <summary>
    /// Representa un beatmap completo de osu!mania cargado desde un archivo .osu.
    /// Contiene toda la información necesaria para el gameplay: metadatos, dificultad, timing points y objetos.
    /// </summary>
    public class Beatmap
    {
        /// <summary>
        /// Metadatos del beatmap (título, artista, creator, audio, etc.).
        /// </summary>
        public BeatmapMetadata Metadata { get; set; } = new();

        /// <summary>
        /// Tasa de drenaje de HP (0-10). Controla qué tan rápido se drena la salud.
        /// </summary>
        public float HPDrainRate { get; set; }

        /// <summary>
        /// Circle Size en el formato .osu. Para mania, determina el número de teclas (key count).
        /// Key count = Math.Round(CircleSize).
        /// Rango: 1-18 teclas.
        /// </summary>
        public float CircleSize { get; set; }

        /// <summary>
        /// Overall Difficulty (OD). Controla las ventanas de timing para juicios (300/200/100/50).
        /// OD más alto = ventanas de timing más estrictas.
        /// Rango típico: 0-10.
        /// </summary>
        public float OverallDifficulty { get; set; }

        /// <summary>
        /// Slider multiplier (no usado en mania, pero presente en el formato).
        /// </summary>
        public float SliderMultiplier { get; set; } = 1.4f;

        /// <summary>
        /// Slider tick rate (no usado en mania, pero presente en el formato).
        /// </summary>
        public float SliderTickRate { get; set; } = 1.0f;

        /// <summary>
        /// Número de teclas calculado desde CircleSize.
        /// Key count = Math.Round(CircleSize).
        /// </summary>
        public int KeyCount => (int)Math.Round(CircleSize);

        /// <summary>
        /// Lista de todos los timing points (heredados y no heredados), ordenados por offset ascendente.
        /// Timing points no heredados definen BPM.
        /// Timing points heredados definen cambios de SV y efectos (kiai, etc.).
        /// </summary>
        public List<TimingPoint> TimingPoints { get; set; } = new();

        /// <summary>
        /// Lista de todos los objetos de golpe (notas) en el beatmap, ordenados por tiempo ascendente.
        /// </summary>
        public List<HitObject> HitObjects { get; set; } = new();

        /// <summary>
        /// Ruta completa al archivo .osu original.
        /// </summary>
        public string FilePath { get; set; } = string.Empty;

        /// <summary>
        /// Directorio que contiene este beatmap (para resolver rutas relativas de audio, skin, etc.).
        /// </summary>
        public string Directory { get; set; } = string.Empty;

        /// <summary>
        /// Duración total de la canción en milisegundos (tiempo del último objeto + ventana de miss).
        /// </summary>
        public int TotalLength => HitObjects.Count > 0 ? HitObjects[^1].EndTime + 188 : 0;

        /// <summary>
        /// Obtiene el BPM más común del beatmap (para display en UI).
        /// Calcula el promedio ponderado por duración de cada sección de timing.
        /// </summary>
        public double MostCommonBPM
        {
            get
            {
                if (TimingPoints.Count == 0) return 0.0;

                var bpmPoints = TimingPoints.FindAll(tp => tp.Uninherited);
                if (bpmPoints.Count == 0) return 0.0;

                // Si solo hay un BPM, retornarlo
                if (bpmPoints.Count == 1) return bpmPoints[0].BPM;

                // Calcular BPM ponderado por duración
                double totalDuration = 0;
                double weightedBPM = 0;

                for (int i = 0; i < bpmPoints.Count; i++)
                {
                    double duration;
                    if (i < bpmPoints.Count - 1)
                        duration = bpmPoints[i + 1].Offset - bpmPoints[i].Offset;
                    else
                        duration = TotalLength - bpmPoints[i].Offset;

                    if (duration > 0)
                    {
                        totalDuration += duration;
                        weightedBPM += bpmPoints[i].BPM * duration;
                    }
                }

                return totalDuration > 0 ? weightedBPM / totalDuration : bpmPoints[0].BPM;
            }
        }

        /// <summary>
        /// Obtiene el último timing point no heredado (BPM point) en o antes del tiempo especificado.
        /// </summary>
        /// <param name="time">Tiempo en milisegundos.</param>
        /// <returns>Timing point no heredado, o null si no hay ninguno antes del tiempo dado.</returns>
        public TimingPoint? GetTimingPointAt(double time)
        {
            TimingPoint? result = null;
            foreach (var tp in TimingPoints)
            {
                if (tp.Uninherited && tp.Offset <= time)
                    result = tp;
                else if (tp.Offset > time)
                    break;
            }
            return result;
        }

        /// <summary>
        /// Obtiene el último timing point heredado (SV point) en o antes del tiempo especificado.
        /// </summary>
        /// <param name="time">Tiempo en milisegundos.</param>
        /// <returns>Timing point heredado, o null si no hay ninguno antes del tiempo dado.</returns>
        public TimingPoint? GetInheritedTimingPointAt(double time)
        {
            TimingPoint? result = null;
            foreach (var tp in TimingPoints)
            {
                if (!tp.Uninherited && tp.Offset <= time)
                    result = tp;
                else if (tp.Offset > time)
                    break;
            }
            return result;
        }

        /// <summary>
        /// Obtiene la velocidad de scroll efectiva en un tiempo dado.
        /// Combina el último inherited timing point (SV multiplier) con la velocidad base.
        /// </summary>
        /// <param name="time">Tiempo en milisegundos.</param>
        /// <returns>Multiplicador de SV (1.0 = velocidad base).</returns>
        public double GetScrollVelocityAt(double time)
        {
            var inheritedPoint = GetInheritedTimingPointAt(time);
            return inheritedPoint?.ScrollVelocityMultiplier ?? 1.0;
        }
    }
}
