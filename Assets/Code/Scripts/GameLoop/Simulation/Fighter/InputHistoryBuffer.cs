using System.Collections.Generic;

public sealed class InputHistoryBuffer
{
    public readonly struct HistoryEntry
    {
        public readonly InputFrame input;
        public readonly int relativeDirection;

        public HistoryEntry(InputFrame input, bool facingRight)
        {
            this.input = input;
            relativeDirection = ToRelativeDirection(input, facingRight);
        }
    }

    private readonly HistoryEntry[] entries;
    private int nextWriteIndex;
    private int count;

    public InputHistoryBuffer(int capacity)
    {
        entries = new HistoryEntry[capacity];
    }

    public int Count => count;
    public int Capacity => entries.Length;

    public void Push(InputFrame input, bool facingRight)
    {
        entries[nextWriteIndex] = new HistoryEntry(input, facingRight);
        nextWriteIndex = (nextWriteIndex + 1) % entries.Length;
        count = count < entries.Length ? count + 1 : entries.Length;
    }

    public HistoryEntry GetRecent(int framesAgo)
    {
        if (framesAgo < 0 || framesAgo >= count)
            return default;

        int index = nextWriteIndex - 1 - framesAgo;
        if (index < 0)
            index += entries.Length;

        return entries[index];
    }

    public IEnumerable<HistoryEntry> EnumerateRecentFirst()
    {
        for (int i = 0; i < count; i++)
            yield return GetRecent(i);
    }

    public static int ToRelativeDirection(InputFrame input, bool facingRight)
    {
        int moveX = input.moveX > 0f ? 1 : input.moveX < 0f ? -1 : 0;
        int moveY = input.moveY > 0f ? 1 : input.moveY < 0f ? -1 : 0;

        int forwardRelativeX = facingRight ? moveX : -moveX;

        if (moveY > 0)
        {
            if (forwardRelativeX < 0)
                return 7;
            if (forwardRelativeX > 0)
                return 9;
            return 8;
        }

        if (moveY < 0)
        {
            if (forwardRelativeX < 0)
                return 1;
            if (forwardRelativeX > 0)
                return 3;
            return 2;
        }

        if (forwardRelativeX < 0)
            return 4;
        if (forwardRelativeX > 0)
            return 6;
        return 5;
    }

    public void Clear()
    {
        nextWriteIndex = 0;
        count = 0;
    }
}
