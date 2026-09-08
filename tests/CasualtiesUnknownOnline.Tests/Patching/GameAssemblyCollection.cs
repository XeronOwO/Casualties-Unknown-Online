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
/// Joining one named collection makes xUnit run these classes strictly one at a
/// time while every other collection keeps running in parallel. Enforced by the
/// normative gate <c>TestIsolationGateTests.StaticGameStateMutations_JoinTheGameAssemblyCollection</c>.
/// </summary>
[CollectionDefinition(Name)]
public sealed class GameAssemblyCollection
{
	public const string Name = "GameAssembly";
}
