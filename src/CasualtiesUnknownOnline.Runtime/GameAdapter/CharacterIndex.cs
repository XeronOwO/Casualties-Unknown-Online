namespace CasualtiesUnknownOnline.Runtime.GameAdapter;

/// <summary>
/// The game's three player characters, named by the index the pre-run screen stores
/// (<c>PreRunScript.characterSelected</c>). The session's character snapshot carries that index as
/// <c>index + 1</c> (0 = not reported — protobuf-net omits 0-valued integers, the rule
/// <c>CharacterDataMsg.HandSlot</c> already follows).
///
/// <para>
/// The index is the game's own spawn table (<c>PlayerCamera.SpawnCharacter</c>, release 1.0:
/// 0 Experiment, 1 Milky, 2 Dune), and this type is the one place that table is written down, so a
/// capture, a clone template and a test cannot drift apart.
/// </para>
/// </summary>
public static class CharacterIndex
{
	/// <summary>The index a snapshot names when it names no character this build knows.</summary>
	public const int Unknown = -1;

	/// <summary>How many characters the game's spawn table has.</summary>
	public const int Count = 3;

	/// <summary>The prefab path the game's own spawn table loads for the index, or null for an unknown index.</summary>
	public static string? PrefabPath(int index) => index switch
	{
		0 => "Special/Characters/Experiment",
		1 => "Special/Characters/Milky",
		2 => "Special/Characters/Dune",
		_ => null,
	};

	/// <summary>The wire value for an index: <c>index + 1</c>, or 0 when the index is unknown.</summary>
	public static int ToWire(int index) => index >= 0 && index < Count ? index + 1 : 0;

	/// <summary>The index a wire value names, or <see cref="Unknown"/> for 0 or a level this build does not know.</summary>
	public static int FromWire(int wire) => wire >= 1 && wire <= Count ? wire - 1 : Unknown;

	/// <summary>The index a body's own object name names (the game instantiates the character's prefab under that name), or <see cref="Unknown"/>.</summary>
	public static int IndexOfName(string? name) => name switch
	{
		"Experiment" => 0,
		"Milky" => 1,
		"Dune" => 2,
		_ => Unknown,
	};

	/// <summary>
	/// The character a body belongs to: its object name first, then the choice the pre-run screen
	/// stored (the fallback for a body whose object name names nothing, e.g. after a mod renamed it).
	/// </summary>
	public static int OfBody(string? bodyName, int storedChoice)
	{
		var byName = IndexOfName(bodyName);
		return byName != Unknown ? byName : (storedChoice >= 0 && storedChoice < Count ? storedChoice : Unknown);
	}
}
