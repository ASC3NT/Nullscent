# 🎯 Plan de Implementación Completo - Nullscent osu!mania Client
## Basado 100% en arquitectura de ppy/osu

---

## 📋 FASE 1: Core Gameplay Systems (PRIORIDAD MÁXIMA)
**Duración estimada: 3-5 días**
**Estado actual: Parcialmente implementado**

### 1.1 Hit Object System ✅ (Completado pero mejorable)
**Referencia: `osu.Game.Rulesets.Mania/Objects/`**

#### Archivos a actualizar/crear:
- ✅ `ManiaHitObject.cs` - Base class
- ✅ `Note.cs` - Single tap note
- ✅ `HoldNote.cs` - LN con head, body, tail
- 🔄 `TailNote.cs` - Añadir para representar el tail de LN separadamente
- 🔄 `HeadNote.cs` - Añadir para representar el head de LN separadamente

#### Mejoras necesarias:
```csharp
// Agregar a ManiaHitObject:
- public double TimePreempt { get; set; } // Tiempo antes de aparecer
- public double TimeFadeIn { get; set; }  // Tiempo de fade in
- public List<HitSampleInfo> AuxiliarySamples { get; set; } // Samples adicionales
- public double StartTime { get; set; }
- public double GetEndTime() // Virtual para override en HoldNote
```

### 1.2 Judgement System ✅ (Completado pero mejorable)
**Referencia: `osu.Game.Rulesets.Mania/Judgements/`**

#### Mejoras necesarias:
```csharp
// ManiaJudgement.cs
- Agregar IsBonus property para bonus notes
- Agregar AffectsCombo property
- Mejorar HealthIncreaseFor() con valores exactos de osu!

// ManiaHitWindows.cs
- Implementar IsHitResultAllowed(HitResult)
- Agregar soporte para mods que modifican windows (HR/EZ)
```

### 1.3 Score Processor ✅ (Completado pero mejorable)
**Referencia: `osu.Game.Rulesets.Mania/Scoring/ManiaScoreProcessor.cs`**

#### Mejoras críticas:
```csharp
// Implementar ScoreV2 exacto de osu!:
- Base score: 1,000,000 points
- Bonus score: hasta 1,000,000 points adicionales
- Fórmula: Score = BaseScore × (1 + Combo/10)
- Accuracy calculation con pesos correctos:
  * Perfect (320): 100%
  * Great (300): 93.75%
  * Good (200): 62.5%
  * Ok (100): 31.25%
  * Meh (50): 15.625%
  * Miss (0): 0%
```

### 1.4 Playfield & Column System 🔄 (Rehecho pero mejorable)
**Referencia: `osu.Game.Rulesets.Mania/UI/`**

#### Mejoras necesarias:

**A. DrawableManiaHitObject pattern:**
```csharp
// Crear jerarquía de Drawables:
- DrawableManiaHitObject (base)
  - DrawableNote
  - DrawableHoldNote
    - DrawableHoldNoteHead
    - DrawableHoldNoteTail
    - DrawableHoldNoteBody

// Cada Drawable maneja su propio rendering y animaciones
```

**B. Column improvements:**
```csharp
// Column.cs
public class Column
{
    - HitObjectContainer (maneja el pooling de drawables)
    - Receptor (el hit line)
    - Background (fondo de columna)
    - HitExplosion (efecto al golpear)
    - KeyArea (área clickeable)

    // Métodos:
    - Add(ManiaHitObject) // Agregar objeto
    - CheckForResult(bool userTriggered, double timeOffset)
    - OnNewResult(HitResult result) // Callback de resultado
}
```

**C. Stage (conjunto de columnas):**
```csharp
// Stage.cs - Contenedor de columnas
public class Stage
{
    - List<Column> Columns
    - StageBackground
    - Barlines (líneas de compás)
    - SpecialColumnPosition (para stages con special column)

    // Soporte para dual stages (2P mode)
}
```

---

## 📋 FASE 2: Visual & UI Polish (PRIORIDAD ALTA)
**Duración estimada: 2-3 días**

### 2.1 Note Rendering & Skinning 🆕
**Referencia: `osu.Game.Rulesets.Mania/Skinning/`**

#### Implementar:

**A. Skin Components:**
```csharp
// ManiaSkinComponents.cs
public enum ManiaSkinComponents
{
    ColumnBackground,
    Note,
    HoldNoteHead,
    HoldNoteTail,
    HoldNoteBody,
    HitTarget, // Receptor
    KeyArea,
    HitExplosion,
    LightingN, LightingL, // Column lighting
    StageBackground,
    StageForeground,
}
```

**B. Skin.ini parsing completo:**
```ini
[Mania]
Keys: 4
ColumnStart: 136
ColumnRight: 19
ColumnSpacing: 0,0,0
ColumnWidth: 30,30,30,30
ColumnLineWidth: 2,2,2,2,2
BarlineHeight: 1.2
LightingNWidth: 0
LightingLWidth: 0
WidthForNoteHeightScale: 0
HitPosition: 402
LightPosition: 413
ScorePosition: 325
ComboPosition: 111
```

**C. Legacy skin transformer:**
```csharp
// LegacyManiaSkinTransformer.cs
- Convierte skin legacy a nuevo formato
- Maneja diferentes estilos (arrow, circle, bar)
- Auto-generación de colores si no están en skin
```

### 2.2 Visual Effects 🆕
**Referencia: `osu.Game.Rulesets.Mania/UI/`**

#### A. Hit Explosions:
```csharp
// DrawableHitExplosion.cs
- Animación al golpear nota
- Diferentes efectos por judgement:
  * Perfect: Explosión grande + partículas
  * Great: Explosión media
  * Good/Ok: Explosión pequeña
  * Miss: Sin explosión
```

#### B. Column Lighting:
```csharp
// ColumnHitObjectArea.cs
- Iluminación al presionar tecla
- Iluminación al golpear nota
- Fade out suave (150-200ms)
- Intensidad configurable
```

#### C. Judgement Display:
```csharp
// DrawableManiaJudgement.cs
- Texto del judgement (Perfect/Great/etc)
- Animación de entrada (scale up)
- Fade out y movimiento hacia arriba
- Posición configurable
```

### 2.3 HUD Improvements 🔄
**Referencia: `osu.Game.Rulesets.Mania/UI/ManiaHUD.cs`**

#### Implementar:
```csharp
// Components:
- ScoreDisplay (grande, animado)
- AccuracyDisplay (porcentaje con decimales)
- ComboDisplay (con animación al aumentar)
- HealthBar (horizontal o vertical)
- ProgressBar (circular o lineal)
- KeyOverlay (opcional, mostrar teclas presionadas)
- JudgementCounter (conteo de cada judgement)
- PPDisplay (performance points estimados)
```

---

## 📋 FASE 3: Advanced Features (PRIORIDAD MEDIA)
**Duración estimada: 4-5 días**

### 3.1 Mods System 🔄 (Parcialmente implementado)
**Referencia: `osu.Game.Rulesets.Mania/Mods/`**

#### A. Rate-Changing Mods:
```csharp
✅ DoubleTime (1.5x speed)
✅ HalfTime (0.75x speed)
🆕 Nightcore (1.5x + pitch shift + beat)
🆕 Daycore (0.75x - pitch shift)
```

#### B. Difficulty Mods:
```csharp
✅ HardRock (OD+, HP+)
✅ Easy (OD-, HP-)
🆕 FadeIn (notas aparecen gradualmente)
🆕 Hidden (notas desaparecen antes del receptor)
🆕 Flashlight (visibilidad limitada)
🆕 SuddenDeath (1 miss = fail)
🆕 Perfect (1 non-perfect = fail)
```

#### C. Conversion Mods:
```csharp
✅ Mirror (espejo horizontal)
✅ Random (randomizar columnas)
🆕 Alternate (alternar manos)
🆕 SingleTap (convertir streams en singles)
🆕 KeyN (cambiar keycount: 1K-10K)
```

#### D. Automation Mods:
```csharp
🆕 Autoplay (juega perfectamente)
🆕 Cinema (solo visualización)
🆕 Relax (auto-tap, solo timing)
```

### 3.2 Replay System 🆕
**Referencia: `osu.Game.Rulesets.Mania/Replays/`**

#### Implementar:
```csharp
// ManiaReplayFrame.cs
public class ManiaReplayFrame
{
    public double Time { get; set; }
    public List<ManiaAction> Actions { get; set; }
}

// ManiaReplay.cs
public class ManiaReplay
{
    public List<ManiaReplayFrame> Frames { get; set; }
    public string PlayerName { get; set; }
    public DateTime PlayTime { get; set; }
    public long Score { get; set; }
    public double Accuracy { get; set; }
    public int MaxCombo { get; set; }

    // Serialization:
    - SaveToFile(string path)
    - LoadFromFile(string path)
}

// ManiaAutoGenerator.cs
- Genera replay perfecto para Autoplay mod
- Calcula timing óptimo para cada nota
```

### 3.3 Difficulty Calculator 🆕
**Referencia: `osu.Game.Rulesets.Mania/Difficulty/`**

#### Implementar:
```csharp
// ManiaDifficultyCalculator.cs
- Star rating calculation
- Considera:
  * Density (notas por segundo)
  * Pattern complexity (streams, jacks, chords)
  * LN ratio
  * Speed changes

// ManiaPerformanceCalculator.cs
- PP (performance points) calculation
- Formula basada en:
  * Star rating
  * Accuracy
  * Score
  * Mods aplicados

// ManiaDifficultyAttributes.cs
public class ManiaDifficultyAttributes
{
    public double StarRating { get; set; }
    public double GreatHitWindow { get; set; }
    public double ScoreMultiplier { get; set; }
    public int MaxCombo { get; set; }
}
```

---

## 📋 FASE 4: Beatmap System (PRIORIDAD MEDIA)
**Duración estimada: 3-4 días**

### 4.1 Beatmap Converter 🔄 (Parcialmente implementado)
**Referencia: `osu.Game.Rulesets.Mania/Beatmaps/`**

#### Mejoras:
```csharp
// ManiaBeatmapConverter.cs improvements:

// A. Pattern Generation (para mapas no-mania):
- Distance patterns (para osu!standard -> mania)
- Hit object types (circles, sliders, spinners)
- Stair patterns
- Stream patterns
- Cycle patterns

// B. Column mapping strategies:
- Left/Right mapping
- Center mapping
- Random mapping (respetando patterns)
- Special column considerations

// C. LN conversion:
- Slider -> LN conversion
- Spinner -> LN conversion
- Duration calculation accurate
```

### 4.2 Beatmap Processor 🆕
```csharp
// ManiaBeatmapProcessor.cs
- Pre-procesa beatmap antes de gameplay:
  * Calcula TimePreempt para cada objeto
  * Aplica mods que modifican timing
  * Genera barlines automáticas
  * Valida columnas y tiempos
```

---

## 📋 FASE 5: Polish & Optimization (PRIORIDAD BAJA)
**Duración estimada: 2-3 días**

### 5.1 Performance Optimizations 🆕

#### A. Object Pooling:
```csharp
// HitObjectPool.cs
- Pool de DrawableManiaHitObject
- Reutilizar objetos en lugar de crear/destruir
- Reducir garbage collection
```

#### B. Draw Call Batching:
```csharp
- Batch de notes del mismo tipo
- Instanced rendering si es posible
- Reducir state changes
```

#### C. Culling Improvements:
```csharp
- Solo procesar objetos visibles
- Spatial partitioning para búsquedas rápidas
- Early-out en updates si no hay cambios
```

### 5.2 Advanced Configuration 🔄
**Referencia: `osu.Game.Rulesets.Mania/Configuration/`**

#### Expandir ManiaConfig:
```csharp
// Visual:
- ScrollDirection (Up/Down)
- ScrollSpeed (0.1x - 40x)
- LaneCoverHeight (0-100%)
- BackgroundDim (0-100%)
- ShowHitLighting (bool)
- ShowBarlines (bool)

// Gameplay:
- InputOverlay (bool)
- ShowPerfectJudgement (bool)
- TimingBasedColoring (bool)
- JudgementSize (float)

// Audio:
- HitsoundVolume (0-100%)
- MusicVolume (0-100%)
- SampleSet (normal/soft/drum)

// Scoring:
- ScoreDisplayMode (Normal/Classic)
- ShowPPCounter (bool)
```

---

## 📋 FASE 6: Extra Features (OPCIONAL)
**Duración estimada: Variable**

### 6.1 Online Features 🆕
```csharp
// Multiplayer support
- Room creation
- Spectating
- Leaderboards
- Score submission

// Integration:
- osu!direct style beatmap download
- Online leaderboards
```

### 6.2 Advanced UI 🆕
```csharp
// Song Select improvements:
- Grouping (by artist, date, BPM, etc)
- Filtering advanced
- Beatmap preview
- Difficulty calculation display

// Results Screen:
- Score graphs
- Hit distribution graph
- Replay playback button
- Online ranking display
```

### 6.3 Editor Mode 🆕
```csharp
// Basic beatmap editor:
- Timeline
- Note placement
- Timing setup
- Hitsound assignment
```

---

## 🎯 PLAN DE ACCIÓN INMEDIATO (Next 48 hours)

### Día 1: Core Gameplay Polish

#### Morning (4 horas):
1. ✅ **Fix DrawableHitObject system** (2h)
   - Crear DrawableNote class
   - Crear DrawableHoldNote class
   - Implementar object pooling básico

2. ✅ **Improve Column rendering** (2h)
   - Hit explosions
   - Better lighting effects
   - Judgement text display

#### Afternoon (4 horas):
3. ✅ **Score Processor accuracy** (2h)
   - Implementar ScoreV2 exacto
   - Fix accuracy calculation
   - Add performance metrics

4. ✅ **HUD improvements** (2h)
   - Better score display with animations
   - Combo counter with scaling
   - Judgement counter

### Día 2: Visual & Mods

#### Morning (4 horas):
5. ✅ **Skin system basics** (3h)
   - Skin.ini parser
   - Basic skin components
   - Legacy skin transformer

6. ✅ **Visual effects** (1h)
   - Hit lighting polished
   - Better note glow

#### Afternoon (4 horas):
7. ✅ **Mods implementation** (3h)
   - FadeIn mod
   - Hidden mod
   - Flashlight mod (básico)

8. ✅ **Testing & polish** (1h)
   - Fix bugs
   - Optimize performance
   - Test different keycounts

---

## 📊 MÉTRICAS DE ÉXITO

### Performance:
- ✅ 60 FPS estable en gameplay (1K-10K)
- ✅ <5ms input latency
- ✅ <100MB memory usage
- ✅ No frame drops durante gameplay

### Accuracy:
- ✅ Hit windows ±1ms vs osu!
- ✅ Score calculation ±0.1% vs osu!
- ✅ Accuracy display ±0.01% vs osu!

### Features:
- ✅ Todos los mods principales funcionando
- ✅ Replay system funcional
- ✅ Skin support completo
- ✅ Difficulty calculator implementado

---

## 🔧 HERRAMIENTAS & RECURSOS

### Development:
- Visual Studio 2022+
- .NET 8.0
- MonoGame 3.8+
- ManagedBass 4.0+

### Testing:
- osu!lazer (referencia)
- Multiple beatmap packs (1K-10K)
- Profiling tools (dotTrace, VS Profiler)

### Documentation:
- osu! wiki: https://osu.ppy.sh/wiki/
- ppy/osu source: https://github.com/ppy/osu
- osu!mania specific: https://github.com/ppy/osu/tree/master/osu.Game.Rulesets.Mania

---

## 📝 NOTAS FINALES

Este plan está diseñado para transformar Nullscent en un cliente completo y profesional de osu!mania, siguiendo exactamente la arquitectura y estándares de ppy/osu, pero con implementación propia para evitar problemas de copyright.

**Prioridades:**
1. 🔴 **CRÍTICO**: Core gameplay debe ser pixel-perfect vs osu!
2. 🟠 **ALTO**: Visual polish y mods principales
3. 🟡 **MEDIO**: Features avanzadas (replays, difficulty calc)
4. 🟢 **BAJO**: Features extras (online, editor)

**Filosofía de desarrollo:**
- "Make it work, make it right, make it fast" - en ese orden
- Testing constante contra osu!lazer como referencia
- Commits pequeños y frecuentes
- Documentación inline de decisiones arquitectónicas

---

**Estado actual:** En Fase 1, listo para comenzar mejoras inmediatas.
**Próximo paso:** Implementar DrawableHitObject system (Día 1, Morning, Task 1)
