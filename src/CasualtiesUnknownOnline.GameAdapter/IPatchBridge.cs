using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter;

/// <summary>
/// The narrow surface Harmony patches may reach. Patch classes are static
/// (Harmony invokes them via reflection) so constructor injection cannot reach
/// them; instead the DI-owned GameAdapter binds once at construction and the
/// patches read only this interface — never the service itself (user
/// architecture rule: state belongs to its owner, DI owns behavior).
/// </summary>
internal interface IPatchBridge
{
	bool IsWorldGenIsolated { get; }

	void OnWorldGenerate();

	void OnBlockSet(Vector2Int pos, ushort block);

	void OnBlockDamaged(Vector2 pos, float dmg);

	bool OnGuestStartAttempt();
}
