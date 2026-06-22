# 🚀 QUICK START GUIDE

## Para probar el cliente INMEDIATAMENTE:

### 1. Ejecuta el setup automático
```powershell
.\setup.ps1
```

### 2. Coloca un beatmap de prueba
**IMPORTANTE**: Necesitas colocar un beatmap real de osu!mania con su archivo de audio.

Pasos:
1. Ve a https://osu.ppy.sh/beatmapsets
2. Busca un mapa de osu!mania (filtra por "osu!mania")
3. Descarga un .osz
4. Extrae el .osz en `Songs/NombreDelMapa/`

Estructura final:
```
Nullscent/
└── Songs/
    └── Artist - Title (Mapper)/
        ├── Artist - Title (Difficulty).osu
        ├── audio.mp3  ← IMPORTANTE: debe existir
        └── bg.jpg (opcional)
```

### 3. (OPCIONAL) Coloca una skin
Si quieres que se vea como osu!:
1. Busca "osu!mania skin" en Google
2. Descarga un .osk
3. Extrae en `Skins/NombreDeLaSkin/`

### 4. Ejecuta el juego
```bash
dotnet run
```

O presiona **F5** en Visual Studio.

---

## 🎮 Controles Básicos

### Song Select:
- ↑/↓: Navegar
- Enter: Jugar
- Escape: Salir

### En Juego (4K):
- **D, F, J, K**: Columnas
- **Escape**: Pausar
- **Ctrl+R**: Reintentar
- **Ctrl+Q**: Volver a song select

---

## ❌ Si no aparecen beatmaps:

1. **Verifica la estructura**:
   ```
   Songs/
   └── CarpetaDelMapa/
       └── archivo.osu  ← Debe tener extensión .osu
   ```

2. **Verifica la consola**: El juego imprime mensajes de debug

3. **Revisa settings.json**: Debe existir en la raíz

---

## 🎯 Configuración Rápida

Edita `settings.json`:

```json
{
  "ScrollSpeed": 25,          // Más alto = más rápido
  "GlobalAudioOffset": 0,     // Ajusta si hay delay
  "HitPosition": 410,         // Altura del receptor
  "DownScroll": false,        // true = scroll hacia abajo
  "ShowHitLighting": true     // Efectos visuales
}
```

---

## 🔍 Troubleshooting

### "No se encuentra bass.dll"
Instala: `dotnet add package ManagedBass`

### "No beatmaps found"
- Asegúrate de que los .osu están en subcarpetas dentro de Songs/
- NO los pongas directamente en Songs/

### El timing está mal
- Ajusta `GlobalAudioOffset` en settings.json
- Negativo = las notas vienen tarde
- Positivo = las notas vienen temprano

### El audio no carga
- Verifica que el archivo de audio (.mp3/.ogg) existe
- El nombre en el .osu debe coincidir EXACTAMENTE con el archivo

---

## 📦 Beatmaps Recomendados para Testing

Busca estos en osu.ppy.sh:
- **4K Easy**: Cualquier mapa de dificultad "Easy" o "Normal"
- **4K Hard**: Busca "Insane" o "Another"
- **7K**: Filtra por "7K" en la búsqueda

---

**¿Listo? ¡Ejecuta `dotnet run` y a jugar! 🎵**
