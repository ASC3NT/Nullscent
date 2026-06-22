#nullable enable

namespace Nullscent.Beatmap
{
    /// <summary>
    /// Representa un objeto de golpe (nota) en un beatmap de osu!mania.
    /// Soporta notas normales (tap) y notas largas (long notes/holds).
    /// </summary>
    public class HitObject
    {
        /// <summary>
        /// Posición X del objeto (0-511), usado para determinar la columna.
        /// </summary>
        public int X { get; set; }

        /// <summary>
        /// Posición Y del objeto (no usado en mania, pero presente en el formato).
        /// </summary>
        public int Y { get; set; }

        /// <summary>
        /// Tiempo de inicio del objeto en milisegundos desde el inicio de la canción.
        /// </summary>
        public int Time { get; set; }

        /// <summary>
        /// Tipo de objeto. Bit 0 (& 1) = nota normal, Bit 7 (& 128) = nota larga.
        /// </summary>
        public int Type { get; set; }

        /// <summary>
        /// Sonido de golpe (hitsound) asociado con este objeto.
        /// 0 = Normal, 2 = Whistle, 4 = Finish, 8 = Clap.
        /// </summary>
        public int HitSound { get; set; }

        /// <summary>
        /// Tiempo de finalización para notas largas (LN) en milisegundos.
        /// Es igual a Time para notas normales.
        /// </summary>
        public int EndTime { get; set; }

        /// <summary>
        /// Índice de la columna calculada (0-based) según la fórmula osu!mania.
        /// Calculado como: floor(x * keyCount / 512)
        /// </summary>
        public int Column { get; set; }

        /// <summary>
        /// Velocidad de scroll efectiva en este objeto (heredada del último inherited timing point).
        /// 1.0 = velocidad base. Valores mayores = más rápido, menores = más lento.
        /// </summary>
        public double ScrollVelocity { get; set; } = 1.0;

        /// <summary>
        /// Archivo de sample adicional especificado en el objeto (opcional).
        /// Formato: sampleSet:additionSet:customIndex:sampleVolume:filename
        /// </summary>
        public string? SampleFileName { get; set; }

        /// <summary>
        /// Indica si este objeto es una nota larga (hold/LN).
        /// </summary>
        public bool IsLongNote => (Type & 128) != 0;

        /// <summary>
        /// Indica si este objeto es una nota normal (tap).
        /// </summary>
        public bool IsNormalNote => (Type & 1) != 0;

        /// <summary>
        /// Duración de la nota larga en milisegundos (0 para notas normales).
        /// </summary>
        public int Duration => IsLongNote ? EndTime - Time : 0;

        /// <summary>
        /// Indica si esta nota ya ha sido juzgada (head) durante el gameplay.
        /// </summary>
        public bool IsJudged { get; set; }

        /// <summary>
        /// Indica si la cola (tail) de una LN ha sido juzgada.
        /// </summary>
        public bool IsTailJudged { get; set; }

        /// <summary>
        /// Timestamp en ms cuando se presionó la tecla para esta nota (usado para cálculo de timing).
        /// </summary>
        public double? HitTimestamp { get; set; }

        /// <summary>
        /// Timestamp en ms cuando se soltó la tecla para la cola de una LN.
        /// </summary>
        public double? ReleaseTimestamp { get; set; }
    }
}
