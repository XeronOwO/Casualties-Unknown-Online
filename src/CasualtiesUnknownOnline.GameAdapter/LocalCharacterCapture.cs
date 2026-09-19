using CasualtiesUnknownOnline.GameAdapter.Character;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;
using MapsterMapper;

namespace CasualtiesUnknownOnline.GameAdapter;

/// <summary>
/// Game-backed capture of the LOCAL character on demand — the adapter half of the
/// target-body verdict seam. The Runtime decides that the client owning a body is
/// the one that judges it; this class is what turns "my body, at this instant"
/// into the wire snapshot those judgments read.
/// <para>
/// It captures through the very helper the save cut captures with, so the two can
/// never describe one instant two ways. It is deliberately a standalone service
/// rather than a member of <see cref="GameAdapter"/>: the interaction services
/// depend on this seam, and routing it through the adapter (which depends on
/// <c>IPlayerInteractionControl</c>) would close a constructor cycle — the same
/// reason <c>PlayerInteractionVisibility</c> stands beside the adapter.
/// </para>
/// </summary>
public sealed class LocalCharacterCapture(IMapper mapper) : ILocalCharacterCapture
{
	/// <summary>True: this is the adapter's live scene read, so "no body" is a real answer about the body rather than a missing source.</summary>
	public bool HasLiveCapture => true;

	public CharacterDataMsg? CaptureLocal()
	{
		var camera = PlayerCamera.main;
		var body = camera != null ? camera.body : null; // Unity object — ==
		return body == null
			? null
			: CharacterDataCapture.Capture(mapper, body, out _, CharacterNativeFields.LiveSystem.Instance);
	}
}
