namespace CasualtiesUnknownOnline.GameState.Domains.World;

/// <summary>
/// Discriminator for a typed run-setting value. Protobuf/wire forms use the
/// matching carrier field instead of polymorphic containers.
/// </summary>
public enum RunSettingKind
{
	Int = 1,
	Float = 2,
	Bool = 3,
	String = 4,
}
