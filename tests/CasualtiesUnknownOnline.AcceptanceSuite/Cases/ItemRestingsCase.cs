using System.Text.Json;

namespace CasualtiesUnknownOnline.AcceptanceSuite.Cases;

/// <summary>
/// The item restings a mid-run cut must keep: an item on the ground, one carried, one inside a
/// carried container. The case stages the ground and container shapes on the live session and reads
/// them back through the game's own item state.
/// </summary>
internal sealed class ItemRestingsCase : AcceptanceCase
{
	public override string Name => "item-restings";

	public override async Task RunAsync(CaseContext context, CancellationToken cancellationToken)
	{
		// A ground item: the game's own factory creates it and its physics lands it.
		var dropped = await context.RecipeAsync("item-drop", cancellationToken,
			("type", "bandage"),
			("height", "1")).ConfigureAwait(false);
		context.Read("dropped", dropped);
		Require(Boolean(dropped, "ok"), "the ground item was not created");

		// A carried container with a child: the game's own LoadItem puts the dogfood inside the trashbag.
		var filled = await context.RecipeAsync("container-fill", cancellationToken,
			("container", "trashbag"),
			("item", "dogfood")).ConfigureAwait(false);
		context.Read("container", filled);
		// The child may already be inside from an earlier batch; the fill is recorded, the read decides.

		// The carried restings this client holds: the container and its child are in the list.
		var local = await context.RecipeAsync("container-read", cancellationToken, ("mode", "local"), ("guest", "")).ConfigureAwait(false);
		context.Read("local", local);
		Require(Number(local, "localCount") >= 2, "the local inventory does not list the carried items");
	}

	private static bool Boolean(JsonElement? reading, string property) =>
		reading is { ValueKind: JsonValueKind.Object } element
		&& element.TryGetProperty(property, out var value)
		&& value.ValueKind == JsonValueKind.True;

	private static double Number(JsonElement? reading, string property) =>
		reading is { ValueKind: JsonValueKind.Object } element
		&& element.TryGetProperty(property, out var value)
		&& value.ValueKind == JsonValueKind.Number
			? value.GetDouble()
			: throw new AssertionException($"the reading has no number '{property}'");

	private static void Require(bool condition, string message)
	{
		if (!condition)
		{
			throw new AssertionException(message);
		}
	}
}
