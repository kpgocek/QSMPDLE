namespace QSMPDLE.Web.Features.Gameplay.Models;

/// <summary>The rules a game was played under. Stored in the database and read by the analytics views, so values never change.</summary>
public enum Ruleset
{
    /// <summary>Recorded before rulesets were; the exact rules cannot be reconstructed.</summary>
    Unknown = 0,

    /// <summary>The original five clues.</summary>
    Classic = 1,

    /// <summary>QSMPDLE 2.0: from the first released <see cref="ClueGroup"/> on.</summary>
    V2 = 2
}
