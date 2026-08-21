using System;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.CharacterData;

/// <summary>
/// The read-only vitals view of one remote player character, projected from the
/// latest <see cref="CharacterHealthMsg"/>. Kept deliberately small: the Online
/// UI nameplate needs a compact status line, not the whole physiological save
/// surface. The snapshot is immutable; the service owning the cache stores the
/// latest per SteamId.
/// </summary>
public sealed class RemoteVitalsSnapshot
{
	private RemoteVitalsSnapshot(
		float brainHealth,
		float hunger,
		float thirst,
		float stamina,
		float energy,
		float temperature,
		bool alive,
		bool conscious)
	{
		BrainHealth = brainHealth;
		Hunger = hunger;
		Thirst = thirst;
		Stamina = stamina;
		Energy = energy;
		Temperature = temperature;
		Alive = alive;
		Conscious = conscious;
	}

	/// <summary>The game's primary health value (0-100, Body.cs:3950).</summary>
	public float BrainHealth { get; }

	/// <summary>Hunger/satiety (negative to 125, Body.cs:2978).</summary>
	public float Hunger { get; }

	/// <summary>Thirst (negative to 250, Body.cs:2979).</summary>
	public float Thirst { get; }

	/// <summary>Stamina (0-100-ish, Body.cs:3942).</summary>
	public float Stamina { get; }

	/// <summary>Energy (0-100-ish, Body.cs:3946).</summary>
	public float Energy { get; }

	/// <summary>Body temperature.</summary>
	public float Temperature { get; }

	/// <summary>Derived from brainHealth by the game; carried for diagnostics.</summary>
	public bool Alive { get; }

	/// <summary>Derived by the game; carried for diagnostics.</summary>
	public bool Conscious { get; }

	/// <summary>
	/// Project a wire health block into the display snapshot. A null block means
	/// the sender has no health data yet — callers should fall back to the
	/// entity-state Alive/Conscious flags rather than showing zeroed vitals.
	/// </summary>
	public static RemoteVitalsSnapshot? From(CharacterHealthMsg? health)
	{
		if (health is null)
		{
			return null;
		}

		return new RemoteVitalsSnapshot(
			health.BrainHealth,
			health.Hunger,
			health.Thirst,
			health.Stamina,
			health.Energy,
			health.Temperature,
			health.Alive,
			health.Conscious);
	}

	/// <summary>
	/// The compact Online-UI status line: HP (brain health), hunger, thirst and
	/// stamina. Values are rounded to integers so the nameplate stays readable at
	/// 10 px.
	/// </summary>
	public string ToShortString() =>
		$"HP {Round(BrainHealth)} H {Round(Hunger)} T {Round(Thirst)} St {Round(Stamina)}";

	private static int Round(float value) => (int)Math.Round(value, MidpointRounding.AwayFromZero);
}
