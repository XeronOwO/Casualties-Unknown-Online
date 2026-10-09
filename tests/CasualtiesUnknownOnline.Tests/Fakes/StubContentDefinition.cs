using CasualtiesUnknownOnline.Abstractions;

namespace CasualtiesUnknownOnline.Tests.Fakes;

/// <summary>
/// A mod-authored content definition in one line: a mod may implement
/// <see cref="IModContentDefinition"/> itself to register a kind of its own, and
/// the suites that only need "some definition with this id and kind" use this
/// shape instead of a well-known DTO. Its members are settable on purpose — a
/// suite can file it under a kind a provider claims
/// (<see cref="ModContentKind.Item"/>, for example) and drive that provider's
/// type refusal, which is what "not the DTO I read" means now.
/// </summary>
internal sealed class StubContentDefinition(string id, string kind = ModContentKind.Item, int schemaVersion = 1)
	: IModContentDefinition
{
	public string Id { get; set; } = id;

	public string Kind { get; set; } = kind;

	public int SchemaVersion { get; set; } = schemaVersion;
}
