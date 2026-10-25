using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CasualtiesUnknownOnline.GameState;
using CasualtiesUnknownOnline.GameState.Domains.Items;
using CasualtiesUnknownOnline.Tests.Patching;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Items;

/// <summary>
/// The shared custody predicate of ticket <c>item-custody-single-authority</c> stage 4: `ItemCustodyRows.OwnsCarried` is what a reader
/// asks instead of the arbitration's transfer cache, and the cache is rebuilt from these very rows — so the three cases below are the whole
/// contract, and a reader cannot get a different answer from the two sources.
/// </summary>
[Trait("Category", "Integration")]
public class ItemCustodyRowsTests
{
	private const ulong Local = 11;
	private const ulong Other = 22;

	private static readonly MethodInfo OwnsCarried = (GameAssemblyHost.ResolveType("CasualtiesUnknownOnline.Runtime.Session.Items.ItemCustodyRows")
		?? throw new InvalidOperationException("ItemCustodyRows not found."))
		.GetMethod("OwnsCarried", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
		?? throw new InvalidOperationException("ItemCustodyRows.OwnsCarried not found.");

	[Fact]
	public void ACarriedRow_IsOwnedByItsOwner()
	{
		var rows = Rows((7, ItemLocation.Carried(new ActorId(Other))));

		Assert.True(Owns(rows, Other, 7));
		Assert.False(Owns(rows, Local, 7), "another member's carried row is not this member's");
	}

	[Fact]
	public void AWorldOrTerminalRow_IsNotCarried()
	{
		var rows = Rows((8, ItemLocation.World(0f, 0f, 0)), (9, ItemLocation.Terminal()));

		Assert.False(Owns(rows, Other, 8));
		Assert.False(Owns(rows, Other, 9));
	}

	[Fact]
	public void AnItemWithNoRow_IsNotOwned() => Assert.False(Owns(Rows(), Other, 99));

	private static bool Owns(IReadOnlyDictionary<ulong, ItemState> rows, ulong steamId, ulong itemId) =>
		(bool)OwnsCarried.Invoke(null, [rows, steamId, itemId])!;

	private static Dictionary<ulong, ItemState> Rows(params (ulong Id, ItemLocation Location)[] rows) =>
		rows.ToDictionary(row => row.Id, row => new ItemState(new ItemIdentity(row.Id, "cu:widget"), 1, row.Location));
}
