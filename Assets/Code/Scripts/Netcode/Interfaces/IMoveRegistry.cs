public interface IMoveRegistry
{
    int GetMoveId(MoveType moveType);
    MoveType ResolveMoveType(int moveId);
}
