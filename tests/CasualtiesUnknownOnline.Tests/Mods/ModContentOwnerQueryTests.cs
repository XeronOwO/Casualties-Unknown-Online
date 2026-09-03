using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime.Session.Mods;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Mods;

/// <summary>
/// The mod-facing content ownership query: it resolves a unique kind + id to
/// the owning mod, and follows the same ambiguity policy as the runtime content
/// catalog — absent or duplicated content never resolves.
/// </summary>
public class ModContentOwnerQueryTests
{
	[Fact]
	public void OwnerQuery_ReturnsSingleOwner()
	{
		var query = CreateQuery(
			new ModContentRegistration("mod.a", new ModContentDefinition("sword", ModContentKind.Item, [1], 1)),
			new ModContentRegistration("mod.b", new ModContentDefinition("potion", ModContentKind.Recipe, [2], 1)));

		Assert.True(query.TryGetOwner(ModContentKind.Item, "sword", out var owner));
		Assert.Equal("mod.a", owner);
		Assert.True(query.TryGetOwner(ModContentKind.Recipe, "potion", out owner));
		Assert.Equal("mod.b", owner);
	}

	[Fact]
	public void OwnerQuery_ReturnsFalseForUnknown()
	{
		var query = CreateQuery(
			new ModContentRegistration("mod.a", new ModContentDefinition("sword", ModContentKind.Item, [1], 1)));

		Assert.False(query.TryGetOwner(ModContentKind.Item, "missing", out var owner));
		Assert.Equal(string.Empty, owner);
	}

	[Fact]
	public void OwnerQuery_ReturnsFalseForAmbiguous()
	{
		var query = CreateQuery(
			new ModContentRegistration("mod.a", new ModContentDefinition("sword", ModContentKind.Item, [1], 1)),
			new ModContentRegistration("mod.b", new ModContentDefinition("sword", ModContentKind.Item, [2], 1)));

		Assert.False(query.TryGetOwner(ModContentKind.Item, "sword", out var owner));
		Assert.Equal(string.Empty, owner);
	}

	[Fact]
	public void OwnerQuery_DistinguishesSameIdAcrossKinds()
	{
		var query = CreateQuery(
			new ModContentRegistration("mod.a", new ModContentDefinition("shared", ModContentKind.Item, [1], 1)),
			new ModContentRegistration("mod.b", new ModContentDefinition("shared", ModContentKind.Recipe, [2], 1)));

		Assert.True(query.TryGetOwner(ModContentKind.Item, "shared", out var owner));
		Assert.Equal("mod.a", owner);
		Assert.True(query.TryGetOwner(ModContentKind.Recipe, "shared", out owner));
		Assert.Equal("mod.b", owner);
	}

	[Fact]
	public void OwnerQuery_EmptyCatalog_ReturnsFalse()
	{
		var query = CreateQuery();

		Assert.False(query.TryGetOwner(ModContentKind.Item, "sword", out var owner));
		Assert.Equal(string.Empty, owner);
	}

	[Fact]
	public void OwnerQuery_NullArguments_AreRefused()
	{
		var query = CreateQuery();

		Assert.Throws<ArgumentNullException>(() => query.TryGetOwner(null!, "sword", out _));
		Assert.Throws<ArgumentNullException>(() => query.TryGetOwner(ModContentKind.Item, null!, out _));
	}

	private static ModContentOwnerQueryAdapter CreateQuery(params ModContentRegistration[] entries) =>
		new(new FakeContentControl(entries));

	private sealed class FakeContentControl(IReadOnlyList<ModContentRegistration> entries) : IModContentControl
	{
		public IReadOnlyList<ModContentRegistration> Entries => entries;
	}
}
