using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// The composition default: there is no game scene to capture from (tests, a
/// dedicated composition). It captures nothing and declares no live capture, so
/// the target-body gate answers from this side's own stored character snapshot
/// instead. That snapshot is still this client's own data — the rule this seam
/// exists for is "the target's own client decides", not "the answer must come
/// from a live Body this frame" — so a composition without a game keeps working
/// unchanged.
/// </summary>
public sealed class UnavailableLocalCharacterCapture : ILocalCharacterCapture
{
	/// <summary>False: there is no live scene here, so a null capture means "no live source", not "this body is unreadable right now".</summary>
	public bool HasLiveCapture => false;

	public CharacterDataMsg? CaptureLocal() => null;
}
