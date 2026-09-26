using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.Items;

/// <summary>
/// What a received world-item impact event presents on the receiver — a pure
/// decision the Game Adapter executes verbatim, so "which parts of the native
/// presentation a peer replays" is a reviewed table rather than whatever the
/// replay happens to call.
///
/// <para>
/// The native bodies are the source: <c>Item.OnCollisionEnter2D</c> plays the
/// <c>drop</c> clip and the landing block's step sound and spawns
/// <c>DustMini</c> (Item.cs:238-247); <c>PlushScript.OnCollisionEnter2D</c>
/// plays the plush's own squeak and nothing else (PlushScript.cs:17-23).
/// </para>
/// </summary>
public readonly record struct ItemImpactPresentation(
	bool PlaysDropClip,
	bool PlaysLandingStepClip,
	bool SpawnsDust,
	bool PlaysOwnClip)
{
	/// <summary>The presentation a kind replays; an unknown kind presents nothing (forward compatibility with a kind this side does not know).</summary>
	public static ItemImpactPresentation Of(ItemImpactKind kind) => kind switch
	{
		ItemImpactKind.ItemImpact => new(PlaysDropClip: true, PlaysLandingStepClip: true, SpawnsDust: true, PlaysOwnClip: false),
		ItemImpactKind.PlushSqueak => new(PlaysDropClip: false, PlaysLandingStepClip: false, SpawnsDust: false, PlaysOwnClip: true),
		_ => default,
	};
}
