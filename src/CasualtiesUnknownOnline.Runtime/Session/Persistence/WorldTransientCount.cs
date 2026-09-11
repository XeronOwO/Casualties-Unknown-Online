namespace CasualtiesUnknownOnline.Runtime.Session.Persistence;

/// <summary>
/// How much of one in-flight class an owner holds at the cut instant. The owner
/// reports only the KEY and the COUNT: the verdict belongs to
/// <see cref="WorldTransientPolicy"/>, so a new owner cannot invent a policy by
/// registering a counter. A count of zero is the same as not reporting the row.
/// </summary>
/// <param name="Key">One of <see cref="WorldTransientPolicy"/>'s row keys.</param>
/// <param name="Pending">In-flight units of that class right now.</param>
public readonly record struct WorldTransientCount(string Key, int Pending);
