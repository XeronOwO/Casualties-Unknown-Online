namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// Which native world-item collision presentation an impact event replays.
/// Both are played by the side that SIMULATES the landing (in a live session
/// the host — a guest's world-item copies are non-authoritative and their
/// collision presentation is suppressed), so the event is host → guest only.
/// </summary>
public enum ItemImpactKind : byte
{
	/// <summary><c>Item.OnCollisionEnter2D</c> (Item.cs:238-247, relative velocity &gt; 3):
	/// the <c>drop</c> clip, the landing block's step sound and the <c>DustMini</c> puff.</summary>
	ItemImpact = 1,

	/// <summary><c>PlushScript.OnCollisionEnter2D</c> (PlushScript.cs:17-23, relative velocity &gt; 2):
	/// the plush's own squeak clip (<c>PlushScript.Squeak</c>).</summary>
	PlushSqueak = 2,
}
