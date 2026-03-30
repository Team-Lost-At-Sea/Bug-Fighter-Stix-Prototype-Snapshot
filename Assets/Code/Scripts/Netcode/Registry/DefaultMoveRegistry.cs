public sealed class DefaultMoveRegistry : IMoveRegistry
{
    public int GetMoveId(MoveType moveType)
    {
        return (int)moveType;
    }

    public MoveType ResolveMoveType(int moveId)
    {
        if (moveId < 0)
            return MoveType.None;

        return (MoveType)moveId;
    }
}
