#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Nullscent.Beatmap;

namespace Nullscent.Gameplay
{
    /// <summary>
    /// Representa una columna individual en el gameplay de osu!mania.
    /// Gestiona las notas de esa columna, input handling, y juicio de hits.
    /// </summary>
    public class Column
    {
        /// <summary>
        /// Índice de esta columna (0-based).
        /// </summary>
        public int Index { get; }

        /// <summary>
        /// Ancho de la columna en píxeles.
        /// </summary>
        public float Width { get; set; }

        /// <summary>
        /// Posición X de esta columna en pantalla.
        /// </summary>
        public float X { get; set; }

        /// <summary>
        /// Lista de notas asignadas a esta columna.
        /// </summary>
        public List<HitObject> Notes { get; set; } = new();

        /// <summary>
        /// Índice de la próxima nota no resuelta (optimización para evitar buscar desde el inicio).
        /// </summary>
        private int _nextUnresolvedNoteIndex = 0;

        /// <summary>
        /// Nota larga (LN) actualmente siendo presionada (null si no hay).
        /// </summary>
        private HitObject? _currentHoldNote;

        /// <summary>
        /// Indica si la tecla de esta columna está presionada actualmente.
        /// </summary>
        public bool IsPressed { get; private set; }

        /// <summary>
        /// Judge system usado para evaluar timing.
        /// </summary>
        private readonly HitJudge _hitJudge;

        /// <summary>
        /// Score engine para registrar juicios.
        /// </summary>
        private readonly ScoreEngine _scoreEngine;

        /// <summary>
        /// Health bar para aplicar cambios de HP.
        /// </summary>
        private readonly HealthBar _healthBar;

        /// <summary>
        /// Callback cuando se juzga una nota (para efectos visuales/sonoros).
        /// Parámetros: (judgement, hitObject)
        /// </summary>
        public event Action<Judgement, HitObject>? OnNoteJudged;

        public Column(int index, HitJudge hitJudge, ScoreEngine scoreEngine, HealthBar healthBar)
        {
            Index = index;
            _hitJudge = hitJudge;
            _scoreEngine = scoreEngine;
            _healthBar = healthBar;
        }

        /// <summary>
        /// Maneja el evento de tecla presionada (KeyDown).
        /// </summary>
        /// <param name="currentTimeMs">Tiempo actual del audio en ms</param>
        public void OnKeyDown(double currentTimeMs)
        {
            IsPressed = true;

            // Buscar la nota más cercana dentro de la miss window
            HitObject? targetNote = FindNearestUnresolvedNote(currentTimeMs);

            if (targetNote == null)
                return;

            // Calcular hit error
            double hitError = currentTimeMs - targetNote.Time;

            // Verificar si está dentro de la miss window
            if (Math.Abs(hitError) <= HitJudge.MissWindow)
            {
                // Juzgar la nota (head)
                Judgement judgement = _hitJudge.JudgeNote(currentTimeMs, targetNote.Time);

                targetNote.IsJudged = true;
                targetNote.HitTimestamp = currentTimeMs;

                // Registrar juicio
                _scoreEngine.AddJudgement(judgement);
                _healthBar.ApplyJudgement(judgement);

                // Notificar
                OnNoteJudged?.Invoke(judgement, targetNote);

                // Si es LN, iniciar tracking del hold
                if (targetNote.IsLongNote && judgement != Judgement.Miss)
                {
                    _currentHoldNote = targetNote;
                }

                // Avanzar índice de búsqueda
                _nextUnresolvedNoteIndex++;
            }
        }

        /// <summary>
        /// Maneja el evento de tecla soltada (KeyUp).
        /// </summary>
        /// <param name="currentTimeMs">Tiempo actual del audio en ms</param>
        public void OnKeyUp(double currentTimeMs)
        {
            IsPressed = false;

            // Si estamos holding una LN, juzgar el tail
            if (_currentHoldNote != null && !_currentHoldNote.IsTailJudged)
            {
                _currentHoldNote.ReleaseTimestamp = currentTimeMs;

                // Obtener el juicio de la head
                Judgement headJudgement = _hitJudge.JudgeNote(
                    _currentHoldNote.HitTimestamp ?? _currentHoldNote.Time, 
                    _currentHoldNote.Time
                );

                // Juzgar el tail
                Judgement tailJudgement = _hitJudge.JudgeTail(
                    currentTimeMs, 
                    _currentHoldNote.EndTime, 
                    headJudgement
                );

                _currentHoldNote.IsTailJudged = true;

                // Registrar juicio del tail
                _scoreEngine.AddJudgement(tailJudgement);
                _healthBar.ApplyJudgement(tailJudgement);

                // Notificar (podemos usar el mismo evento, el caller diferencia por IsTailJudged)
                OnNoteJudged?.Invoke(tailJudgement, _currentHoldNote);

                _currentHoldNote = null;
            }
        }

        /// <summary>
        /// Actualiza el estado de la columna (auto-miss de notas pasadas).
        /// </summary>
        /// <param name="currentTimeMs">Tiempo actual del audio en ms</param>
        public void Update(double currentTimeMs)
        {
            // Auto-miss de notas que pasaron la miss window sin ser golpeadas
            while (_nextUnresolvedNoteIndex < Notes.Count)
            {
                HitObject note = Notes[_nextUnresolvedNoteIndex];

                if (note.IsJudged)
                {
                    _nextUnresolvedNoteIndex++;
                    continue;
                }

                // Si la nota está más allá de la miss window, auto-miss
                double timeDiff = currentTimeMs - note.Time;
                if (timeDiff > HitJudge.MissWindow)
                {
                    note.IsJudged = true;
                    _scoreEngine.AddJudgement(Judgement.Miss);
                    _healthBar.ApplyJudgement(Judgement.Miss);
                    OnNoteJudged?.Invoke(Judgement.Miss, note);

                    // Si era LN, también auto-miss del tail
                    if (note.IsLongNote && !note.IsTailJudged)
                    {
                        note.IsTailJudged = true;
                        _scoreEngine.AddJudgement(Judgement.Miss);
                        _healthBar.ApplyJudgement(Judgement.Miss);
                    }

                    _nextUnresolvedNoteIndex++;
                }
                else
                {
                    break; // Notas futuras aún no han llegado
                }
            }
        }

        /// <summary>
        /// Encuentra la nota no resuelta más cercana al tiempo actual dentro de la miss window.
        /// </summary>
        private HitObject? FindNearestUnresolvedNote(double currentTimeMs)
        {
            for (int i = _nextUnresolvedNoteIndex; i < Notes.Count; i++)
            {
                HitObject note = Notes[i];

                if (note.IsJudged)
                    continue;

                double timeDiff = Math.Abs(currentTimeMs - note.Time);
                if (timeDiff <= HitJudge.MissWindow)
                    return note;

                // Si la nota está muy en el futuro, parar búsqueda
                if (note.Time - currentTimeMs > HitJudge.MissWindow)
                    break;
            }

            return null;
        }

        /// <summary>
        /// Resetea el estado de la columna (para retry).
        /// </summary>
        public void Reset()
        {
            IsPressed = false;
            _currentHoldNote = null;
            _nextUnresolvedNoteIndex = 0;

            foreach (var note in Notes)
            {
                note.IsJudged = false;
                note.IsTailJudged = false;
                note.HitTimestamp = null;
                note.ReleaseTimestamp = null;
            }
        }

        /// <summary>
        /// Obtiene todas las notas visibles en el rango de tiempo especificado.
        /// </summary>
        public IEnumerable<HitObject> GetVisibleNotes(double currentTimeMs, double visibleRangeMs)
        {
            return Notes.Where(note =>
                note.Time >= currentTimeMs - 100 && // Pequeño buffer detrás del receptor
                note.Time <= currentTimeMs + visibleRangeMs
            );
        }
    }
}
