# 🎮 GUÍA RÁPIDA DE TESTING - NULLSCENT MANIA

## ✅ COMPILACIÓN
```bash
# El proyecto ya compila exitosamente
dotnet build Nullscent.sln
# o F5 en Visual Studio
```

## 🎯 TESTING BÁSICO

### 1. **Iniciar el juego**
- Ejecutar Nullscent.exe
- Deberías ver el Main Menu con fuente fuente.otf

### 2. **Cargar un beatmap**
- **Opción A:** Arrastra un archivo .osz o .osu a la ventana
- **Opción B:** Coloca carpetas de beatmaps en `Songs/`
- El juego escaneará automáticamente al abrir Song Select

### 3. **Song Select → Gameplay**
- Navega con ↑↓ en la lista
- Enter para seleccionar
- **NUEVO:** Ahora carga `ManiaGameplayScreen` (no el legacy)

### 4. **Durante Gameplay**
- **Teclas default:**
  - 4K: `D F J K`
  - 7K: `S D F Space J K L`
- **ESC:** Abre menú de pausa (ya no cierra el juego)
- **HUD:** Score, Accuracy, Combo, Health bar

### 5. **Menú de Pausa**
- Navegación: ↑↓
- Enter: Ejecutar opción
- Opciones:
  - Continue (vuelve al gameplay)
  - Retry (reinicia el beatmap)
  - Quit (vuelve a Song Select)

### 6. **Results Screen**
- Se muestra al completar el beatmap
- Muestra: Score, Accuracy, MaxCombo, Rank
- **R:** Retry
- **Enter/Esc:** Volver a Song Select

## 🔍 QUÉ VERIFICAR

### ✅ Debe funcionar:
- [x] Carga de beatmaps
- [x] Conversión a formato mania
- [x] Playfield centrado con múltiples columnas
- [x] Notas scrolling (hacia arriba por defecto)
- [x] HUD visible (score, accuracy, combo, health)
- [x] ESC abre pausa (no cierra)
- [x] Menú de pausa navegable
- [x] Completar beatmap → Results
- [x] Retry desde results
- [x] Volver a Song Select

### ⚠️ Limitaciones conocidas (TODO):
- [ ] **Hit detection:** Las notas NO se juzgan al presionar teclas aún
  - El playfield renderiza, pero no hay logic de hit
  - Todas las notas se auto-miss al pasar
- [ ] **Skins:** No carga sprites de skins (usa colores planos)
- [ ] **HoldNotes:** No renderizan tails correctamente
- [ ] **Scroll speed:** No se aplica el valor de config aún
- [ ] **Audio samples:** No reproduce hitsounds al golpear
- [ ] **Visual effects:** No hay lighting, explosions, o animations

## 🐛 PROBLEMAS COMUNES

### **"No se ven notas"**
- Verifica que el beatmap tiene HitObjects
- Chequea la consola para errores de conversión

### **"ESC cierra el juego"**
- Asegúrate de estar en ManiaGameplayScreen (no legacy)
- SongSelectScreen debe instanciar ManiaGameplayScreen

### **"Crash al cargar beatmap"**
- Verifica que el audio existe: `Songs/[beatmap]/[audio].mp3`
- Chequea la consola para stack traces

### **"HUD no se ve"**
- Verifica fuente.otf en root
- ManiaConfig.Show* flags deben estar en true

## 🎨 PERSONALIZACIÓN RÁPIDA

### **Cambiar KeyCount**
En `ManiaGameplayScreen.cs` línea ~100:
```csharp
var converter = new ManiaBeatmapConverter(_originalBeatmap, 7); // Force 7K
```

### **Cambiar Scroll Speed**
En `ManiaConfig.cs`:
```csharp
public double ScrollSpeed { get; set; } = 30.0; // Más rápido
```

### **Habilitar DownScroll**
En `ManiaConfig.cs`:
```csharp
public bool DownScroll { get; set; } = true; // Notes caen en lugar de subir
```

### **Probar mods**
En `SongSelectScreen.cs` antes de crear gameplay:
```csharp
var mods = new List<ManiaMod>
{
    new ManiaModDoubleTime(), // 1.5x speed
    new ManiaModRandom()      // Shuffle columns
};

var gameplayScreen = new ManiaGameplayScreen(
    _game, _graphicsDevice, _spriteBatch, _stateManager,
    _audioEngine, _inputManager, _hitSoundPlayer, _settings,
    beatmap,
    mods // <-- Pasar mods aquí
);
```

## 📊 DEBUGGING

### **Ver logs en consola:**
```csharp
Console.WriteLine("[ManiaGameplayScreen] ...");
Console.WriteLine("[ManiaBeatmapConverter] ...");
Console.WriteLine("[ManiaPlayfield] ...");
```

### **Breakpoints útiles:**
- `ManiaGameplayScreen.Initialize()` - Setup inicial
- `ManiaBeatmapConverter.Convert()` - Conversión de beatmap
- `ManiaPlayfield.Update()` - Update loop de notas
- `ManiaScoreProcessor.ApplyResult()` - Scoring (cuando se implemente hit detection)

## 🚀 SIGUIENTE PASO: HIT DETECTION

Para hacer el juego realmente jugable, implementar en `ManiaColumn.OnKeyDown()`:

```csharp
public void OnKeyDown(double currentTime)
{
    _isPressed = true;

    // Buscar la nota más cercana al receptor
    var nearestNote = _hitObjects
        .OfType<Note>()
        .OrderBy(n => Math.Abs(n.StartTime - currentTime))
        .FirstOrDefault();

    if (nearestNote != null)
    {
        double offset = currentTime - nearestNote.StartTime;
        var result = _hitWindows.JudgeHit(offset);

        if (result != HitResult.Miss)
        {
            // Hit exitoso
            _scoreProcessor.ApplyResult(result);
            _hitObjects.Remove(nearestNote);

            // TODO: Play hitsound
            // TODO: Show judgement text
            // TODO: Trigger hit lighting
        }
    }
}
```

## ✨ TESTING CHECKLIST

Antes de considerar la migración "completa":

- [ ] Jugar un beatmap completo sin crashes
- [ ] Pausar y resumir correctamente
- [ ] Ver results screen con stats correctas
- [ ] Retry funcional
- [ ] Diferentes KeyCounts (4K, 7K)
- [ ] Mods aplicándose correctamente
- [ ] Audio synced con notas
- [ ] Health drain funcionando
- [ ] Combos contando

## 🎉 DISFRUTA

¡La base está lista! Ahora es cuestión de pulir y añadir las features visuales y de gameplay que faltan.

**Happy coding! 🎮✨**
