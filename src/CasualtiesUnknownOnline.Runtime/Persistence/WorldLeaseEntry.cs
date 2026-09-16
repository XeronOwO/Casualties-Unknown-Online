namespace CasualtiesUnknownOnline.Runtime.Persistence;

/// <summary>
/// Who is writing a world folder, and when it last did (§5's writer lease). The
/// owner is a process identity rather than a session one on purpose: what the lease
/// protects is the FOLDER, and the two writers it has to keep apart are two
/// processes, whether they are two game instances on one machine, a copied folder or
/// a folder both opened over a share.
/// </summary>
/// <param name="Owner">The writing process, <c>&lt;machine&gt;:&lt;pid&gt;</c>.</param>
/// <param name="HeartbeatUtc">When that process last wrote the world, in the archive's UTC stamp format.</param>
internal sealed record WorldLeaseEntry(string Owner, string HeartbeatUtc);
