using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.Items;

/// <summary>
/// One-frame restore marker for a liquidcentrifuge's <c>CustomItemBehaviour.data[0]</c>
/// cooldown. <c>CustomItemBehaviour.Start</c> initializes that array to 0f every
/// time a fresh prefab is instantiated (CustomItemBehaviour.cs:9-17), which runs
/// AFTER <see cref="ItemStateCodec.RestoreComponentStates"/> has applied a synced
/// cooldown. The marker is added during restore and reapplies the value from
/// Update (after Start) on the next frame, then destroys itself; this keeps the
/// transferred/reconnected cooldown correct without changing the game's own
/// lifecycle.
/// </summary>
internal sealed class LiquidCentrifugeCooldownRestore : MonoBehaviour
{
	internal float Cooldown;

	private void Update()
	{
		var custom = GetComponent<CustomItemBehaviour>();
		if (custom == null) // Unity object — ==
		{
			Destroy(this);
			return;
		}

		custom.data = CustomItemDataState.WithLiquidCentrifugeCooldown(
			CustomItemDataState.LiquidCentrifugeItemId, custom.data, Cooldown);
		Destroy(this);
	}
}
