using UnityEngine;

public static class InputPacketCodec
{
    public static FrameInputPacket Encode(InputFrame input, int frame, int playerId, uint sequence)
    {
        ushort buttons = (ushort)InputButtons.None;
        if (input.punchLight)
            buttons |= (ushort)InputButtons.PunchLightHeld;
        if (input.punchMedium)
            buttons |= (ushort)InputButtons.PunchMediumHeld;
        if (input.punchHeavy)
            buttons |= (ushort)InputButtons.PunchHeavyHeld;
        if (input.dirt)
            buttons |= (ushort)InputButtons.DirtHeld;
        if (input.punchLightPressed)
            buttons |= (ushort)InputButtons.PunchLightPressed;
        if (input.punchMediumPressed)
            buttons |= (ushort)InputButtons.PunchMediumPressed;
        if (input.punchHeavyPressed)
            buttons |= (ushort)InputButtons.PunchHeavyPressed;
        if (input.dirtPressed)
            buttons |= (ushort)InputButtons.DirtPressed;

        return new FrameInputPacket
        {
            frame = frame,
            playerId = playerId,
            buttonsBitmask = buttons,
            moveX = (sbyte)Mathf.RoundToInt(Mathf.Clamp(input.moveX, -1f, 1f)),
            moveY = (sbyte)Mathf.RoundToInt(Mathf.Clamp(input.moveY, -1f, 1f)),
            sequence = sequence
        };
    }

    public static InputFrame Decode(FrameInputPacket packet)
    {
        InputButtons buttons = (InputButtons)packet.buttonsBitmask;
        return new InputFrame
        {
            moveX = packet.moveX,
            moveY = packet.moveY,
            punchLight = Has(buttons, InputButtons.PunchLightHeld),
            punchMedium = Has(buttons, InputButtons.PunchMediumHeld),
            punchHeavy = Has(buttons, InputButtons.PunchHeavyHeld),
            dirt = Has(buttons, InputButtons.DirtHeld),
            punchLightPressed = Has(buttons, InputButtons.PunchLightPressed),
            punchMediumPressed = Has(buttons, InputButtons.PunchMediumPressed),
            punchHeavyPressed = Has(buttons, InputButtons.PunchHeavyPressed),
            dirtPressed = Has(buttons, InputButtons.DirtPressed)
        };
    }

    public static bool ContentEquals(FrameInputPacket a, FrameInputPacket b)
    {
        return a.frame == b.frame
            && a.playerId == b.playerId
            && a.buttonsBitmask == b.buttonsBitmask
            && a.moveX == b.moveX
            && a.moveY == b.moveY;
    }

    private static bool Has(InputButtons buttons, InputButtons flag)
    {
        return (buttons & flag) != 0;
    }
}
