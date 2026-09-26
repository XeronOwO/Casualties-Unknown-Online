using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using Microsoft.Extensions.Logging;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.World;

/// <summary>
/// The world-item impact presentation chain: the side that SIMULATES a landing
/// plays the native collision presentation (the <c>drop</c> clip, the landing
/// block's step sound and the <c>DustMini</c> puff, or a plush's squeak), and
/// every other member replays the same presentation at the same position.
///
/// <para>
/// In a live session that side is the host: a guest's world-item copies are
/// non-authoritative local simulations whose collision presentation
/// <c>NonAuthoritativeItemImpactPolicy</c> suppresses, so a guest both hears
/// and sees nothing when a world item lands — its own drops included. The host
/// is therefore the only reporter, and the event is host → guest only.
/// </para>
/// </summary>
internal sealed class WorldItemImpactSync(
	IItemControl items,
	ISessionControl session,
	ILogger<WorldItemImpactSync> log)
{
	private readonly IItemControl _items = items;
	private readonly ISessionControl _session = session;
	private readonly ILogger<WorldItemImpactSync> _log = log;

	internal void BindToSession() => _items.ItemImpactReceived += OnReceived;

	internal void Unbind() => _items.ItemImpactReceived -= OnReceived;

	/// <summary>
	/// The authority side's native collision presentation just ran (the guard's
	/// <c>ShouldReport</c> said so). Report the one-shot so every guest presents
	/// the same landing; solo play reports nothing.
	/// </summary>
	internal void Report(Vector2 position, ItemImpactKind kind, byte soundIndex)
	{
		if (!_session.SessionActive)
		{
			return;
		}

		_items.SendItemImpact(new ItemImpactMsg
		{
			Position = new NetVector2Msg { X = position.x, Y = position.y },
			Kind = kind,
			SoundIndex = soundIndex,
		});

		_log.LogDebug("[ItemImpact] reported kind={Kind} at ({X:F1},{Y:F1}) index={Index}.", kind, position.x, position.y, soundIndex);
	}

	/// <summary>An authority's impact arrived — replay the presentation on this member's own world.</summary>
	private void OnReceived(ulong sender, ItemImpactMsg msg)
	{
		WorldItemImpactReplay.Play(msg);
		_log.LogDebug("[ItemImpact] replayed kind={Kind} at ({X:F1},{Y:F1}) index={Index} from {Sender}.",
			msg.Kind, msg.Position.X, msg.Position.Y, msg.SoundIndex, sender);
	}
}
