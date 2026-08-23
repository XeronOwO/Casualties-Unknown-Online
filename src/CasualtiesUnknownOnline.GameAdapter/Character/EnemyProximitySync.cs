using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.EntitySync;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.GameAdapter.Character;

/// <summary>
/// The enemy-proximity side-effect domain. The game's own Update /
/// OnWillRenderObject callbacks mutate the LOCAL body only
/// (ElderThornbackBehaviour.cs:43-101, XalorisScript.cs:23-31,
/// GrabberPlant.cs:75-90); this coordinator captures the post-effect terminal
/// state at the verified transition edge and sends the dedicated
/// <c>EnemyEffectMsg</c> (never the 1 Hz snapshot). Received effects apply to
/// the victim's clone fact table via <see cref="CharacterDataSync"/>.
/// </summary>
internal sealed class EnemyProximitySync(
	ISessionControl session,
	EnemySyncService enemies,
	CharacterDataSync characterData,
	ILogger<EnemyProximitySync> log)
{
	private readonly ISessionControl _session = session;
	private readonly EnemySyncService _enemies = enemies;
	private readonly CharacterDataSync _characterData = characterData;
	private readonly ILogger<EnemyProximitySync> _log = log;

	internal void BindToSession() => _enemies.EnemyEffectReceived += OnEnemyEffectReceived;

	internal void Unbind() => _enemies.EnemyEffectReceived -= OnEnemyEffectReceived;

	/// <summary>ElderThornbackBehaviour.Update ran its 1 s tick while the local body was inside a proximity field — report the post-tick terminal state.</summary>
	internal void ReportElderHorrorTick(Body body)
	{
		Report(body, new EnemyEffectMsg
		{
			Kind = EnemyEffectKind.ElderHorrorTick,
			HorrifiedLevel = body.horrifiedLevel,
			FocusedLevel = body.focusedLevel,
			Adrenaline = body.adrenaline,
			Energy = body.energy,
			Stamina = body.stamina,
		});
	}

	/// <summary>ElderThornbackBehaviour.OnDestroy rewarded the local player (health <= 0 and within 45 units) — report the post-reward terminal state.</summary>
	internal void ReportElderHorrorDefeat(Body body)
	{
		Report(body, new EnemyEffectMsg
		{
			Kind = EnemyEffectKind.ElderHorrorDefeat,
			HorrifiedLevel = body.horrifiedLevel,
			Happiness = body.happiness,
			Caffeinated = body.caffeinated,
		});
	}

	/// <summary>XalorisScript.OnWillRenderObject added its 0.5 s septic tick to the local body — report the post-tick septic shock.</summary>
	internal void ReportXalorisSepticTick(Body body)
	{
		Report(body, new EnemyEffectMsg
		{
			Kind = EnemyEffectKind.XalorisSepticTick,
			SepticShock = body.septicShock,
		});
	}

	/// <summary>GrabberPlant.Update grabbed the local body (the no-grab → grab transition) — report the post-grab shock/eye-panic terminal state.</summary>
	internal void ReportGrabberGrabbed(Body body)
	{
		Report(body, new EnemyEffectMsg
		{
			Kind = EnemyEffectKind.GrabberGrabbed,
			Shock = body.shock,
			EyePanicTime = body.eyePanicTime,
		});
	}

	private void Report(Body body, EnemyEffectMsg msg)
	{
		if (!_session.SessionActive || body == null) // Unity object — ==
		{
			return;
		}

		msg.VictimSteamId = _session.LocalSteamId;
		_enemies.SendEnemyEffect(msg);
		_log.LogInformation("[EnemyEffect] reported {Kind} for local victim {Victim}.", msg.Kind, msg.VictimSteamId);
	}

	private void OnEnemyEffectReceived(ulong sender, EnemyEffectMsg msg) =>
		_characterData.ApplyEnemyEffect(msg);
}
