#nullable enable

using System;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Color = Microsoft.Xna.Framework.Color;

namespace Nullscent.Core
{
    /// <summary>
    /// Ayudante optimizado para realizar capturas de pantalla sin congelar el hilo principal de renderizado.
    /// Captura los píxeles desde el backbuffer y los guarda asíncronamente en formatos BMP (sin comprimir) o PNG (comprimido).
    /// </summary>
    public static class ScreenshotHelper
    {
        private static bool _isCapturing = false;

        public static void TakeScreenshot(GraphicsDevice graphicsDevice, bool compress)
        {
            if (_isCapturing) return;
            _isCapturing = true;

            try
            {
                int w = graphicsDevice.PresentationParameters.BackBufferWidth;
                int h = graphicsDevice.PresentationParameters.BackBufferHeight;
                
                // 1. Obtener los píxeles del backbuffer en el hilo principal de MonoGame (rápido, ~2-5ms)
                Color[] backBufferData = new Color[w * h];
                graphicsDevice.GetBackBufferData(backBufferData);

                // 2. Procesar y guardar el archivo en un hilo de fondo (sin congelar el gameplay)
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string extension = compress ? ".png" : ".bmp";
                string fileName = $"screenshot_{timestamp}{extension}";
                string screenshotsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Screenshots");
                string filePath = Path.Combine(screenshotsDir, fileName);

                Task.Run(() =>
                {
                    try
                    {
                        Directory.CreateDirectory(screenshotsDir);

                        // Crear bitmap en GDI+ usando Formato 32bpp Argb (que espera BGRA en bytes)
                        using (var bitmap = new Bitmap(w, h, PixelFormat.Format32bppArgb))
                        {
                            var rect = new System.Drawing.Rectangle(0, 0, w, h);
                            var bmpData = bitmap.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);

                            // MonoGame Color tiene formato RGBA (R, G, B, A en bytes individuales)
                            // GDI+ Format32bppArgb espera BGRA (B, G, R, A). 
                            // Convertimos los bytes empaquetándolos en un int de orden BGRA:
                            int[] argbPixels = new int[backBufferData.Length];
                            for (int i = 0; i < backBufferData.Length; i++)
                            {
                                Color c = backBufferData[i];
                                // (A << 24) | (R << 16) | (G << 8) | B
                                argbPixels[i] = (c.A << 24) | (c.R << 16) | (c.G << 8) | c.B;
                            }

                            // Copiar el buffer completo en memoria a GDI+
                            Marshal.Copy(argbPixels, 0, bmpData.Scan0, argbPixels.Length);
                            bitmap.UnlockBits(bmpData);

                            // Guardar en el formato correspondiente
                            if (compress)
                            {
                                bitmap.Save(filePath, ImageFormat.Png);
                            }
                            else
                            {
                                bitmap.Save(filePath, ImageFormat.Bmp);
                            }
                        }

                        Console.WriteLine($"[ScreenshotHelper] Screenshot saved successfully: {filePath}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[ScreenshotHelper] Error saving screenshot: {ex.Message}");
                    }
                    finally
                    {
                        _isCapturing = false;
                    }
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ScreenshotHelper] Error capturing backbuffer: {ex.Message}");
                _isCapturing = false;
            }
        }
    }
}
