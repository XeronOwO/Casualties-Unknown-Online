using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.GameAdapter.World;

/// <summary>
/// The spawn-landing presentation (sound + camera shake, WorldGeneration.cs:
/// 3671-3674): deferred while the start gate holds (both would play into the
/// frozen world — the shake's Invoke even runs on real time and is lost
/// before the release), replayed together at release.
/// </summary>
internal sealed class LifePodPresentation(ILogger<LifePodPresentation> log)
{
	private readonly ILogger<LifePodPresentation> _log = log;

	private bool _deferredLifePodSound;
	private bool _replayingLifePodSound;
	private bool _deferredLifePodShake;

	/// <summary>True while the spawn-landing presentation is deferred (played at the gate release).</summary>
	internal bool HasDeferredEffects => _deferredLifePodSound || _deferredLifePodShake;

	/// <summary>True while the deferred sound is being replayed — the Sound.Play patch must not defer it again.</summary>
	internal bool IsReplayingSound => _replayingLifePodSound;

	/// <summary>Deferred by the Sound.Play patch while the start gate holds (it would play into the frozen world).</summary>
	internal void DeferSound()
	{
		_log.LogInformation("[Sound] lifePodHit deferred (gate holds or timeScale 0).");
		_deferredLifePodSound = true;
	}

	/// <summary>Deferred by the LifePodShake patch while the start gate holds (the Invoke fires into the frozen wait).</summary>
	internal void DeferShake()
	{
		_log.LogInformation("[FX] LifePodShake deferred (gate holds).");
		_deferredLifePodShake = true;
	}

	/// <summary>Replay both deferred effects together at the gate release.</summary>
	internal void Replay()
	{
		if (_deferredLifePodSound)
		{
			_deferredLifePodSound = false;
			_log.LogInformation("[Sound] deferred lifePodHit played.");
			if (PlayerCamera.main != null && PlayerCamera.main.body != null) // Unity objects — ==
			{
				_replayingLifePodSound = true;
				try
				{
					Sound.Play("lifePodHit", PlayerCamera.main.body.transform.position, true, false, null, 1f, 1f, false, false);
				}
				finally
				{
					_replayingLifePodSound = false;
				}
			}
		}

		if (_deferredLifePodShake)
		{
			_deferredLifePodShake = false;
			_log.LogInformation("[FX] deferred LifePodShake played.");
			if (PlayerCamera.main != null) // Unity object — ==
			{
				PlayerCamera.main.LifePodShake();
			}
		}
	}
}
