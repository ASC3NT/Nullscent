#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;

namespace Nullscent.UI
{
    /// <summary>
    /// Representa un set de beatmaps (grupo de dificultades de la misma canción).
    /// </summary>
    public class BeatmapSet
    {
        /// <summary>
        /// ID del beatmap set.
        /// </summary>
        public int BeatmapSetID { get; set; }

        /// <summary>
        /// Ruta al directorio del beatmap set.
        /// </summary>
        public string Directory { get; set; } = string.Empty;

        /// <summary>
        /// Título de la canción (del primer beatmap).
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Título Unicode.
        /// </summary>
        public string TitleUnicode { get; set; } = string.Empty;

        /// <summary>
        /// Artista (del primer beatmap).
        /// </summary>
        public string Artist { get; set; } = string.Empty;

        /// <summary>
        /// Artista Unicode.
        /// </summary>
        public string ArtistUnicode { get; set; } = string.Empty;

        /// <summary>
        /// Creator/Mapper.
        /// </summary>
        public string Creator { get; set; } = string.Empty;

        /// <summary>
        /// Lista de todas las dificultades en este set.
        /// </summary>
        public List<BeatmapDifficulty> Difficulties { get; set; } = new();

        /// <summary>
        /// Título para display (prioriza Unicode si está disponible).
        /// </summary>
        public string DisplayTitle => !string.IsNullOrWhiteSpace(TitleUnicode) ? TitleUnicode : Title;

        /// <summary>
        /// Artista para display.
        /// </summary>
        public string DisplayArtist => !string.IsNullOrWhiteSpace(ArtistUnicode) ? ArtistUnicode : Artist;

        /// <summary>
        /// String completo para display: "Artist - Title".
        /// </summary>
        public string FullDisplayName => $"{DisplayArtist} - {DisplayTitle}";

        // Caché de strings en minúsculas para optimizar búsquedas (evitar asignaciones de memoria repetitivas en cada frame/tecla)
        private string? _titleLower;
        public string TitleLower => _titleLower ??= Title.ToLowerInvariant();

        private string? _titleUnicodeLower;
        public string TitleUnicodeLower => _titleUnicodeLower ??= TitleUnicode.ToLowerInvariant();

        private string? _artistLower;
        public string ArtistLower => _artistLower ??= Artist.ToLowerInvariant();

        private string? _artistUnicodeLower;
        public string ArtistUnicodeLower => _artistUnicodeLower ??= ArtistUnicode.ToLowerInvariant();

        private string? _creatorLower;
        public string CreatorLower => _creatorLower ??= Creator.ToLowerInvariant();

        // Caché de la estrella máxima para no re-calcular con LINQ Max() cada frame
        private double? _maxStars;
        public double MaxStars => _maxStars ??= (Difficulties.Count > 0 ? Difficulties.Max(d => d.StarRating) : 0.0);
    }

    public class BeatmapDifficulty
    {
        /// <summary>
        /// Hash MD5 único del contenido del archivo .osu
        /// </summary>
        public string Hash { get; set; } = string.Empty;

        /// <summary>
        /// Ruta completa al archivo .osu.
        /// </summary>
        public string FilePath { get; set; } = string.Empty;

        /// <summary>
        /// Nombre de la dificultad (Version en .osu).
        /// </summary>
        public string DifficultyName { get; set; } = string.Empty;

        /// <summary>
        /// Key count (número de teclas).
        /// </summary>
        public int KeyCount { get; set; }

        /// <summary>
        /// Overall Difficulty.
        /// </summary>
        public float OverallDifficulty { get; set; }

        /// <summary>
        /// HP Drain Rate.
        /// </summary>
        public float HPDrainRate { get; set; }

        /// <summary>
        /// BPM más común del beatmap.
        /// </summary>
        public double BPM { get; set; }

        /// <summary>
        /// Duración total en milisegundos.
        /// </summary>
        public int Length { get; set; }

        /// <summary>
        /// Número de objetos (notas).
        /// </summary>
        public int ObjectCount { get; set; }

        /// <summary>
        /// Beatmap ID único.
        /// </summary>
        public int BeatmapID { get; set; }

        /// <summary>
        /// Nombre del archivo de audio (.mp3, etc.).
        /// </summary>
        public string AudioFilename { get; set; } = string.Empty;

        /// <summary>
        /// Tiempo de previsualización en ms.
        /// </summary>
        public int PreviewTime { get; set; } = -1;

        // Caché de Star Rating para evitar Math.Pow recurrentes en cada frame
        private double? _cachedStarRating;

        /// <summary>
        /// Estimación de dificultad en estrellas (Star Rating ★).
        /// </summary>
        public double StarRating
        {
            get
            {
                if (_cachedStarRating.HasValue) return _cachedStarRating.Value;
                if (Length <= 0 || ObjectCount <= 0) return 0.0;
                double seconds = Length / 1000.0;
                double density = ObjectCount / seconds;

                // Power-based scaling for realistic star ratings (curves fitted to osu!mania standards)
                double stars = Math.Pow(density, 0.65) * 0.65 
                    * Math.Pow(BPM / 150.0, 0.4) 
                    * Math.Pow(4.0 / KeyCount, 0.3);

                _cachedStarRating = Math.Round(Math.Clamp(stars, 0.5, 10.0), 2);
                return _cachedStarRating.Value;
            }
        }
    }

    /// <summary>
    /// Lista de beatmaps con funcionalidad de filtrado y búsqueda.
    /// </summary>
    public class SongList
    {
        private List<BeatmapSet> _allBeatmapSets = new();
        private List<BeatmapSet> _filteredBeatmapSets = new();
        private int _selectedSetIndex;
        private int _selectedDifficultyIndex;
        private string _searchQuery = string.Empty;

        /// <summary>
        /// Todos los beatmap sets cargados.
        /// </summary>
        public IReadOnlyList<BeatmapSet> AllBeatmapSets => _allBeatmapSets;

        /// <summary>
        /// Beatmap sets filtrados por búsqueda actual.
        /// </summary>
        public IReadOnlyList<BeatmapSet> FilteredBeatmapSets => _filteredBeatmapSets;

        /// <summary>
        /// Índice del set seleccionado actualmente.
        /// </summary>
        public int SelectedSetIndex
        {
            get => _selectedSetIndex;
            set => _selectedSetIndex = Math.Clamp(value, 0, Math.Max(0, _filteredBeatmapSets.Count - 1));
        }

        /// <summary>
        /// Índice de la dificultad seleccionada dentro del set actual.
        /// </summary>
        public int SelectedDifficultyIndex
        {
            get => _selectedDifficultyIndex;
            set
            {
                if (SelectedBeatmapSet != null)
                    _selectedDifficultyIndex = Math.Clamp(value, 0, Math.Max(0, SelectedBeatmapSet.Difficulties.Count - 1));
            }
        }

        /// <summary>
        /// Beatmap set seleccionado actualmente.
        /// </summary>
        public BeatmapSet? SelectedBeatmapSet =>
            _filteredBeatmapSets.Count > 0 && _selectedSetIndex < _filteredBeatmapSets.Count
                ? _filteredBeatmapSets[_selectedSetIndex]
                : null;

        /// <summary>
        /// Dificultad seleccionada actualmente.
        /// </summary>
        public BeatmapDifficulty? SelectedDifficulty
        {
            get
            {
                var set = SelectedBeatmapSet;
                if (set == null || set.Difficulties.Count == 0)
                    return null;

                return _selectedDifficultyIndex < set.Difficulties.Count
                    ? set.Difficulties[_selectedDifficultyIndex]
                    : null;
            }
        }

        /// <summary>
        /// Query de búsqueda actual.
        /// </summary>
        public string SearchQuery
        {
            get => _searchQuery;
            set
            {
                _searchQuery = value;
                ApplyFilter();
            }
        }

        /// <summary>
        /// Carga beatmap sets desde una lista.
        /// </summary>
        public void LoadBeatmapSets(List<BeatmapSet> sets)
        {
            _allBeatmapSets = sets;
            _filteredBeatmapSets = new List<BeatmapSet>(_allBeatmapSets);
            _selectedSetIndex = 0;
            _selectedDifficultyIndex = 0;

            Console.WriteLine($"[SongList] Loaded {_allBeatmapSets.Count} beatmap sets");
        }

        /// <summary>
        /// Aplica filtro de búsqueda.
        /// </summary>
        private void ApplyFilter()
        {
            if (string.IsNullOrWhiteSpace(_searchQuery))
            {
                _filteredBeatmapSets = new List<BeatmapSet>(_allBeatmapSets);
            }
            else
            {
                string query = _searchQuery.ToLowerInvariant();
                _filteredBeatmapSets = _allBeatmapSets
                    .Where(set =>
                        set.TitleLower.Contains(query) ||
                        set.TitleUnicodeLower.Contains(query) ||
                        set.ArtistLower.Contains(query) ||
                        set.ArtistUnicodeLower.Contains(query) ||
                        set.CreatorLower.Contains(query))
                    .ToList();
            }

            // Resetear selección si está fuera de rango
            _selectedSetIndex = Math.Clamp(_selectedSetIndex, 0, Math.Max(0, _filteredBeatmapSets.Count - 1));
        }

        /// <summary>
        /// Navega al siguiente beatmap set.
        /// </summary>
        public void SelectNext()
        {
            if (_filteredBeatmapSets.Count > 0)
            {
                SelectedSetIndex = (_selectedSetIndex + 1) % _filteredBeatmapSets.Count;
                _selectedDifficultyIndex = 0; // Resetear dificultad al cambiar de set
            }
        }

        /// <summary>
        /// Navega al beatmap set anterior.
        /// </summary>
        public void SelectPrevious()
        {
            if (_filteredBeatmapSets.Count > 0)
            {
                SelectedSetIndex = (_selectedSetIndex - 1 + _filteredBeatmapSets.Count) % _filteredBeatmapSets.Count;
                _selectedDifficultyIndex = 0;
            }
        }

        /// <summary>
        /// Navega a la siguiente dificultad dentro del set actual.
        /// </summary>
        public void SelectNextDifficulty()
        {
            var set = SelectedBeatmapSet;
            if (set != null && set.Difficulties.Count > 0)
            {
                SelectedDifficultyIndex = (_selectedDifficultyIndex + 1) % set.Difficulties.Count;
            }
        }

        /// <summary>
        /// Navega a la dificultad anterior dentro del set actual.
        /// </summary>
        public void SelectPreviousDifficulty()
        {
            var set = SelectedBeatmapSet;
            if (set != null && set.Difficulties.Count > 0)
            {
                SelectedDifficultyIndex = (_selectedDifficultyIndex - 1 + set.Difficulties.Count) % set.Difficulties.Count;
            }
        }

        /// <summary>
        /// Intenta seleccionar un beatmap específico por su ruta de archivo (.osu).
        /// </summary>
        public bool TrySelectDifficultyByPath(string filePath)
        {
            for (int setIdx = 0; setIdx < _filteredBeatmapSets.Count; setIdx++)
            {
                var set = _filteredBeatmapSets[setIdx];
                for (int diffIdx = 0; diffIdx < set.Difficulties.Count; diffIdx++)
                {
                    if (string.Equals(set.Difficulties[diffIdx].FilePath, filePath, StringComparison.OrdinalIgnoreCase))
                    {
                        _selectedSetIndex = setIdx;
                        _selectedDifficultyIndex = diffIdx;
                        return true;
                    }
                }
            }
            return false;
        }
    }
}
