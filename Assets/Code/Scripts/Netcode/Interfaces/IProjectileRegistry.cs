public interface IProjectileRegistry
{
    int ResolveProjectileTypeId(MoveType moveType);
    MoveType ResolveSourceMoveType(int projectileTypeId);
}
