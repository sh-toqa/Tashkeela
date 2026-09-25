namespace Tashkeela.Domain.Teams;

/// <summary>A member's permissions within one team. Roles belong to memberships, not users (SRS §2.4).</summary>
public enum TeamRole
{
    Player,
    Manager,
}

/// <summary>FR-TEAM-4: whether a member plays regularly or is called in when regulars are unavailable.</summary>
public enum MemberType
{
    Regular,
    Spare,
}

/// <summary>Sports played by the target market (SRS §2.2).</summary>
public enum Sport
{
    Football,
    Basketball,
    Volleyball,
    Padel,
    Other,
}
