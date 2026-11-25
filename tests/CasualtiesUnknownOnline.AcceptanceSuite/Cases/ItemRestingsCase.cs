using System.Globalization;
using System.Text.Json;

namespace CasualtiesUnknownOnline.AcceptanceSuite.Cases;

/// <summary>
/// The item restings a mid-run cut must keep: an item on the ground, one carried, one inside a
/// carried container — judged from the game's own item state and from the other player's screen,
/// not from a count of whatever the inventory happens to list.
/// </summary>
internal sealed class ItemRestingsCase : AcceptanceCase
{
	public override string Name => "item-restings";

	/// <summary>The items the resting test staged and dropped.</summary>
	public override CaseLeftovers Leaves => CaseLeftovers.Bag | CaseLeftovers.Hands;

	public override async Task RunAsync(CaseContext context, CancellationToken cancellationToken)
	{
		// The kernel's own world set before the drop, so the bandage can be found afterwards by the difference:
		// `item-drop` reports that it created an object, not which id the authority gave it (2026-11-23).
		var before = await context.RecipeAsync("container-read", cancellationToken, ("mode", "host"), ("guest", "")).ConfigureAwait(false);
		context.Read("before", before);
		Require(Boolean(before, "ok"), "the host could not read its own item sets: " + Raw(before));
		var worldBefore = Number(before, "worldCount");

		// A ground item: the game's own factory creates it and its physics lands it.
		var dropped = await context.RecipeAsync("item-drop", cancellationToken,
			("type", "bandage"),
			("height", "1")).ConfigureAwait(false);
		context.Read("dropped", dropped);
		Require(Boolean(dropped, "ok"), "the ground item was not created");

		var after = await context.RecipeAsync("container-read", cancellationToken, ("mode", "host"), ("guest", "")).ConfigureAwait(false);
		context.Read("after", after);
		// The judgement: the bandage is in the game's own world set, exactly one item more than before. A factory
		// call that returns ok but leaves nothing in the world would fail here.
		Require(Number(after, "worldCount") == worldBefore + 1,
			"the dropped bandage is not in the game's world set: "
			+ worldBefore.ToString("0", CultureInfo.InvariantCulture) + " before, "
			+ Number(after, "worldCount").ToString("0", CultureInfo.InvariantCulture) + " after");

		// The carried container is this case's own: an earlier case may have emptied the bag, so the
		// case frees its slots and takes a trashbag before the child goes inside it.
		await context.ClearBodyAsync(cancellationToken).ConfigureAwait(false);
		var trashbag = await context.RecipeAsync("item-provide", cancellationToken,
			("type", "trashbag"),
			("mode", "create")).ConfigureAwait(false);
		context.Read("trashbag", trashbag);
		Require(Boolean(trashbag, "ok"), "the host could not take a trashbag");
		var dogfood = await context.RecipeAsync("item-provide", cancellationToken,
			("type", "dogfood"),
			("mode", "create")).ConfigureAwait(false);
		context.Read("dogfood", dogfood);
		Require(Boolean(dogfood, "ok"), "the host could not take a dogfood");

		// A carried container with a child: the game's own LoadItem puts the dogfood inside the trashbag.
		var filled = await context.RecipeAsync("container-fill", cancellationToken,
			("container", "trashbag"),
			("item", "dogfood")).ConfigureAwait(false);
		context.Read("container", filled);
		// The child may already be inside from an earlier batch; the fill is recorded, the read decides.

		var restings = await context.RecipeAsync("container-read", cancellationToken, ("mode", "host"), ("guest", "")).ConfigureAwait(false);
		context.Read("restings", restings);
		// The discipline the recipe states: a nested item is judged in the contained half, never by its absence
		// from the world half. Both halves must carry something, and the container itself is carried.
		Require(Number(restings, "carriedCount") >= 1, "the carried trashbag is not in the game's carried set: " + Raw(restings));
		Require(Number(restings, "containedCount") >= 1, "the dogfood inside the trashbag is not in the game's contained set: " + Raw(restings));

		// The other player's screen: the member sees the host's carried container and its child as proxies, which is
		// what a second player actually looks at. A count of this screen's own inventory would say nothing about it.
		var seen = await context.MemberRecipeAsync("container-read", cancellationToken, ("mode", "clone"), ("guest", "")).ConfigureAwait(false);
		context.Read("member-view", seen);
		Require(Boolean(seen, "ok"), "the member could not read what its screen shows: " + Raw(seen));
		Require(Number(seen, "itemCount") >= 2,
			"the member's screen does not show the host's two carried items: " + Raw(seen));
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

	private static string Raw(JsonElement? reading) => reading?.GetRawText() ?? "null";

	private static void Require(bool condition, string message)
	{
		if (!condition)
		{
			throw new AssertionException(message);
		}
	}
}
