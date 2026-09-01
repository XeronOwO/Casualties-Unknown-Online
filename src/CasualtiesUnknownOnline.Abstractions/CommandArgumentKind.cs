namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// The declared shape of one command argument. The command console uses this to
/// decide which suggestion source applies and to keep the hint/usage text in the
/// same place as the command registration. This type lives in Abstractions so
/// mods can declare console-command argument shapes without referencing Runtime
/// internals.
/// </summary>
public enum CommandArgumentKind
{
	None,
	CommandName,
	PlayerOrSteamId,
	SteamId,
	Number,
	Text,
	Json,
	Selector,
	ResourceLocation,
}
