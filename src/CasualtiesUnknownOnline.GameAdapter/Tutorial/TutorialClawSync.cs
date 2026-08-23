using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.Tutorial;
using Microsoft.Extensions.Logging;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.Tutorial;

/// <summary>
/// The tutorial-claw presentation domain: the host captures
/// <see cref="TutorialHandler"/>'s claw flow (handPos / handPosCurrent /
/// material) and publishes it to the Runtime's 20 Hz
/// <see cref="TutorialClawService"/>; a guest that is not running its own
/// course applies the streamed state to its local TutorialHandler and a
/// <see cref="TutorialClawRemoteDriver"/> keeps the arm material aligned.
/// Course state and claw-created props remain per-side by design — this is the
/// presentation stream, not a course-state synchronization.
/// </summary>
internal sealed class TutorialClawSync(
	ITutorialClawControl tutorialClaw,
	ISessionControl session,
	ILogger<TutorialClawSync> log)
{
	private readonly ITutorialClawControl _tutorialClaw = tutorialClaw;
	private readonly ISessionControl _session = session;
	private readonly ILogger<TutorialClawSync> _log = log;

	internal void BindToSession() => _tutorialClaw.TutorialClawStateReceived += OnTutorialClawStateReceived;

	internal void Unbind() => _tutorialClaw.TutorialClawStateReceived -= OnTutorialClawStateReceived;

	/// <summary>Host/solo authority: publish the current claw state every frame;
	/// the Runtime service throttles the actual broadcast to the configured
	/// state-stream cadence.</summary>
	internal void Update()
	{
		if (_session.Role != SessionRole.Host || !_session.SessionActive)
		{
			return;
		}

		var tutorial = TutorialHandler.main;
		if (tutorial == null) // Unity object — ==
		{
			_tutorialClaw.ClearTutorialClawState();
			return;
		}

		_tutorialClaw.PublishTutorialClawState(Capture(tutorial));
	}

	private static TutorialClawStateMsg Capture(TutorialHandler tutorial)
	{
		var grab = tutorial.grabInfo.Item1; // Unity object — ==
		var grabKind = grab != null
			? tutorial.grabInfo.Item2 switch
			{
				1 => TutorialClawStateMsg.GrabItem,
				2 => TutorialClawStateMsg.GrabBuilding,
				_ => TutorialClawStateMsg.GrabBody,
			}
			: TutorialClawStateMsg.GrabNone;

		byte material;
		if (grab != null) // Unity object — ==
		{
			material = TutorialClawStateMsg.MaterialClosed;
		}
		else if (tutorial.armKnifeSpriteOverride)
		{
			material = TutorialClawStateMsg.MaterialKnife;
		}
		else if (!tutorial.blockQueueEmpty)
		{
			material = TutorialClawStateMsg.MaterialPlace;
		}
		else
		{
			material = TutorialClawStateMsg.MaterialOpen;
		}

		var handPos = tutorial.handPos;
		var current = tutorial.handPosCurrent;
		return new TutorialClawStateMsg
		{
			HandPosX = handPos.x,
			HandPosY = handPos.y,
			HandPosCurrentX = current.x,
			HandPosCurrentY = current.y,
			GrabKind = grabKind,
			Material = material,
			ArmKnifeSpriteOverride = tutorial.armKnifeSpriteOverride,
		};
	}

	private void OnTutorialClawStateReceived(TutorialClawStateMsg msg)
	{
		if (_session.Role != SessionRole.Guest)
		{
			return;
		}

		var tutorial = TutorialHandler.main;
		if (tutorial == null) // Unity object — ==
		{
			return;
		}

		// A guest running its own tutorial course owns its claw (per-side course
		// state remains by design). The remote stream is the observer/fallback
		// path for guests without an active course.
		if (tutorial.activeCourse != null) // Unity object — ==
		{
			return;
		}

		tutorial.handPos = new Vector2(msg.HandPosX, msg.HandPosY);
		tutorial.handPosCurrent = new Vector2(msg.HandPosCurrentX, msg.HandPosCurrentY);
		tutorial.armKnifeSpriteOverride = msg.ArmKnifeSpriteOverride;

		var driver = tutorial.GetComponent<TutorialClawRemoteDriver>();
		if (driver == null) // Unity object — ==
		{
			driver = tutorial.gameObject.AddComponent<TutorialClawRemoteDriver>();
		}

		driver.Apply(msg.Material);
		_log.LogDebug("[TutorialClaw] remote state seq {Seq} applied at ({X:F1},{Y:F1}).",
			msg.Seq, msg.HandPosCurrentX, msg.HandPosCurrentY);
	}
}
