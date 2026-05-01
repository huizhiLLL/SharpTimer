namespace SharpTimer.Bluetooth;

public sealed record SmartCubeNameFilter(string? Name = null, string? NamePrefix = null)
{
    public bool Matches(string? deviceName)
    {
        if (string.IsNullOrWhiteSpace(deviceName))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(Name)
            && string.Equals(deviceName, Name, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(NamePrefix)
            && deviceName.StartsWith(NamePrefix, StringComparison.OrdinalIgnoreCase);
    }
}
