# 🗺️ Roadmap de Migración - Nullscent a Arquitectura Mania

## 📌 Visión General

Este roadmap describe el plan completo para migrar Nullscent desde su arquitectura actual hacia la nueva arquitectura profesional inspirada en ppy/osu!mania.

---

## 🎯 Objetivos

1. **Mantener funcionalidad existente** durante toda la migración
2. **Mejorar progresivamente** cada componente
3. **Eliminar duplicación** de código
4. **Adoptar best practices** de ppy/osu
5. **Habilitar características avanzadas** (mods, replays, etc.)

---

## 📅 Fases de Migración

### ✅ Fase 0: Preparación (COMPLETADA)
**Duración:** 1 día  
**Estado:** ✅ Completado

**Objetivos:**
- [x] Crear estructura de carpetas Rulesets/Mania
- [x] Implementar ManiaHitObject (Note, HoldNote)
- [x] Implementar ManiaJudgement (HitWindows, Results)
- [x] Implementar ManiaScoreProcessor (Scoring V2)
- [x] Implementar ManiaPlayfield (Playfield, Column)
- [x] Implementar ManiaMods (15+ mods)
- [x] Implementar ManiaConfig (20+ opciones)

**Resultados:**
- ✅ Nueva arquitectura coexiste con la actual
- ✅ Compilación sin errores
- ✅ Base sólida para migración

---

### 🔄 Fase 1: Migración del Sistema de Juego
**Duración estimada:** 2-3 días  
**Prioridad:** Alta  
**Estado:** ⏳ Pendiente

#### 1.1: Actualizar GameplayScreen
**Tareas:**
- [ ] Reemplazar `Column[]` con `ManiaPlayfield`
- [ ] Reemplazar `ScoreEngine` con `ManiaScoreProcessor`
- [ ] Reemplazar `HitJudge` con `ManiaHitWindows`
- [ ] Reemplazar `HealthBar` con `ManiaHealthProcessor`
- [ ] Actualizar input handling para usar ManiaColumn
- [ ] Actualizar rendering para usar ManiaPlayfield.Draw()

**Archivos afectados:**
- `Nullscent/Gameplay/GameplayScreen.cs` → Reescribir
- `Nullscent/Gameplay/Column.cs` → Deprecar (usar ManiaColumn)
- `Nullscent/Gameplay/ScoreEngine.cs` → Deprecar (usar ManiaScoreProcessor)
- `Nullscent/Gameplay/HitJudge.cs` → Deprecar (usar ManiaJudgement)
- `Nullscent/Gameplay/HealthBar.cs` → Deprecar (usar ManiaHealthProcessor)

**Testing:**
- [ ] Gameplay funcional con nueva arquitectura
- [ ] Scoring correcto (V2 formula)
- [ ] Hit windows precisas
- [ ] Health calculation correcta
- [ ] No regression de funcionalidad

---

#### 1.2: Migrar Sistema de Notas
**Tareas:**
- [ ] Crear `ManiaBeatmapConverter`
- [ ] Convertir `HitObject` legacy a `ManiaHitObject`
- [ ] Actualizar `BeatmapParser` para generar `ManiaHitObject`
- [ ] Soporte para samples múltiples por nota
- [ ] Mantener compatibilidad con .osu parsing

**Archivos nuevos:**
- `Rulesets/Mania/Beatmaps/ManiaBeatmapConverter.cs`
- `Rulesets/Mania/Beatmaps/ManiaBeatmap.cs`

**Archivos afectados:**
- `Nullscent/Beatmap/BeatmapParser.cs` → Actualizar

**Testing:**
- [ ] Beatmaps se parsean correctamente
- [ ] Notas se convierten a ManiaHitObject
- [ ] LN (hold notes) funcionan correctamente
- [ ] Samples se cargan correctamente

---

#### 1.3: Integrar Sistema de Mods
**Tareas:**
- [ ] Crear `ModManager` para gestionar mods activos
- [ ] Implementar aplicación de mods en GameplayScreen
- [ ] Implementar efectos de mods:
  - [ ] DT/HT (cambio de velocidad)
  - [ ] HR/EZ (cambio de hit windows)
  - [ ] HD/FI (fade in/out de notas)
  - [ ] Random (shuffle de columnas)
  - [ ] Mirror (flip de playfield)
- [ ] Mostrar mods activos en UI
- [ ] Score multiplier calculation

**Archivos nuevos:**
- `Rulesets/Mania/Mods/ModManager.cs`
- `Rulesets/Mania/UI/ModDisplay.cs`

**Testing:**
- [ ] Mods se aplican correctamente
- [ ] Score multipliers funcionan
- [ ] Efectos visuales de mods funcionan
- [ ] Combinaciones de mods funcionan

---

### 🔄 Fase 2: Mejoras de UI/UX
**Duración estimada:** 2 días  
**Prioridad:** Media  
**Estado:** ⏳ Pendiente

#### 2.1: Mejorar Rendering de Notas
**Tareas:**
- [ ] Implementar note skins (Default, Arrow, Bar)
- [ ] Implementar hit lighting effects
- [ ] Implementar judgement display (Perfect, Great, etc.)
- [ ] Implementar combo display con animaciones
- [ ] Implementar barlines
- [ ] Implementar lane covers
- [ ] Implementar column lighting

**Archivos nuevos:**
- `Rulesets/Mania/Skinning/ManiaNoteSkin.cs`
- `Rulesets/Mania/UI/JudgementDisplay.cs`
- `Rulesets/Mania/UI/ComboDisplay.cs`
- `Rulesets/Mania/UI/HitLighting.cs`

**Testing:**
- [ ] Notas se ven bien con diferentes skins
- [ ] Hit lighting funciona correctamente
- [ ] Judgement text aparece correctamente
- [ ] Combo display anima correctamente

---

#### 2.2: Mejorar HUD de Gameplay
**Tareas:**
- [ ] Rediseñar score display
- [ ] Rediseñar accuracy display
- [ ] Rediseñar health bar
- [ ] Agregar progress bar
- [ ] Agregar key overlay (opcional)
- [ ] Agregar performance graph (opcional)

**Archivos afectados:**
- `Nullscent/Gameplay/GameplayScreen.cs` → Actualizar DrawHUD()

**Testing:**
- [ ] HUD se ve profesional
- [ ] Información es clara y legible
- [ ] Animaciones son smooth
- [ ] No afecta rendimiento

---

### 🔄 Fase 3: Características Avanzadas
**Duración estimada:** 3-4 días  
**Prioridad:** Baja  
**Estado:** ⏳ Pendiente

#### 3.1: Sistema de Dificultad
**Tareas:**
- [ ] Implementar star rating calculation
- [ ] Implementar difficulty attributes
- [ ] Implementar performance points (pp) calculation
- [ ] Mostrar star rating en song select
- [ ] Mostrar difficulty attributes en beatmap info

**Archivos nuevos:**
- `Rulesets/Mania/Difficulty/ManiaDifficultyCalculator.cs`
- `Rulesets/Mania/Difficulty/ManiaPerformanceCalculator.cs`
- `Rulesets/Mania/Difficulty/ManiaDifficultyAttributes.cs`

**Referencias:**
- https://github.com/ppy/osu/tree/master/osu.Game.Rulesets.Mania/Difficulty

---

#### 3.2: Sistema de Replays
**Tareas:**
- [ ] Implementar replay recording
- [ ] Implementar replay playback
- [ ] Implementar replay export/import
- [ ] Mostrar replays en results screen
- [ ] Permitir ver replays desde song select

**Archivos nuevos:**
- `Rulesets/Mania/Replays/ManiaReplay.cs`
- `Rulesets/Mania/Replays/ManiaReplayFrame.cs`
- `Rulesets/Mania/Replays/ManiaAutoGenerator.cs`

**Testing:**
- [ ] Replays se graban correctamente
- [ ] Replays se reproducen correctamente
- [ ] Autoplay genera replay perfecto

---

#### 3.3: Sistema de Skinning Avanzado
**Tareas:**
- [ ] Implementar skin.ini parsing completo
- [ ] Soporte para diferentes note skins
- [ ] Soporte para diferentes stage backgrounds
- [ ] Soporte para diferentes hit lighting
- [ ] Soporte para custom sounds
- [ ] Soporte para custom textures

**Archivos nuevos:**
- `Rulesets/Mania/Skinning/ManiaLegacySkinTransformer.cs`
- `Rulesets/Mania/Skinning/ManiaSkinComponent.cs`

**Referencias:**
- https://github.com/ppy/osu/tree/master/osu.Game.Rulesets.Mania/Skinning

---

### 🔄 Fase 4: Optimización y Polish
**Duración estimada:** 2 días  
**Prioridad:** Media  
**Estado:** ⏳ Pendiente

#### 4.1: Optimización de Rendimiento
**Tareas:**
- [ ] Profiling de gameplay
- [ ] Optimizar note rendering (culling)
- [ ] Optimizar hit detection
- [ ] Optimizar audio playback
- [ ] Reducir allocations en hot path
- [ ] Object pooling para notas

**Tools:**
- Visual Studio Profiler
- dotTrace
- BenchmarkDotNet

**Target:**
- 60 FPS estable en gameplay
- <5ms frame time en 4K maps
- <10ms frame time en 7K maps

---

#### 4.2: Polish Visual
**Tareas:**
- [ ] Agregar transiciones smooth entre pantallas
- [ ] Agregar animaciones de entrada/salida
- [ ] Agregar particle effects (opcional)
- [ ] Mejorar shaders (opcional)
- [ ] Agregar bloom effects (opcional)

---

#### 4.3: Polish de Audio
**Tareas:**
- [ ] Mejorar hitsound playback
- [ ] Agregar audio feedback para UI
- [ ] Optimizar audio latency
- [ ] Agregar pitch shifting para DT/HT

---

### 🔄 Fase 5: Limpieza de Código Legacy
**Duración estimada:** 1 día  
**Prioridad:** Baja  
**Estado:** ⏳ Pendiente

#### 5.1: Eliminar Código Duplicado
**Tareas:**
- [ ] Eliminar `Column.cs` (usar `ManiaColumn`)
- [ ] Eliminar `ScoreEngine.cs` (usar `ManiaScoreProcessor`)
- [ ] Eliminar `HitJudge.cs` (usar `ManiaJudgement`)
- [ ] Eliminar `HealthBar.cs` (usar `ManiaHealthProcessor`)
- [ ] Eliminar código no usado en `HitObject.cs`
- [ ] Consolidar beatmap parsing

**Beneficios:**
- Menor tamaño de codebase
- Menos confusión
- Mejor mantenibilidad

---

## 📊 Métricas de Éxito

### Performance Targets:
- ✅ 60 FPS estable en gameplay (4K-7K)
- ✅ <5ms input latency
- ✅ <100MB memory usage
- ✅ <1s song select load time

### Code Quality Targets:
- ✅ <10% código duplicado
- ✅ >80% test coverage (futuro)
- ✅ Zero memory leaks
- ✅ Zero warnings en build

### Feature Completeness:
- ✅ Todos los mods implementados
- ✅ Todas las key counts (1K-10K)
- ✅ Replay system funcional
- ✅ Difficulty calculation
- ✅ Advanced skinning

---

## 🚨 Riesgos y Mitigación

### Riesgo 1: Breaking Changes
**Mitigación:**
- Mantener código legacy durante migración
- Testing exhaustivo en cada fase
- Rollback plan disponible

### Riesgo 2: Performance Regression
**Mitigación:**
- Profiling antes y después
- Benchmarking automático
- Optimización incremental

### Riesgo 3: Complejidad Excesiva
**Mitigación:**
- Migración gradual (no big bang)
- Documentación detallada
- Code reviews

---

## 📚 Recursos Necesarios

### Conocimiento:
- C# / .NET 8
- MonoGame framework
- osu! beatmap format
- osu!mania gameplay mechanics
- Git / GitHub

### Herramientas:
- Visual Studio 2022+
- Git
- Profiler (dotTrace o VS Profiler)
- BenchmarkDotNet (opcional)

### Referencias:
- ppy/osu source code
- osu! wiki
- osu! community forums

---

## 🎉 Siguiente Acción Inmediata

**Para comenzar Fase 1:**

1. **Backup del código actual:**
   ```bash
   git add .
   git commit -m "Backup antes de migración Fase 1"
   git branch backup-pre-migration
   ```

2. **Crear nueva rama para migración:**
   ```bash
   git checkout -b feature/migrate-gameplay-to-mania
   ```

3. **Comenzar con GameplayScreen:**
   - Abrir `Nullscent/Gameplay/GameplayScreen.cs`
   - Comentar código legacy
   - Implementar nuevo código usando `ManiaPlayfield`
   - Compilar y testear frecuentemente

4. **Testing continuo:**
   - Probar con beatmaps de diferentes key counts
   - Verificar que scoring funcione
   - Verificar que input funcione
   - Verificar que rendering funcione

---

**Estado del Roadmap:** 📋 Definido y Listo  
**Próxima Fase:** Fase 1 - Migración del Sistema de Juego  
**ETA para Fase 1:** 2-3 días  
**ETA para completar todas las fases:** 10-12 días  

*Última actualización: 2026-05-03*
