using System.Security.Cryptography;
using Desk.Core.Data;

namespace Desk.Core.Patrol;

// A place an officer must visit. Its NFC tag holds the link
// https://desk.shorehotelapps.com/p/<Code>; tapping a phone on it records the visit.
public class Checkpoint
{
    public int Id { get; set; }
    public int HotelId { get; set; }
    public required string Code { get; set; }            // random, lowercase; in the tag's link
    public required string Name { get; set; }
    public int? AreaId { get; set; }
    public LocationArea? Area { get; set; }
    public string? Instructions { get; set; }            // what to check here
    public bool IsActive { get; set; } = true;
}

public class PatrolRoute
{
    public int Id { get; set; }
    public int HotelId { get; set; }
    public required string Name { get; set; }
    public int ExpectedMinutes { get; set; }
    public bool IsActive { get; set; } = true;
    public List<RouteStop> Stops { get; set; } = [];
}

public class RouteStop
{
    public int Id { get; set; }
    public int RouteId { get; set; }
    public int CheckpointId { get; set; }
    public Checkpoint Checkpoint { get; set; } = null!;
    public int Order { get; set; }
}

public enum RoundStatus { InProgress, Completed, Abandoned }

// One walk of a route by one officer.
public class PatrolRound
{
    public int Id { get; set; }
    public int HotelId { get; set; }
    public int RouteId { get; set; }
    public PatrolRoute Route { get; set; } = null!;
    public int OfficerId { get; set; }
    public DateTime StartedLocal { get; set; }
    public DateTime? EndedLocal { get; set; }
    public RoundStatus Status { get; set; }
    public int Missed { get; set; }                      // set when the round ends
    public string? Notes { get; set; }
}

// A tag tapped (or, without NFC, a checkpoint confirmed by hand - marked Manual).
public class CheckpointScan
{
    public int Id { get; set; }
    public int HotelId { get; set; }
    public int CheckpointId { get; set; }
    public Checkpoint Checkpoint { get; set; } = null!;
    public int? RoundId { get; set; }
    public int OfficerId { get; set; }
    public DateTime ScannedLocal { get; set; }
    public bool Manual { get; set; }
    public bool Problem { get; set; }
    public string? Note { get; set; }
}

public static class PatrolRules
{
    const string Alphabet = "abcdefghjkmnpqrstuvwxyz23456789";   // no look-alikes (l/1, o/0, i)

    // Hard to guess, so a checkpoint cannot be "visited" by typing a made-up link.
    public static string NewCode()
    {
        Span<char> c = stackalloc char[8];
        for (var i = 0; i < c.Length; i++) c[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        return new string(c);
    }

    // Route checkpoints with no scan in the round.
    public static List<int> Missed(IEnumerable<int> routeCheckpoints, IEnumerable<int> scanned)
    {
        var done = scanned.ToHashSet();
        return routeCheckpoints.Where(c => !done.Contains(c)).Distinct().ToList();
    }
}
