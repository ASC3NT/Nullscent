using System;
using System.IO;

// Configurar logging a archivo
var logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "nullscent_log.txt");
var logWriter = new StreamWriter(logPath, append: false) { AutoFlush = true };
Console.SetOut(logWriter);

Console.WriteLine($"=== Nullscent Log Started at {DateTime.Now} ===");
Console.WriteLine($"Log file: {logPath}");

using var game = new Nullscent.Game1();
game.Run();

logWriter.Close();
