using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session;

namespace CasualtiesUnknownOnline.GameAdapter.Character;

/// <summary>
/// Applies the host-computed continuous-vitals row to the body it belongs to (the 2026-10-18
/// ruling: the host owns the bleeding values, the affected client's own body receives them).
///
/// <para>
/// Only this client's OWN body is written. Another player's row belongs to that player's client;
/// this side reads their values through the ordinary character snapshot, so a row naming someone
/// else is dropped rather than written onto a clone.
/// </para>
///
/// <para>
/// The pump hands the live body over once per frame (<see cref="TrackLocalBody"/>) because the row
/// arrives outside it: without the hand-over a row that lands between two frames could only be
/// applied one frame late, which on a 10 Hz row is visible.
/// </para>
/// </summary>
internal sealed class LocalVitalsApplier(ISessionControl session)
{
	private Body? _localBody;

	/// <summary>The pump's per-frame hand-over of the live local body.</summary>
	internal void TrackLocalBody(Body? localBody) => _localBody = localBody;

	/// <summary>Apply one row: the values the host owns, and nothing terminal.</summary>
	internal void Apply(PlayerVitalsMsg msg)
	{
		if (_localBody == null || msg.SteamId != session.LocalSteamId) // Unity object — ==
		{
			return;
		}

		_localBody.bloodVolume = msg.BloodVolume;
		// The panel's l/m line and the moodles read this derived value; the body's own update
		// recomputes it from the (host-applied) limb values between rows, so the row only has to
		// seed it here.
		_localBody.totalBleedSpeed = msg.TotalBleedSpeed;
		foreach (var row in msg.Limbs)
		{
			if (row.Index < 0 || row.Index >= _localBody.limbs.Length)
			{
				continue;
			}

			var limb = _localBody.limbs[row.Index]; // Unity object — ==
			if (limb != null)
			{
				limb.bleedAmount = row.BleedAmount;
			}
		}
	}
}
