using System;
using System.IO;

namespace SharpTimer.App.Services;

public static class AppDataPaths
{
    public static string LocalDataFolder
    {
        get
        {
            var path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SharpTimer");
            Directory.CreateDirectory(path);
            return path;
        }
    }

    public static string DatabasePath => Path.Combine(LocalDataFolder, "sharptimer.db");

    public static string SettingsPath => Path.Combine(LocalDataFolder, "settings.json");
}
