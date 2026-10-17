namespace CasualtiesUnknownOnline.GameAdapter.Run;

/// <summary>
/// One member row as a client holds it, as plain values. <see cref="Handshaken"/> is
/// carried because the gate arms on it: a peer whose handshake has not completed is not
/// loading, it is retrying, and naming it would put a name on screen beside a promise
/// that is not true for it.
/// </summary>
internal sealed record SessionWaitMember(ulong SteamId, bool Handshaken, bool InWorld, string DisplayName);
