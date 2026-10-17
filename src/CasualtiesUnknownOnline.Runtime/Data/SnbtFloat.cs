namespace CasualtiesUnknownOnline.Runtime.Data;

/// <summary>
/// A floating-point number. <see cref="Single"/> is true for an <c>f</c>-suffixed literal and
/// for every float the adapter projects: the writer prints <c>f</c> for it and <c>d</c>
/// otherwise, so the kind survives a read/write round trip.
/// </summary>
public sealed record SnbtFloat(double Value, bool Single = false) : SnbtValue
{
	public override string Kind => "a number";
}
