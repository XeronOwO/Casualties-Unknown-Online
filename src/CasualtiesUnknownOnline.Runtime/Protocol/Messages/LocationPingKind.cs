namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>The visual marker type carried by a location ping.</summary>
public enum LocationPingKind : byte
{
	/// <summary>A soft circle marker (first middle click).</summary>
	Circle = 0,

	/// <summary>An exclamation/alert marker (quick second middle click).</summary>
	Exclamation = 1,
}
