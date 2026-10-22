using CasualtiesUnknownOnline.Runtime.GameAdapter;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Tests.Fakes;

/// <summary>
/// The receiving client's placement answer for a TEST node: a test host has no game scene, so this stand-in
/// answers with the destination the query named — a body slot as asked, a container as asked. It is the shape
/// the landed give tests were written against (the host's core records the named destination); a case that
/// needs a refusal, a substitution or no answer at all replaces the registration in its own extraRegistrations.
/// </summary>
internal sealed class PermissivePlacementResolver : IRemotePlacementResolver
{
	internal static readonly PermissivePlacementResolver Instance = new();

	public RemotePlacementResolution Resolve(CharacterItemMsg item, int requestedSlot, ulong requestedContainerInstanceId) =>
		requestedContainerInstanceId != 0
			? RemotePlacementResolution.IntoContainer(requestedContainerInstanceId)
			: RemotePlacementResolution.ToSlot(requestedSlot);
}
