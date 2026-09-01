using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime.Session.Mods;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Mods;

/// <summary>
/// The read-only content catalog base: it enumerates the framework-wide mod
/// content view, filters by kind, resolves a unique kind + id, and reports
/// cross-mod/schema conflicts without interpreting payloads.
/// </summary>
public class ModContentCatalogTests
{
	private static ModContentCatalog CreateCatalog(params ModContentRegistration[] entries) =>
		new(new FakeContentControl(entries), NullLogger<ModContentCatalog>.Instance);

	[Fact]
	public void Catalog_EnumeratesAndFiltersByKind()
	{
		var catalog = CreateCatalog(
			new ModContentRegistration("mod.a", new ModContentDefinition("sword", "item", [1], 1)),
			new ModContentRegistration("mod.a", new ModContentDefinition("potion", "recipe", [2], 1)),
			new ModContentRegistration("mod.b", new ModContentDefinition("axe", "item", [3], 1)));

		Assert.Equal(3, catalog.Entries.Count);
		Assert.Equal(["sword", "axe"], catalog.OfKind("item").Select(e => e.Definition.Id));
		Assert.Equal(["potion"], catalog.OfKind("recipe").Select(e => e.Definition.Id));
		Assert.Empty(catalog.OfKind("building"));
	}

	[Fact]
	public void Catalog_TryResolve_ReturnsSingleMatch()
	{
		var catalog = CreateCatalog(
			new ModContentRegistration("mod.a", new ModContentDefinition("sword", "item", [1], 1)));

		Assert.True(catalog.TryResolve("item", "sword", out var entry));
		Assert.NotNull(entry);
		Assert.Equal("mod.a", entry!.ModId);
		Assert.Equal("sword", entry.Definition.Id);
	}

	[Fact]
	public void Catalog_TryResolve_RefusesUnknownAndAmbiguous()
	{
		var catalog = CreateCatalog(
			new ModContentRegistration("mod.a", new ModContentDefinition("sword", "item", [1], 1)),
			new ModContentRegistration("mod.b", new ModContentDefinition("sword", "item", [2], 1)));

		Assert.False(catalog.TryResolve("item", "missing", out _));
		Assert.False(catalog.TryResolve("item", "sword", out _));
		Assert.True(catalog.HasConflicts);
	}

	[Fact]
	public void Catalog_ReportsDuplicateIdsAcrossMods()
	{
		var catalog = CreateCatalog(
			new ModContentRegistration("mod.a", new ModContentDefinition("sword", "item", [1], 1)),
			new ModContentRegistration("mod.b", new ModContentDefinition("sword", "item", [2], 1)));

		var conflict = Assert.Single(catalog.Conflicts);
		Assert.Equal(ModContentConflictKind.DuplicateId, conflict.ConflictKind);
		Assert.Equal("item", conflict.Kind);
		Assert.Equal("sword", conflict.Id);
		Assert.Equal(["mod.a", "mod.b"], conflict.OwnerModIds);
	}

	[Fact]
	public void Catalog_ReportsSchemaVersionMismatchForSameContent()
	{
		var catalog = CreateCatalog(
			new ModContentRegistration("mod.a", new ModContentDefinition("sword", "item", [1], 1)),
			new ModContentRegistration("mod.b", new ModContentDefinition("sword", "item", [2], 2)));

		var conflicts = catalog.Conflicts;
		Assert.Contains(conflicts, c => c.ConflictKind == ModContentConflictKind.DuplicateId);
		Assert.Contains(conflicts, c => c.ConflictKind == ModContentConflictKind.VersionMismatch);
	}

	[Fact]
	public void Catalog_AllowsSameIdInDifferentKinds()
	{
		var catalog = CreateCatalog(
			new ModContentRegistration("mod.a", new ModContentDefinition("shared", "item", [1], 1)),
			new ModContentRegistration("mod.b", new ModContentDefinition("shared", "recipe", [2], 1)));

		Assert.False(catalog.HasConflicts);
		Assert.True(catalog.TryResolve("item", "shared", out _));
		Assert.True(catalog.TryResolve("recipe", "shared", out _));
	}

	[Fact]
	public void Catalog_EmptyCatalog_HasNoConflicts()
	{
		var catalog = CreateCatalog();

		Assert.Empty(catalog.Entries);
		Assert.False(catalog.HasConflicts);
		Assert.Empty(catalog.Conflicts);
		Assert.False(catalog.TryResolve("item", "missing", out _));
	}

	[Fact]
	public void Catalog_NullArguments_AreRefused()
	{
		var catalog = CreateCatalog();

		Assert.Throws<ArgumentNullException>(() => catalog.OfKind(null!));
		Assert.Throws<ArgumentNullException>(() => catalog.TryResolve(null!, "id", out _));
		Assert.Throws<ArgumentNullException>(() => catalog.TryResolve("item", null!, out _));
	}

	private sealed class FakeContentControl(IReadOnlyList<ModContentRegistration> entries) : IModContentControl
	{
		public IReadOnlyList<ModContentRegistration> Entries => entries;
	}
}
