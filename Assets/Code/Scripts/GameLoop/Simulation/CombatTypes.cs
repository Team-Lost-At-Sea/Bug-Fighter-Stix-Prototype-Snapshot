public enum HitLevel
{
    Mid = 0,
    High = 1,
    Low = 2,
    Unblockable = 3,
    AirUnblockable = 4,
}

public enum HitResultType
{
    None = 0,
    Hit = 1,
    Blocked = 2,
    CounterHit = 3,
    CounterHitBlocked = 4,
    Parry = 5,
}

public enum RoundPhase
{
    Fighting = 0,
    RoundOverFreeze = 1,
    MatchOver = 2,
}

public enum RoundResult
{
    None = 0,
    Player1Win = 1,
    Player2Win = 2,
    Draw = 3,
}

public enum RoundEndType
{
    None = 0,
    KO = 1,
    TimeOut = 2,
    Draw = 3,
}

public enum MatchWinner
{
    None = 0,
    Player1 = 1,
    Player2 = 2,
}
