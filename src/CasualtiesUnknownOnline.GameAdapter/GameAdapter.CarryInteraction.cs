using CasualtiesUnknownOnline.GameAdapter.Character;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter;

/// <summary>
/// Direct player-interaction carry/release apply side (partial of
/// <see cref="GameAdapter"/>). The host records the authoritative carry
/// relation and broadcasts <see cref="PlayerCarryStateMsg"/> to every member;
/// this half receives that state and turns the local body into a
/// carrier-following render proxy while it is being carried. The carried body
/// reports its own character/entity state through the ordinary streams, so
/// peers do not need a carry-specific render network — they already see the
/// position the carried client reports.
/// </summary>
public sealed partial class GameAdapter
{
	/// <summary>A carry-state broadcast arrived before the local body existed —
	/// reapplied on the first frame the body is available.</summary>
	private PlayerCarryStateMsg? _pendingCarryState;

	private void OnCarryStateChanged(PlayerCarryStateMsg msg)
	{
		_pendingCarryState = msg;
		var body = _run.LocalBody; // Unity object — ==
		if (body == null)
		{
			return;
		}

		ApplyCarryStateToBody(body, msg);
		_pendingCarryState = null;
	}

	private void ApplyCarryStateToBody(Body body, PlayerCarryStateMsg msg)
	{
		var driver = body.GetComponent<CarriedBodyDriver>();
		if (msg.CarriedSteamId == _session.LocalSteamId)
		{
			if (driver == null) // Unity object — ==
			{
				driver = body.gameObject.AddComponent<CarriedBodyDriver>();
			}

			driver.CarrierSteamId = msg.CarrierSteamId;
			body.standing = false;
			_log.LogInformation("[Carry] local body is carried by {Carrier}.", msg.CarrierSteamId);
			return;
		}

		// A release for the relation this side was carrying (or a stale state
		// for a different carrier) clears the driver if this local body was the
		// carried half of that same relation.
		if (driver != null && driver.CarrierSteamId == msg.CarrierSteamId && msg.CarriedSteamId == 0)
		{
			Object.Destroy(driver);
			_log.LogInformation("[Carry] local body released by {Carrier}.", msg.CarrierSteamId);
		}
	}

	/// <summary>
	/// Drive a carried local body from the carrier's entity state. Called every
	/// frame after the run coordinator refreshes the local body reference. The
	/// carrier entity exists on every side: on the host it is a guest's report
	/// buffer, on a guest it is the host/other-guest relay buffer. The body's
	/// rigidbodies are frozen by BodyPatches while the CarriedBodyDriver is
	/// present, so this transform write is the only mover.
	/// </summary>
	private void UpdateCarriedBody(Body? localBody)
	{
		if (localBody == null) // Unity object — ==
		{
			return;
		}

		if (_pendingCarryState is { } pending)
		{
			ApplyCarryStateToBody(localBody, pending);
			_pendingCarryState = null;
		}

		var driver = localBody.GetComponent<CarriedBodyDriver>();
		if (driver == null || driver.CarrierSteamId == 0)
		{
			return;
		}

		var carrier = _entities.GetRemotePlayer(driver.CarrierSteamId);
		if (carrier is null)
		{
			return;
		}

		var side = carrier.IsRight ? -1f : 1f;
		var up = carrier.Crouching ? 0.5f : 0.9f;
		var offset = new Vector2(0.35f * side, up);
		localBody.transform.position = new Vector3(carrier.Position.X + offset.x, carrier.Position.Y + offset.y, 0f);
		localBody.rb.velocity = new Vector2(carrier.Velocity.X, carrier.Velocity.Y);
		localBody.isRight = carrier.IsRight;
		localBody.standing = false;
		localBody.moveDir = Vector2.zero;
	}
}
