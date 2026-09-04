namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// The plain request passed to a registered custom-building instance hook. It
/// carries only the stable building identity, the vanilla template id, and the
/// world transform values the instance is being created at. It deliberately
/// does not contain a Unity GameObject or any game-assembly type: a hook can
/// only return component type names for the Game Adapter to attach before the
/// instance becomes active.
/// </summary>
public sealed class ModBuildingInstanceRequest
{
	/// <summary>The registered custom building id.</summary>
	public string BuildingId { get; set; } = "";

	/// <summary>The vanilla prefab id used as the runtime template base.</summary>
	public string TemplateId { get; set; } = "";

	/// <summary>The world X coordinate where the instance is being created.</summary>
	public float X { get; set; }

	/// <summary>The world Y coordinate where the instance is being created.</summary>
	public float Y { get; set; }

	/// <summary>The Z rotation in degrees applied to the instance.</summary>
	public float Rotation { get; set; }
}
