using SharpTimer.Core.Models;

namespace SharpTimer.Storage;

internal static class StorageValueConverter
{
    public static string ToStorageText(Guid value)
    {
        return value.ToString("D");
    }

    public static string ToStorageText(DateTimeOffset value)
    {
        return value.ToUniversalTime().ToString("O");
    }

    public static Guid ToGuid(string value)
    {
        return Guid.Parse(value);
    }

    public static DateTimeOffset ToDateTimeOffset(string value)
    {
        return DateTimeOffset.Parse(value, null, System.Globalization.DateTimeStyles.RoundtripKind);
    }

    public static long ToDurationMilliseconds(TimeSpan duration)
    {
        return checked((long)Math.Round(duration.TotalMilliseconds, MidpointRounding.AwayFromZero));
    }

    public static TimeSpan ToDuration(long milliseconds)
    {
        return TimeSpan.FromMilliseconds(milliseconds);
    }

    public static int ToPenaltyValue(Penalty penalty)
    {
        return penalty switch
        {
            Penalty.None => 0,
            Penalty.PlusTwo => 1,
            Penalty.Dnf => 2,
            _ => throw new ArgumentOutOfRangeException(nameof(penalty), penalty, "Unknown penalty.")
        };
    }

    public static Penalty ToPenalty(int value)
    {
        return value switch
        {
            0 => Penalty.None,
            1 => Penalty.PlusTwo,
            2 => Penalty.Dnf,
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown penalty.")
        };
    }

    public static int ToSolveSourceValue(SolveSource source)
    {
        return source switch
        {
            SolveSource.Manual => 0,
            SolveSource.SmartCube => 1,
            SolveSource.Imported => 2,
            _ => throw new ArgumentOutOfRangeException(nameof(source), source, "Unknown solve source.")
        };
    }

    public static SolveSource ToSolveSource(int value)
    {
        return value switch
        {
            0 => SolveSource.Manual,
            1 => SolveSource.SmartCube,
            2 => SolveSource.Imported,
            _ => SolveSource.Manual
        };
    }

    public static object ToDbValue(string? value)
    {
        return value is null ? DBNull.Value : value;
    }

    public static object ToDbValue(int? value)
    {
        return value is null ? DBNull.Value : value.Value;
    }

    public static object ToDbValue(double? value)
    {
        return value is null ? DBNull.Value : value.Value;
    }
}
