namespace SharpTimer.Bluetooth;

public sealed record SmartCubeCapabilities(
    bool Gyroscope = false,
    bool Battery = false,
    bool Facelets = false,
    bool Hardware = false,
    bool Reset = false);
