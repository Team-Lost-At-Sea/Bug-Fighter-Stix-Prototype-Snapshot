public sealed class DefaultProjectileRegistry : IProjectileRegistry
{
    public int ResolveProjectileTypeId(MoveType moveType)
    {
        return moveType.IsFireball() ? (int)moveType : 0;
    }

    public MoveType ResolveSourceMoveType(int projectileTypeId)
    {
        if (projectileTypeId <= 0)
            return MoveType.None;

        return (MoveType)projectileTypeId;
    }
}
