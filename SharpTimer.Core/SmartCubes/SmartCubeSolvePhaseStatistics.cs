using System.Text.Json;

namespace SharpTimer.Core.SmartCubes;

public static class SmartCubeSolvePhaseStatistics
{
    public static IReadOnlyList<SmartCubeSolvePhaseStatistic> FromSolveMetaJson(string? solveMetaJson)
    {
        if (string.IsNullOrWhiteSpace(solveMetaJson))
        {
            return Array.Empty<SmartCubeSolvePhaseStatistic>();
        }

        try
        {
            using var document = JsonDocument.Parse(solveMetaJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("phases", out var phasesElement)
                || phasesElement.ValueKind != JsonValueKind.Array)
            {
                return Array.Empty<SmartCubeSolvePhaseStatistic>();
            }

            var builders = new List<PhaseStatisticBuilder>();
            foreach (var phaseElement in phasesElement.EnumerateArray())
            {
                if (phaseElement.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var name = ReadString(phaseElement, "name");
                var moveCount = ReadInt(phaseElement, "moveCount");
                if (string.IsNullOrWhiteSpace(name) || moveCount <= 0)
                {
                    continue;
                }

                var durationMs = ReadDurationMs(phaseElement);
                AddPhase(builders, name, moveCount, durationMs);
            }

            return builders.Select(builder => builder.Build()).ToArray();
        }
        catch (JsonException)
        {
            return Array.Empty<SmartCubeSolvePhaseStatistic>();
        }
    }

    private static void AddPhase(List<PhaseStatisticBuilder> builders, string name, int moveCount, int durationMs)
    {
        if (name.StartsWith("F2L", StringComparison.Ordinal))
        {
            var f2l = builders.FirstOrDefault(builder => builder.Name == "F2L");
            if (f2l is null)
            {
                f2l = new PhaseStatisticBuilder("F2L");
                builders.Add(f2l);
            }

            f2l.Add(moveCount, durationMs);
            f2l.Children.Add(new SmartCubeSolvePhaseStatistic(
                name,
                durationMs,
                moveCount,
                Array.Empty<SmartCubeSolvePhaseStatistic>()));
            return;
        }

        var existing = builders.FirstOrDefault(builder => builder.Name == name);
        if (existing is null)
        {
            existing = new PhaseStatisticBuilder(name);
            builders.Add(existing);
        }

        existing.Add(moveCount, durationMs);
    }

    private static int ReadDurationMs(JsonElement phaseElement)
    {
        if (TryReadInt(phaseElement, "startMs", out var startMs)
            && TryReadInt(phaseElement, "endMs", out var endMs))
        {
            return Math.Max(0, endMs - startMs);
        }

        return Math.Max(0, ReadInt(phaseElement, "durationMs"));
    }

    private static string ReadString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString() ?? string.Empty
            : string.Empty;
    }

    private static int ReadInt(JsonElement element, string propertyName)
    {
        return TryReadInt(element, propertyName, out var value) ? value : 0;
    }

    private static bool TryReadInt(JsonElement element, string propertyName, out int value)
    {
        if (element.TryGetProperty(propertyName, out var property) && property.TryGetInt32(out value))
        {
            return true;
        }

        value = 0;
        return false;
    }

    private sealed class PhaseStatisticBuilder(string name)
    {
        public string Name { get; } = name;

        public int DurationMs { get; private set; }

        public int MoveCount { get; private set; }

        public List<SmartCubeSolvePhaseStatistic> Children { get; } = new();

        public void Add(int moveCount, int durationMs)
        {
            MoveCount += moveCount;
            DurationMs += durationMs;
        }

        public SmartCubeSolvePhaseStatistic Build()
        {
            return new SmartCubeSolvePhaseStatistic(Name, DurationMs, MoveCount, Children.ToArray());
        }
    }
}

public sealed record SmartCubeSolvePhaseStatistic(
    string Name,
    int DurationMs,
    int MoveCount,
    IReadOnlyList<SmartCubeSolvePhaseStatistic> Children)
{
    public double Tps => DurationMs > 0 ? MoveCount * 1000d / DurationMs : 0d;
}
