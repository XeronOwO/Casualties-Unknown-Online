using System.Collections.Generic;

namespace CasualtiesUnknownOnline.GameState.Domains.Items;

/// <summary>One simple-typed field value inside a component state.</summary>
public sealed record ItemComponentField(
	string Name,
	ItemComponentFieldKind Kind,
	float FloatValue,
	int IntValue,
	bool BoolValue,
	string StringValue,
	IReadOnlyList<string> StringList);
