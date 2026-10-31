using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol;

namespace CasualtiesUnknownOnline.Runtime.Session.CharacterData;

/// <summary>
/// The one-shot visual policy every character presentation message shares: a guest REPORTS its own visual to the host, the host BROADCASTS its own to every
/// handshaken member, and an inactive session sends nothing. Reliable, because one animation, landing, sound or collapse is one message — the trigger never rides
/// the snapshot stream.
/// </summary>
/// <remarks>
/// One method rather than four copies: the store carried the same eight-line branch four times (sound, attack animation, landing visual, ragdoll), so a change to
/// the policy — who hears it, what happens in a session without a host, whether it stays reliable — had to be made four times and could drift. The message type is
/// the only thing that differs, so it is the parameter.
/// </remarks>
internal sealed class CharacterVisualPublisher(ISessionControl session, PacketSender sender)
{
	internal void Publish(NetMsg kind, object payload)
	{
		if (!session.SessionActive)
		{
			return;
		}

		if (session.Role == SessionRole.Host)
		{
			sender.SendToAll(
				session.Members.Where(m => m.Handshaken && m.SteamId != session.LocalSteamId).Select(m => m.SteamId),
				kind,
				payload,
				reliable: true);
		}
		else
		{
			sender.Send(session.HostSteamId, kind, payload);
		}
	}
}
