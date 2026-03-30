using System;

[Flags]
public enum InputButtons : ushort
{
    None = 0,
    PunchLightHeld = 1 << 0,
    PunchMediumHeld = 1 << 1,
    PunchHeavyHeld = 1 << 2,
    PunchLightPressed = 1 << 3,
    PunchMediumPressed = 1 << 4,
    PunchHeavyPressed = 1 << 5,
}
