using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SharpTimer.App.Services;

public sealed class AppSettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public AppSettings Load()
    {
        if (!File.Exists(AppDataPaths.SettingsPath))
        {
            return new AppSettings();
        }

        try
        {
            var json = File.ReadAllText(AppDataPaths.SettingsPath);
            var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();

            return settings with
            {
                DecimalPlaces = Math.Clamp(settings.DecimalPlaces, 2, 3),
                ScrambleFontSize = Math.Clamp(settings.ScrambleFontSize, 16, 34),
                SmartCubePreviewSize = Math.Clamp(settings.SmartCubePreviewSize, 140, 320)
            };
        }
        catch (JsonException)
        {
            return new AppSettings();
        }
        catch (IOException)
        {
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        var normalized = settings with
        {
            DecimalPlaces = Math.Clamp(settings.DecimalPlaces, 2, 3),
            ScrambleFontSize = Math.Clamp(settings.ScrambleFontSize, 16, 34),
            SmartCubePreviewSize = Math.Clamp(settings.SmartCubePreviewSize, 140, 320)
        };

        var json = JsonSerializer.Serialize(normalized, JsonOptions);
        File.WriteAllText(AppDataPaths.SettingsPath, json);
    }
}
