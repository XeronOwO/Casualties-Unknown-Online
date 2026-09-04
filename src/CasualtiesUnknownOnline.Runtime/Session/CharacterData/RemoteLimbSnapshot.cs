using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.CharacterData;

/// <summary>
/// Read-only projection of one remote limb from <see cref="CharacterLimbMsg"/>.
/// The Online UI medical panel renders this without touching the Game Adapter
/// or Unity limb objects.
/// </summary>
public sealed class RemoteLimbSnapshot
{
	private RemoteLimbSnapshot(
		int index,
		float skinHealth,
		float muscleHealth,
		bool broken,
		bool dislocated,
		bool splinted,
		bool infected,
		float infectionAmount,
		float bleedAmount,
		float pain,
		int shrapnel,
		bool blockedBleeding,
		bool dismembered,
		bool isHead,
		bool isVital)
	{
		Index = index;
		SkinHealth = skinHealth;
		MuscleHealth = muscleHealth;
		Broken = broken;
		Dislocated = dislocated;
		Splinted = splinted;
		Infected = infected;
		InfectionAmount = infectionAmount;
		BleedAmount = bleedAmount;
		Pain = pain;
		Shrapnel = shrapnel;
		BlockedBleeding = blockedBleeding;
		Dismembered = dismembered;
		IsHead = isHead;
		IsVital = isVital;
	}

	public int Index { get; }

	public float SkinHealth { get; }

	public float MuscleHealth { get; }

	public bool Broken { get; }

	public bool Dislocated { get; }

	public bool Splinted { get; }

	public bool Infected { get; }

	public float InfectionAmount { get; }

	public float BleedAmount { get; }

	public float Pain { get; }

	public int Shrapnel { get; }

	public bool BlockedBleeding { get; }

	public bool Dismembered { get; }

	public bool IsHead { get; }

	public bool IsVital { get; }

	internal static RemoteLimbSnapshot From(CharacterLimbMsg limb) =>
		new(
			limb.Index,
			limb.SkinHealth,
			limb.MuscleHealth,
			limb.Broken,
			limb.Dislocated,
			limb.Splinted,
			limb.Infected,
			limb.InfectionAmount,
			limb.BleedAmount,
			limb.Pain,
			limb.Shrapnel,
			limb.BlockedBleeding,
			limb.Dismembered,
			limb.IsHead,
			limb.IsVital);
}
