namespace CasualtiesUnknownOnline.Runtime.Session.Commands;

/// <summary>
/// What a completion candidate's hint row says, as a TYPED fact rather than a sentence. The Runtime half of the console decides what the hint means
/// (this candidate is a member name, this one is a banned SteamId, this one completes a command); the client that draws the console decides how to say
/// it in the player's language. Before this type the domain built English strings, so a Chinese client read "Place the item in this body slot index"
/// (user's report, 2026-10-25).
/// </summary>
public enum CommandSuggestionHint
{
	/// <summary>No hint row.</summary>
	None = 0,

	/// <summary>The candidate completes a command: <c>Args[0]</c> is that command's name.</summary>
	Command,

	/// <summary>A session member's display name.</summary>
	MemberName,

	/// <summary>A SteamId, in decimal.</summary>
	SteamId,

	/// <summary>A SteamId, in hexadecimal.</summary>
	SteamIdHex,

	/// <summary>A banned SteamId, in decimal.</summary>
	BannedSteamId,

	/// <summary>A banned SteamId, in hexadecimal.</summary>
	BannedSteamIdHex,

	/// <summary>A selector filter key: <c>Args[0]</c> is the key.</summary>
	FilterBy,

	/// <summary>A selector value: <c>Args[0]</c> is the key, <c>Args[1]</c> the value.</summary>
	FilterValue,

	/// <summary>An empty JSON object.</summary>
	JsonEmpty,

	/// <summary>A JSON object template.</summary>
	JsonTemplate,

	/// <summary>A position the summon command accepts.</summary>
	SummonPosition,

	/// <summary>A body slot index to put the item in.</summary>
	BodySlot,

	/// <summary>A value the gamemode command accepts.</summary>
	GamemodeValue,

	/// <summary>A host rule's current value: <c>Args[0]</c> is the value.</summary>
	HostRuleCurrent,

	/// <summary>A value a host rule accepts: <c>Args[0]</c> is the rule's name.</summary>
	HostRuleValue,

	/// <summary>The <c>/data</c> verb "get": read one item's recorded state.</summary>
	DataGet,

	/// <summary>The <c>/data</c> verb "merge": merge properties into an item's recorded state.</summary>
	DataMerge,

	/// <summary>The <c>/data</c> verb "modify": modify one path of an item's recorded state.</summary>
	DataModify,

	/// <summary>The <c>/data</c> verb "remove": remove one path from an item's recorded state.</summary>
	DataRemove,

	/// <summary>The selector <c>@a</c>: all players.</summary>
	SelectorAllPlayers,

	/// <summary>The selector <c>@p</c>: the nearest player.</summary>
	SelectorNearestPlayer,

	/// <summary>The selector <c>@s</c>: the executing player.</summary>
	SelectorSelf,

	/// <summary>The selector <c>@e</c>: all entities.</summary>
	SelectorAllEntities,

	/// <summary>The selector <c>@r</c>: a random player.</summary>
	SelectorRandomPlayer,

	/// <summary>A resource-location suggestion: <c>Args[0]</c> is its kind, <c>Args[1]</c> its display name.</summary>
	ResourceLocation,

	/// <summary>
	/// The opening of an item state: the candidate inserts <c>{</c> and the next keystroke completes the keys inside it.
	/// </summary>
	ItemState,

	/// <summary>
	/// A sentence from a MOD's own catalogue: <c>Args[0]</c> is the key, the rest are its arguments. The catalogue entry belongs to the mod that registered
	/// the command, so the client that draws the row renders it in its own language exactly like a framework key.
	/// </summary>
	KeyedText,
}
