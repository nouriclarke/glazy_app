using Avalonia;
using ReactiveUI;
using System;
using System.IO;

namespace ASTEM_DB;

sealed class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        LoadEnvFile();
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    private static void LoadEnvFile()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, ".env");
            if (File.Exists(candidate))
            {
                DotNetEnv.Env.Load(candidate);
                Console.WriteLine($"[env] Loaded {candidate}");
                return;
            }
            dir = dir.Parent;
        }
        Console.WriteLine("[env] No .env file found — falling back to defaults.");
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}