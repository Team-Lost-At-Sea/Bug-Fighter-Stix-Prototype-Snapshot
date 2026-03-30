using System.Collections.Generic;
using System;

public sealed class InputHistoryBuffer
{
    public readonly struct Snapshot
    {
        public readonly HistoryEntry[] entries;
        public readonly int nextWriteIndex;
        public readonly int count;

        public Snapshot(HistoryEntry[] entries, int nextWriteIndex, int count)
        {
            this.entries = entries;
            this.nextWriteIndex = nextWriteIndex;
            this.count = count;
        }
    }

    public readonly struct HistoryEntry
    {
        public readonly InputFrame input;
        public readonly int relativeDirection;

        public HistoryEntry(InputFrame input, bool facingRight)
        {
            this.input = input;
            relativeDirection = ToRelativeDirection(input, facingRight);
        }

        public HistoryEntry(InputFrame input, int relativeDirection)
        {
            this.input = input;
            this.relativeDirection = relativeDirection;
        }
    }

    private readonly HistoryEntry[] entries;
    private int nextWriteIndex;
    private int count;

    public InputHistoryBuffer(int capacity)
    {
        if (capacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(capacity), "Input history capacity must be greater than zero.");

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

    public Snapshot CaptureSnapshot()
    {
        HistoryEntry[] copy = new HistoryEntry[entries.Length];
        Array.Copy(entries, copy, entries.Length);
        return new Snapshot(copy, nextWriteIndex, count);
    }

    public void RestoreSnapshot(Snapshot snapshot)
    {
        if (snapshot.entries == null)
        {
            Clear();
            return;
        }

        Array.Clear(entries, 0, entries.Length);
        int copyCount = Math.Min(entries.Length, snapshot.entries.Length);
        Array.Copy(snapshot.entries, entries, copyCount);
        nextWriteIndex = ClampInt(snapshot.nextWriteIndex, 0, entries.Length - 1);
        count = ClampInt(snapshot.count, 0, entries.Length);
    }

    private static int ClampInt(int value, int min, int max)
    {
        if (value < min)
            return min;

        if (value > max)
            return max;

        return value;
    }
}
