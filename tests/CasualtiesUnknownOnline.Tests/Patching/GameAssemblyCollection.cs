using Xunit;

namespace CasualtiesUnknownOnline.Tests.Patching;

/// <summary>
/// Serialization boundary for every test that writes process-global state in the
/// loaded game/Unity assemblies — for example <c>Item.GlobalItems</c>,
/// <c>ItemLootPool.pool</c>, <c>PlayerCamera.main</c> or
/// <c>RemoteBackpackView._focusedBody</c>. xUnit v2 gives every test class its
/// own collection by default, so two such classes would otherwise run in
/// parallel and race on the same static field (a test that replaces the table
/// between another test's arrange and assert turns that test into a flake).
/// <para>
/// <c>DisableParallelization = true</c> is deliberate: a shared collection alone
/// would only serialize the writers, while a reader in any other collection
/// could still observe a half-applied static write. This collection therefore
/// runs with no other collection in flight. Its total work is a fraction of a
/// second, so the wall-clock cost is negligible. Enforced by the normative gate
/// <c>TestIsolationGateTests.StaticGameStateMutations_JoinTheGameAssemblyCollection</c>.
/// </para>
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class GameAssemblyCollection
{
	public const string Name = "GameAssembly";
}
