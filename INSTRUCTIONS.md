# Nullscent - osu!mania Practice Client

## 🎮 Descripción
Nullscent es un cliente de práctica para osu!mania inspirado en osu!lazer y McOsu. Es un cliente **solo para práctica local** - sin rankings online, sin multiplayer, solo tú y los mapas.

## ✅ Estado del Proyecto
El proyecto está **completamente funcional** y listo para probar. Puedes:
- Cargar y jugar beatmaps de osu!mania (.osu)
- Usar skins personalizadas compatibles con osu!mania
- Configurar keybinds, audio offset, scroll speed
- Ver resultados con precisión, accuracy y grado
- Sistema de replay/retry instantáneo
- Pause menu funcional

## 📋 Requisitos Previos
- Windows 10/11
- .NET 8 Runtime
- Visual Studio 2022 (para desarrollo)
- Beatmaps de osu!mania (.osu o .osz)
- Skin de osu!mania (opcional)

## 🚀 Cómo Probar el Cliente

### 1. Preparar Beatmaps
1. Crea la carpeta `Songs` en la raíz del proyecto
2. Coloca tus beatmaps de osu!mania dentro:
   - **Opción A**: Extrae archivos .osz en subcarpetas
   - **Opción B**: Copia carpetas de beatmap directamente

Estructura recomendada:
```
Nullscent/
├── Songs/
│   ├── Artist - Title (Mapper)/
│   │   ├── Artist - Title (Difficulty).osu
│   │   ├── audio.mp3
│   │   └── bg.jpg
│   └── Otra Canción/
│       └── ...
```

### 2. Preparar Skin (Opcional)
1. Crea la carpeta `Skins` en la raíz del proyecto
2. Coloca tu skin de osu!mania:
   - Extrae un .osk o copia una carpeta de skin completa

Estructura:
```
Nullscent/
├── Skins/
│   ├── DefaultSkin/
│   │   ├── skin.ini
│   │   ├── mania-note1.png
│   │   ├── mania-key1.png
│   │   └── ...
```

**Nota**: Si no tienes skin, el cliente usa renderizado con rectángulos de colores (totalmente funcional).

### 3. Configurar el Juego (Opcional)
Edita `settings.json` en la raíz del proyecto para ajustar:
```json
{
  "ScrollSpeed": 20,
  "GlobalAudioOffset": 0.0,
  "MasterVolume": 0.8,
  "MusicVolume": 0.7,
  "HitsoundVolume": 0.5,
  "HitPosition": 410,
  "DownScroll": false,
  "ShowHitLighting": true,
  "CurrentSkinPath": "Skins/DefaultSkin"
}
```

### 4. Ejecutar el Cliente
Desde Visual Studio:
1. Abre `Nullscent.sln`
2. Presiona F5 o Click en "Start"

Desde terminal:
```bash
cd Nullscent
dotnet run
```

### 5. Controles

#### Song Select
- **↑/↓**: Navegar beatmaps
- **Enter**: Seleccionar y comenzar gameplay
- **Escape**: Salir
- **Backspace**: Borrar búsqueda
- **Letras/números**: Buscar beatmaps

#### Gameplay (4K default)
- **D, F, J, K**: Teclas de columnas (izquierda a derecha)
- **Escape**: Pausar
- **Ctrl+R**: Retry rápido
- **Ctrl+Q**: Quit a song select

#### Pause Menu
- **↑/↓**: Navegar opciones
- **Enter**: Seleccionar
- **Escape**: Reanudar

## 🎯 Características Implementadas

### Core Engine
- ✅ Parser completo de beatmaps .osu (formato osu!mania)
- ✅ Audio engine con BASS.NET (playback, rate control, seeking)
- ✅ Input manager de alta frecuencia (1000Hz polling)
- ✅ Hit judgment preciso (MAX, 300, 200, 100, 50, MISS)
- ✅ Score system ScoreV2 oficial de osu!
- ✅ Health bar con drenaje/recuperación

### Gameplay
- ✅ Columnas renderizadas con receptores
- ✅ Notas normales y long notes (LN/holds)
- ✅ Efectos de hit (lighting, particle trails)
- ✅ HUD completo (combo, score, accuracy, health, judgment)
- ✅ Pause menu funcional
- ✅ Results screen detallada

### UI/UX
- ✅ Song select screen optimizada
- ✅ Búsqueda de mapas en tiempo real
- ✅ Settings manager persistente
- ✅ Keybind configuration (1K-18K)

### Skinning
- ✅ Sistema de skinning compatible con osu!mania
- ✅ Soporte para skin.ini parsing
- ✅ Texturas personalizadas (notas, keys, stage, judgements)
- ✅ Fallback rendering cuando no hay skin

### Audio
- ✅ Hitsounds personalizados
- ✅ Control de volumen separado (master, music, hitsound)
- ✅ Global audio offset
- ✅ Playback rate control (future)

## ⚙️ Configuraciones Disponibles

### Gameplay
- `ScrollSpeed`: Velocidad de scroll (1-40, default: 20)
- `HitPosition`: Altura del receptor en píxeles (default: 410)
- `DownScroll`: Scroll hacia abajo (default: false)
- `ShowHitLighting`: Efectos de iluminación (default: true)

### Audio
- `GlobalAudioOffset`: Offset global en ms (default: 0.0)
- `MasterVolume`: Volumen maestro (0.0-1.0, default: 0.8)
- `MusicVolume`: Volumen de música (0.0-1.0, default: 0.7)
- `HitsoundVolume`: Volumen de hitsounds (0.0-1.0, default: 0.5)

### Keybinds
Totalmente configurables por key count en settings.json:
```json
"Keybinds": {
  "4": ["D", "F", "J", "K"],
  "7": ["S", "D", "F", "Space", "J", "K", "L"]
}
```

## 🐛 Troubleshooting

### "No beatmaps found"
- Verifica que la carpeta `Songs/` existe
- Asegúrate de que los .osu están en subcarpetas
- Los archivos deben tener extensión .osu

### "Audio failed to load"
- Verifica que el archivo de audio existe en la carpeta del beatmap
- Formatos soportados: .mp3, .ogg
- Revisa que el nombre en el .osu coincide con el archivo real

### El juego se ve diferente a osu!
- Coloca una skin en `Skins/` y configura `CurrentSkinPath` en settings.json
- El cliente usa fallback rendering si no hay skin (totalmente normal)

### Input lag / timing issues
- Ajusta `GlobalAudioOffset` en settings.json
- Valores negativos = notas aparecen más tarde
- Valores positivos = notas aparecen más temprano

## 🔧 Configuración de Desarrollo

### Dependencias NuGet
```xml
<PackageReference Include="MonoGame.Framework.WindowsDX" Version="3.8.2.1105" />
<PackageReference Include="ManagedBass" Version="3.1.1" />
<PackageReference Include="Newtonsoft.Json" Version="13.0.3" />
```

### Estructura del Proyecto
```
Nullscent/
├── Audio/           # AudioEngine, HitSoundPlayer
├── Beatmap/         # BeatmapParser, Beatmap, HitObject
├── Core/            # InputManager, GameStateManager
├── Config/          # GameSettings, Keybinds
├── Gameplay/        # GameplayScreen, ScoreProcessor, HealthBar
├── Screens/         # SongSelect, Pause, Results
├── Skin/            # SkinManager, SkinConfig, SkinConfigParser
├── UI/              # FontRenderer, NoteRenderer
└── Game1.cs         # Entry point
```

## 📝 Notas Importantes

1. **Este es un cliente de práctica**: No hay integración con osu! servers
2. **Compatible con .osu**: Usa tus beatmaps existentes de osu!
3. **Skins compatibles**: Cualquier skin de osu!mania funciona
4. **Sin online features**: No hay leaderboards, no hay multiplayer
5. **Focus en practice**: Retry instantáneo, offset adjustment, rate control

## 🎓 Inspiración
- **osu!lazer**: UI/UX moderna, clean architecture
- **McOsu**: Practice-focused features, configurability

## 📄 Licencia
Este proyecto es educacional y no afiliado con osu! o ppy.

---

**¡Disfruta practicando! 🎵**
