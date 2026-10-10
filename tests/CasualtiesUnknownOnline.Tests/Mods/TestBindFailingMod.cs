using System;
using CasualtiesUnknownOnline.Abstractions;

namespace CasualtiesUnknownOnline.Tests.Mods;

/// <summary>
/// A mod that registers content and then FAILS to load — the load-failure test's
/// victim, the same shape as <see cref="TestThrowingMod"/>: its presence in the test
/// assembly means every TestNode discovers it, and the mod domain isolates the throw,
/// so no other suite is affected — PROVIDED a failed load leaves nothing behind.
///
/// It exists because a mod's content is registered BEFORE its <c>Bind</c> can fail
/// (a code registration happens inside <c>Bind</c>; the attribute scan runs even
/// earlier), and the framework-wide content view is what the console, the ownership
/// query and both content fingerprints read: a mod that never loaded must not appear
/// in it.
///
/// HostOnly on purpose, and not for realism: discovery lists this mod on every node while
/// the loaded-manifest list never carries it, so a STATE-BEARING declaration would make
/// every peer claim a mod the other side does not run — the handshake's own rejection row —
/// and no other suite could handshake at all. This mod needs
/// <see cref="ModPermission.RegisterContent"/>, and among the local-surface modes only
/// `HostOnly` may declare it.
/// </summary>
[CuoMod("test.bindfailing", "Bind Failing", "1.0.0",
	NetworkMode = NetworkMode.HostOnly,
	Permissions = ModPermission.RegisterContent,
	Namespace = "testbindfail")]
public sealed class TestBindFailingMod : ICuoMod
{
	/// <summary>The content this mod registers before it fails — what a failed load must not leave behind.</summary>
	internal const string Id = "test.bindfailing";

	internal const string RegisteredItemId = "bindfail.item";

	public void Bind(IModContext context)
	{
		context.Content.TryRegister(new ModItemDefinition { Id = RegisteredItemId, DisplayName = "Never Materialized" });
		throw new InvalidOperationException("test.bindfailing always throws in Bind, after it registered its content");
	}

	public void Initialize()
	{
	}

	public void Start()
	{
	}

	public void Update()
	{
	}

	public void Stop()
	{
	}

	public void Dispose()
	{
	}
}
