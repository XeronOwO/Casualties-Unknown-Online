using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.Items;

/// <summary>
/// Holds a runtime world item's CUO instance id — (local counter, spawner
/// account id), globally unique without host allocation so the spawner can
/// apply its own item immediately (local compute). Attached by the Item.Start
/// hook when a runtime-generated item appears; remote applications attach it
/// first, which is how the hook recognizes them and does not re-report.
/// Generation-time items never carry one — world-gen determinism covers them.
/// </summary>
public sealed class ItemInstanceId : MonoBehaviour
{
	public ulong Id;
}
