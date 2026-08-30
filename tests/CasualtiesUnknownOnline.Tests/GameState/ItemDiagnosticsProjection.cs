using System.Collections.Generic;
using CasualtiesUnknownOnline.GameState.Domains.Items;
using CasualtiesUnknownOnline.GameState.Projections;

namespace CasualtiesUnknownOnline.Tests.GameState;

/// <summary>
/// Test-only terminal-fact comparison helper. It was originally a production
/// shadow-differential projection; Phase E moved it out of the GameState kernel
/// project because it is not an authoritative projection and has no production
/// consumers.
/// </summary>
public static class ItemDiagnosticsProjection
{
	public static IReadOnlyDictionary<ulong, ItemTerminalFact> BuildActiveFacts(IEnumerable<ItemState> items)
	{
		var facts = new Dictionary<ulong, ItemTerminalFact>();
		foreach (var item in items)
		{
			if (item.Location.Kind == ItemLocationKind.Terminal)
			{
				continue;
			}

			facts[item.Identity.InstanceId] = ItemTerminalFact.From(item);
		}

		return facts;
	}

	public static ItemTerminalDiff Compare(
		IReadOnlyDictionary<ulong, ItemTerminalFact> expected,
		IReadOnlyDictionary<ulong, ItemTerminalFact> actual,
		bool includeRevision = true)
	{
		var differences = new List<string>();
		foreach (var pair in expected)
		{
			if (!actual.TryGetValue(pair.Key, out var actualFact))
			{
				differences.Add($"expected item {pair.Key} at {Format(pair.Value)} but kernel has no active fact");
				continue;
			}

			if (!FactsEqual(pair.Value, actualFact, includeRevision))
			{
				differences.Add($"expected item {pair.Key} {Format(pair.Value)} but kernel has {Format(actualFact)}");
			}
		}

		foreach (var pair in actual)
		{
			if (!expected.TryGetValue(pair.Key, out _))
			{
				differences.Add($"kernel has unexpected active item {pair.Key} {Format(pair.Value)}");
			}
		}

		return new ItemTerminalDiff(differences);
	}

	private static bool FactsEqual(ItemTerminalFact a, ItemTerminalFact b, bool includeRevision) =>
		a.InstanceId == b.InstanceId
			&& a.DefinitionId == b.DefinitionId
			&& a.LocationKind == b.LocationKind
			&& a.Owner == b.Owner
			&& a.ParentItemId == b.ParentItemId
			&& ApproxEqual(a.X, b.X)
			&& ApproxEqual(a.Y, b.Y)
			&& (!includeRevision || a.Revision == b.Revision);

	private static bool ApproxEqual(float a, float b) => System.Math.Abs(a - b) < 0.001f;

	private static string Format(ItemTerminalFact fact) =>
		$"{fact.LocationKind} owner={fact.Owner} parent={fact.ParentItemId} ({fact.X:0.###},{fact.Y:0.###}) rev={fact.Revision}";
}
