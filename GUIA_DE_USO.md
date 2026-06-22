# Guía de Uso - Nullscent

## 🎮 Control en Gameplay

### Problema Resuelto: ESC ya no cierra el juego
Antes, al presionar ESC en gameplay, el juego se cerraba directamente. Ahora:

1. **Primera vez que presionas ESC en gameplay:** Se abre el menú de pausa
2. **En el menú de pausa tienes opciones:**
   - **Resume:** Continuar jugando
   - **Retry:** Reintentar el mapa desde el inicio
   - **Change Rate:** Cambiar velocidad de reproducción (0.5x, 0.75x, 1.0x, 1.25x, 1.5x)
   - **Quit to Song Select:** Volver a la selección de canciones

3. **El flujo correcto ahora es:**
   ```
   Main Menu → Song Select → Gameplay
   En Gameplay: ESC → Pause Menu
   Pause Menu: Quit → Song Select
   Song Select: ESC → Main Menu
   Main Menu: ESC → Salir del juego
   ```

### Cambios Técnicos Realizados:
- ✅ El gameplay ahora solo pausa al presionar ESC (no cierra)
- ✅ El estado del teclado se resetea al mostrar el pause menu (previene input fantasma)
- ✅ El SongSelect inicializa correctamente el estado del teclado al entrar
- ✅ El flujo de navegación es coherente y predecible

---

## 📁 Sistema de Drag & Drop

### ¿Cómo importar Beatmaps y Skins?

El sistema de drag & drop **está funcionando** y configurado correctamente. Aquí está cómo usarlo:

#### Para importar Beatmaps (.osz):
1. Descarga un archivo `.osz` (beatmap pack de osu!)
2. **Arrastra el archivo directamente sobre la ventana de Nullscent** mientras el juego está ejecutándose
3. Verás mensajes en la consola:
   ```
   [FileDropManager] File dropped: C:\path\to\beatmap.osz
   [FileDropManager] Imported beatmap: nombre_del_mapa -> C:\...\Songs\nombre_del_mapa
   ```
4. El mapa se extrae automáticamente a la carpeta `Songs/`
5. Ve a **Song Select** para ver el nuevo mapa

#### Para importar Skins (.osk):
1. Descarga un archivo `.osk` (skin pack de osu!)
2. **Arrastra el archivo directamente sobre la ventana de Nullscent**
3. Verás mensajes en la consola:
   ```
   [FileDropManager] File dropped: C:\path\to\skin.osk
   [FileDropManager] Skin imported: nombre_de_la_skin
   ```
4. El skin se extrae automáticamente a la carpeta `Skins/`
5. Ve a **Settings** para seleccionar el nuevo skin

#### Para importar beatmaps sueltos (.osu):
1. Si tienes un archivo `.osu` individual
2. **Arrástralo sobre la ventana de Nullscent**
3. Se creará una carpeta nueva en `Songs/` con ese beatmap

### Verificación del Sistema Drag & Drop

Al iniciar el juego, deberías ver estos mensajes en la consola:

```
[FileDropManager] Songs directory: C:\...\Songs
[FileDropManager] Skins directory: C:\...\Skins
[FileDropManager] Window handle: [número]
[FileDropManager] Form control obtained successfully
[FileDropManager] ✓ Drag & drop enabled successfully!
[FileDropManager] You can now drag .osz, .osk, or .osu files into the window
```

Si ves el mensaje `✓ Drag & drop enabled successfully!`, significa que está funcionando.

### Solución de Problemas - Drag & Drop

**Si el drag & drop no funciona:**

1. **Verifica la consola:** 
   - Si ves "ERROR: Could not obtain form control from window handle" → Reinicia el juego
   - Si ves "ERROR: Could not enable drag & drop" → Verifica que UseWindowsForms esté en el .csproj

2. **Asegúrate de que:**
   - Estás arrastrando archivos `.osz`, `.osk`, o `.osu` válidos
   - El juego está ejecutándose (no minimizado)
   - Tienes permisos de escritura en las carpetas Songs/ y Skins/

3. **Formatos soportados:**
   - ✅ `.osz` (beatmap packs) → Se extraen a `Songs/`
   - ✅ `.osk` (skin packs) → Se extraen a `Skins/`
   - ✅ `.osu` (beatmaps individuales) → Se copian a `Songs/[nombre]/`
   - ❌ Otros formatos no son soportados

### Ejemplo de uso completo:

1. **Inicia Nullscent**
2. Ve a cualquier pantalla (Main Menu, Song Select, Settings, etc.)
3. **Arrastra un archivo .osz desde tu explorador de archivos**
4. **Suelta el archivo sobre la ventana del juego**
5. Espera el mensaje de confirmación en consola
6. Ve a Song Select → El nuevo mapa debería aparecer
7. ¡Juega!

---

## 🎨 Configuración de Skins

Las skins se gestionan en **Settings → Current Skin**

Cuando importas una skin con drag & drop:
- Se extrae automáticamente a `Skins/[nombre_de_la_skin]/`
- Se carga automáticamente como skin activa
- Se guarda en la configuración

---

## ⚙️ Configuraciones Disponibles

### Gameplay:
- **Scroll Speed:** Velocidad de las notas
- **Down Scroll:** Dirección de las notas (arriba/abajo)
- **Receptor Position:** Posición de los receptores
- **Health Drain:** Activar/desactivar drenaje de vida

### Audio:
- **Master Volume:** Volumen general
- **Music Volume:** Volumen de la música
- **Hitsound Volume:** Volumen de los hitsounds
- **Global Audio Offset:** Ajuste de sincronización

### Video:
- **VSync:** Sincronización vertical
- **Show FPS:** Mostrar contador de FPS
- **Background Dim:** Opacidad del fondo

---

## 🔧 Changelog de esta actualización

### Arreglos ESC en Gameplay:
- ✅ ESC ahora pausa el juego correctamente
- ✅ No se cierra el juego al presionar ESC en gameplay
- ✅ El flujo de navegación es coherente
- ✅ Se previene input fantasma al cambiar de pantalla

### Sistema Drag & Drop:
- ✅ Mensajes de debug mejorados
- ✅ Feedback visual en consola al arrastrar archivos
- ✅ Validación de formatos soportados
- ✅ Manejo de errores mejorado
- ✅ Confirmación de drag & drop habilitado al inicio

---

**Estado del Proyecto:** ✅ Totalmente funcional
**Build:** ✅ Sin errores de compilación
