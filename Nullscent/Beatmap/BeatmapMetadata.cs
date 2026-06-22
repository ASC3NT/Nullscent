#nullable enable

namespace Nullscent.Beatmap
{
    /// <summary>
    /// Contiene metadatos de un beatmap (información de [General] y [Metadata]).
    /// </summary>
    public class BeatmapMetadata
    {
        /// <summary>
        /// Título de la canción (romanizado).
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Título de la canción en Unicode (puede ser igual a Title si no hay versión Unicode).
        /// </summary>
        public string TitleUnicode { get; set; } = string.Empty;

        /// <summary>
        /// Artista de la canción (romanizado).
        /// </summary>
        public string Artist { get; set; } = string.Empty;

        /// <summary>
        /// Artista de la canción en Unicode.
        /// </summary>
        public string ArtistUnicode { get; set; } = string.Empty;

        /// <summary>
        /// Creador del beatmap (mapper).
        /// </summary>
        public string Creator { get; set; } = string.Empty;

        /// <summary>
        /// Nombre de la dificultad (por ejemplo: "Easy", "Hard", "Insane").
        /// </summary>
        public string Version { get; set; } = string.Empty;

        /// <summary>
        /// ID del beatmap set (grupo de dificultades de la misma canción).
        /// </summary>
        public int BeatmapSetID { get; set; }

        /// <summary>
        /// ID único de este beatmap específico.
        /// </summary>
        public int BeatmapID { get; set; }

        /// <summary>
        /// Nombre del archivo de audio (relativo a la carpeta del beatmap).
        /// </summary>
        public string AudioFilename { get; set; } = string.Empty;

        /// <summary>
        /// Lead-in de audio en milisegundos (silencio agregado antes del primer objeto).
        /// </summary>
        public int AudioLeadIn { get; set; }

        /// <summary>
        /// Tiempo de preview en milisegundos (para song select).
        /// </summary>
        public int PreviewTime { get; set; } = -1;

        /// <summary>
        /// Modo de juego (0 = osu!, 1 = Taiko, 2 = Catch, 3 = Mania).
        /// Solo soportamos Mode = 3.
        /// </summary>
        public int Mode { get; set; }

        /// <summary>
        /// Source/origen de la canción (anime, juego, etc.) - opcional.
        /// </summary>
        public string Source { get; set; } = string.Empty;

        /// <summary>
        /// Tags de búsqueda separados por espacios.
        /// </summary>
        public string Tags { get; set; } = string.Empty;
    }
}
