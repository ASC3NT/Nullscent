# 📋 CÓMO VER LOGS DE NULLSCENT

## ✅ NUEVO: Logs Automáticos a Archivo

He modificado el código para que todos los logs se escriban automáticamente a un archivo.

### **Ubicación del archivo de logs:**
```
C:\Users\Alguien\source\repos\Nullscent\Nullscent\bin\Debug\net8.0-windows\nullscent_log.txt
```

### **Cómo usarlo:**

1. **Ejecuta el juego** (desde Visual Studio con F5 o ejecutando `Nullscent.exe`)
2. **Carga un beatmap y dale Play**
3. **Cierra el juego**
4. **Abre el archivo `nullscent_log.txt`** en Notepad o VS Code

### **Desde PowerShell:**
```powershell
cd "C:\Users\Alguien\source\repos\Nullscent\Nullscent\bin\Debug\net8.0-windows"

# Ejecutar juego
.\Nullscent.exe

# Después de cerrar, ver logs
notepad nullscent_log.txt

# O en PowerShell directamente
Get-Content nullscent_log.txt
```

---

## 📊 Qué Buscar en el Log

Cuando abras `nullscent_log.txt`, busca estas líneas:

### **1. Inicio del programa:**
```
=== Nullscent Log Started at [fecha/hora] ===
```

### **2. Al cargar un beatmap (después de darle Play):**
```
[BeatmapParser] Parsing X hit object lines for YK
[BeatmapParser] Parsed X hit objects
```
**→ Si X = 0, el beatmap no tiene notas o el parser falló**

### **3. Conversión a mania:**
```
[ManiaGameplayScreen] Initializing: [Título] [Versión]
[ManiaGameplayScreen] Original beatmap has X HitObjects
[ManiaGameplayScreen] KeyCount: Y
[ManiaGameplayScreen] Converted to X mania objects
[ManiaGameplayScreen] Notes: A, HoldNotes: B
```
**→ Si "Converted to 0", el converter falló**

### **4. Añadir al playfield:**
```
[ManiaGameplayScreen] Adding X objects to playfield
[ManiaGameplayScreen] Successfully added X objects to playfield
```
**→ Si se añaden 0 objetos, el playfield está vacío**

---

## 🔍 Ejemplo de Log Normal (Con Notas)

```
=== Nullscent Log Started at 2026-05-03 20:15:30 ===
[AudioEngine] Initialized successfully at 44100Hz
[FileDropManager] File drop manager initialized
[Game1] Starting main menu
[SongSelectScreen] Scanning beatmaps...
[BeatmapParser] Parsing 423 hit object lines for 7K
[BeatmapParser] Parsed 423 hit objects
[ManiaGameplayScreen] Initializing: Caffeine Fighter [Nervous Breakdown]
[ManiaGameplayScreen] Original beatmap has 423 HitObjects
[ManiaGameplayScreen] KeyCount: 7
[ManiaGameplayScreen] Converted to 423 mania objects
[ManiaGameplayScreen] Notes: 380, HoldNotes: 43
[ManiaGameplayScreen] Adding 423 objects to playfield
[ManiaGameplayScreen] Successfully added 423 objects to playfield
[AudioEngine] Loaded track: audio.mp3 (185234ms)
[ManiaGameplayScreen] Initialization complete: 7K, 423 objects
```

---

## 🐛 Ejemplo de Log con Problema (0 Notas)

```
=== Nullscent Log Started at 2026-05-03 20:20:15 ===
[AudioEngine] Initialized successfully at 44100Hz
[SongSelectScreen] Scanning beatmaps...
[BeatmapParser] Parsing 0 hit object lines for 7K  ← PROBLEMA AQUÍ
[BeatmapParser] Parsed 0 hit objects
[ManiaGameplayScreen] Initializing: [Título] [Versión]
[ManiaGameplayScreen] Original beatmap has 0 HitObjects  ← CONFIRMA: NO HAY NOTAS
[ManiaGameplayScreen] Converted to 0 mania objects
[ManiaGameplayScreen] Adding 0 objects to playfield
[ManiaGameplayScreen] Beatmap has no objects, going to results
```

**→ Esto significa que el archivo .osu no tiene `[HitObjects]` o está vacío**

---

## 📝 PRÓXIMOS PASOS

### **Opción A: Ver logs en Visual Studio (Recomendado)**
1. Abre VS 2026
2. Abre Nullscent.sln
3. Presiona `Ctrl + Alt + O` (abre Output)
4. En dropdown: selecciona "Debug"
5. Presiona `F5`
6. Carga beatmap y dale Play
7. Los logs aparecen en tiempo real en la ventana Output

### **Opción B: Usar el archivo de logs**
1. Ejecuta `Nullscent.exe`
2. Prueba cargar beatmap
3. Cierra el juego
4. Abre `nullscent_log.txt`
5. **Copia TODO el contenido** y pégalo aquí

---

## 🎯 LO QUE NECESITO

**Por favor, después de ejecutar el juego y probar cargar un beatmap:**

1. Abre `nullscent_log.txt`
2. Copia **TODO** el contenido
3. Pégalo aquí en el chat

Con eso podré ver exactamente:
- ✅ Si el parser encuentra las notas
- ✅ Si la conversión funciona
- ✅ Si se añaden al playfield
- ✅ Por qué va directo a results

---

## 💡 BONUS: Ver logs en tiempo real

Si quieres ver los logs mientras juegas (en PowerShell):

**Terminal 1:** (ejecuta el juego)
```powershell
cd "C:\Users\Alguien\source\repos\Nullscent\Nullscent\bin\Debug\net8.0-windows"
.\Nullscent.exe
```

**Terminal 2:** (ve los logs en tiempo real)
```powershell
cd "C:\Users\Alguien\source\repos\Nullscent\Nullscent\bin\Debug\net8.0-windows"
Get-Content nullscent_log.txt -Wait
```

El segundo terminal mostrará los logs a medida que se escriben.

---

**Ahora ejecuta el juego y envíame el contenido completo de `nullscent_log.txt`** 🔍
