namespace AskARabbiLIB.Accounts;

/// <summary>Describes occupied places at one admission revision, including unfinished registrations.</summary>
/// <param name="Revision">Revision required by the next atomic reservation.</param>
/// <param name="OccupiedPlaces">Existing accounts plus distinct, not-yet-persisted identities with reservations.</param>
/// <param name="HasPlace">Whether the requested identity already has an account or a reservation.</param>
public sealed record AccountRegistrationSnapshot(long Revision, long OccupiedPlaces, bool HasPlace);
