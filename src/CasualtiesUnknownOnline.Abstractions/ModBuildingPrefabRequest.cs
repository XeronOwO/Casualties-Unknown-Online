namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// The plain request passed to a registered custom-building prefab hook. It
/// carries only the stable building identity and the vanilla template id used
/// as the runtime base. It deliberately does not contain a Unity GameObject or
/// any game-assembly type: a hook can only return component type names for the
/// Game Adapter to attach to the inactive runtime template.
/// </summary>
public sealed class ModBuildingPrefabRequest
{
	/// <summary>The registered custom building id.</summary>
	public string BuildingId { get; set; } = "";

	/// <summary>The vanilla prefab id used as the runtime template base.</summary>
	public string TemplateId { get; set; } = "";
}
