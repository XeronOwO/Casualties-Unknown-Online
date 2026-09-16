namespace CasualtiesUnknownOnline.Runtime.Session.Persistence;

/// <summary>
/// Which world a cut writes into, as the service knows it at the instant the seam
/// asks for one. The trigger never reads the service's identity fields itself: the
/// identity changes over a session (a run starts, a restore adopts another world)
/// and a cut must write into the world it was armed for, not into whatever the
/// field holds a frame later.
/// </summary>
/// <param name="WorldId">The world folder the cut writes into ("" = this host owns no world).</param>
/// <param name="DisplayName">The renameable display name the manifest records.</param>
internal sealed record WorldCutTarget(string WorldId, string DisplayName);
