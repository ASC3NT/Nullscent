#nullable enable

namespace Nullscent.Beatmap
{
    /// <summary>
    /// Representa un timing point en un beatmap de osu!.
    /// Puede ser heredado (inherited) para cambios de SV, o no heredado (uninherited) para cambios de BPM.
    /// </summary>
    public class TimingPoint
    {
        /// <summary>
        /// Offset en milisegundos desde el inicio de la canción donde este timing point toma efecto.
        /// </summary>
        public double Offset { get; set; }

        /// <summary>
        /// Valor de beatLength del formato .osu.
        /// Si Uninherited = true: duración de un beat en ms (60000 / BPM).
        /// Si Uninherited = false: valor negativo que indica el multiplicador de SV (-100 / beatLength).
        /// </summary>
        public double BeatLength { get; set; }

        /// <summary>
        /// Numerador de la métrica (por ejemplo, 4 en 4/4).
        /// </summary>
        public int Meter { get; set; }

        /// <summary>
        /// Set de samples usado (0 = Auto, 1 = Normal, 2 = Soft, 3 = Drum).
        /// </summary>
        public int SampleSet { get; set; }

        /// <summary>
        /// Índice de sample customizado (0 = defecto).
        /// </summary>
        public int SampleIndex { get; set; }

        /// <summary>
        /// Volumen de los hitsounds (0-100).
        /// </summary>
        public int Volume { get; set; }

        /// <summary>
        /// Indica si este timing point no es heredado (uninherited = 1 → BPM point).
        /// Si false, es un inherited point (cambio de SV o efectos).
        /// </summary>
        public bool Uninherited { get; set; }

        /// <summary>
        /// Efectos especiales (bit flags):
        /// Bit 0 = Kiai time
        /// Bit 3 = Omit first barline
        /// </summary>
        public int Effects { get; set; }

        /// <summary>
        /// BPM calculado para timing points no heredados.
        /// Calculado como: 60000 / BeatLength.
        /// </summary>
        public double BPM => Uninherited ? 60000.0 / BeatLength : 0.0;

        /// <summary>
        /// Multiplicador de velocidad de scroll para timing points heredados.
        /// Calculado como: -100 / BeatLength.
        /// 1.0 = velocidad normal. Valores mayores = más rápido.
        /// </summary>
        public double ScrollVelocityMultiplier => !Uninherited && BeatLength < 0 
            ? -100.0 / BeatLength 
            : 1.0;

        /// <summary>
        /// Indica si Kiai time está activo en este timing point.
        /// </summary>
        public bool IsKiai => (Effects & 1) != 0;
    }
}
