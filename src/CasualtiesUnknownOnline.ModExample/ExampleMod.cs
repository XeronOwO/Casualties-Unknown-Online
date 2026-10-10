using System.Collections.Generic;
using System.Globalization;
using CasualtiesUnknownOnline.Abstractions;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.ModExample;

/// <summary>
/// The example CUO mod — the Phase 4 Mod API's runtime verification target:
/// its lifecycle and every message it receives are logged, and each received
/// payload is echoed (a guest's report reaches the host's copy; the host
/// broadcasts the echo to every member). Synchronized: both sides must run the
/// same version, so the handshake consistency check admits the pair and a
/// missing copy is refused — exactly what the two-process verification proves.
///
/// It shows both network forms side by side: the anonymous tunnel
/// (<see cref="IModNetwork"/>, one value per mod and one callback, echoed by
/// hand) and a DECLARED packet (<see cref="IModPackets"/>, an id the mod owns
/// with the framework doing the relay). The console command
/// <c>exampleecho &lt;text&gt;</c> drives the declared one on a real session.
///
/// It declares its CONTENT both ways on purpose. <c>example.trophy</c> is
/// registered by code in <see cref="Bind"/>; <c>example.dressing</c> and
/// <c>example.dressing.recipe</c> are classes next to this one carrying
/// <see cref="ModContentAttribute"/> and no registration call anywhere — the
/// framework finds them, takes each kind from the contract the class implements
/// and hands them to the same <see cref="IModContent.TryRegister"/>.
/// <c>exampleweight</c> moves the value the declared dressing COMPUTES its
/// weight and description from, so a rebuilt item table carries the new number
/// instead of the one the declaration started with.
/// </summary>
[CuoMod("cuo.example", "CUO Example", "0.1.0", NetworkMode = NetworkMode.Synchronized,
	Permissions = ModPermission.SendNetworkMessage | ModPermission.RegisterCommand | ModPermission.ExecuteHostAction
		| ModPermission.RegisterContent)]
public sealed class ExampleMod : ICuoMod
{
	/// <summary>The declared echo packet: whoever sends it, the other members run the chain.</summary>
	public const string EchoPacket = "example.echo";

	private IModContext? _context;

	/// <summary>
	/// The number the attribute-declared dressing computes its weight and its
	/// description from — the MOD's value rather than a constant inside the
	/// declaration, which is what makes a computed member worth having. It is
	/// static because the framework instantiates a declaration itself and has no
	/// reference to the mod instance; that is what "the declaration reads the
	/// mod's own configuration" means in practice.
	/// </summary>
	private static float DeclaredWeight { get; set; } = 1.5f;

	public void Bind(IModContext context)
	{
		_context = context;
		context.Content.TryRegister(new ModItemDefinition
		{
			Id = "example.trophy",
			DisplayName = "Example Trophy",
			Description = "Content registered by the CUO example mod.",
			Category = "nospawn",
		});

		// The anonymous tunnel: one callback for every value this mod receives,
		// and the fan-out written by hand. The value is typed, so the echo wraps
		// what arrived instead of re-encoding text.
		context.Network.MessageReceived += (sender, value) =>
		{
			context.Logger.LogInformation("[Example] echo from {Sender}: {Value}", sender, value);
			if (context.Session.IsHost)
			{
				context.Network.Broadcast(ModValue.Map(("echo", value)));
			}
		};

		// The declared packet: the mod owns the message id and the chain, the
		// framework owns the frame, the routing and the relay.
		context.Packets.Register(new ModPacket(EchoPacket, ModPacketSender.AnyMember, ModPacketDelivery.EveryOtherMember,
			new ModPacketHandler(ModPacketStage.Validate, ctx =>
			{
				if (!ctx.Value.TryGetText(out var text) || text.Length == 0)
				{
					ctx.Refuse($"an echo carries non-empty text; this value was {ctx.Value}");
				}
			}),
			new ModPacketHandler(ModPacketStage.Apply, ctx =>
			{
				ctx.Value.TryGetText(out var text);
				context.Logger.LogInformation("[Example] {Packet} from {Sender}: {Text}", EchoPacket, ctx.SenderSteamId, text);
			})));

		context.Commands.Register(new ModCommand("echo", c => $"echo:{string.Join(" ", c.Arguments)}"));
		context.Commands.Register(new ModCommand("whoami", c => $"requester:{c.RequesterSteamId}", isHostAction: true));
		context.ConsoleCommands.Register(new ModConsoleCommand(
			"exampleecho",
			"Send the example's declared echo packet",
			"/exampleecho <text>",
			CommandPermission.Anyone,
			[CommandArgumentKind.Text],
			EchoFromConsole));
		context.ConsoleCommands.Register(new ModConsoleCommand(
			"exampleweight",
			"Set the weight the attribute-declared Example Dressing reports",
			"/exampleweight <weight>",
			CommandPermission.Anyone,
			[CommandArgumentKind.Number],
			SetDeclaredWeightFromConsole));
		context.Ui.Register("example", "CUO Example", window =>
		{
			window.Label($"session active: {context.Session.SessionActive}");
			window.Label($"host: {context.Session.HostSteamId}");
		});
		context.PlayerJoined += id => context.Logger.LogInformation("[Example] player {Id} joined.", id);
		context.PlayerLeft += id => context.Logger.LogInformation("[Example] player {Id} left.", id);
		context.SessionEnded += () => context.Logger.LogInformation("[Example] session ended.");
		context.Logger.LogInformation("[Example] bound (session active: {Active}, host: {Host}).",
			context.Session.SessionActive, context.Session.HostSteamId);
	}

	public void Initialize() => _context?.Logger.LogInformation("[Example] initialized.");

	public void Start() => _context?.Logger.LogInformation("[Example] started.");

	public void Update()
	{
	}

	public void Stop()
	{
	}

	public void Dispose()
	{
	}

	private string? EchoFromConsole(IModConsoleCommandContext ctx)
	{
		if (ctx.Arguments.Count == 0)
		{
			return "usage: /exampleecho <text>";
		}

		var text = string.Join(" ", ctx.Arguments);
		var value = ModValue.Text(text);

		// The packet is declared AnyMember + EveryOtherMember, so the sender's own
		// copy never runs it: a guest reports it (the host and the other members
		// run the chain) and the host broadcasts it to the other members.
		var sent = ctx.Session.IsHost
			? _context!.Packets.Broadcast(EchoPacket, value)
			: _context!.Packets.SendToHost(EchoPacket, value);

		return sent
			? ctx.Session.IsHost
				? $"broadcast \"{text}\" to the other members"
				: $"reported \"{text}\" — the host and the other members run the chain"
			: "the send was refused locally — see the log";
	}

	private string? SetDeclaredWeightFromConsole(IModConsoleCommandContext ctx)
	{
		if (ctx.Arguments.Count == 0
			|| !float.TryParse(ctx.Arguments[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var weight)
			|| weight < 0f)
		{
			return "usage: /exampleweight <weight>";
		}

		DeclaredWeight = weight;
		_context!.Logger.LogInformation(
			"[Example] the declared dressing now reports weight {Weight}; the next item-table rebuild carries it.", weight);
		return $"the declared dressing reports weight {weight} from the next item-table rebuild";
	}

	/// <summary>
	/// The dressing the framework declares, registers and materializes with no
	/// call from this mod: the class implements exactly one kind contract
	/// (<see cref="IModItemDefinition"/>), which IS its kind, and two of its
	/// members are computed from the mod's own value rather than stored.
	///
	/// It is NESTED in this class on purpose: this assembly declares two mods, and
	/// a declaration that is not nested in a mod would have no single owner. The
	/// sibling <see cref="ExampleMachineMod"/> shows the other half of the same
	/// rule — an assembly with ONE mod owns its top-level declarations.
	/// </summary>
	[ModContent]
	public sealed class ExampleDressing : IModItemDefinition
	{
		public string Id => "example.dressing";

		public string Kind => ModContentKind.Item;

		public int SchemaVersion => 1;

		public string DisplayName => "Example Dressing";

		public string Description => $"Declared by attribute; the mod now reports weight {DeclaredWeight:0.##}.";

		public string Category => "nospawn";

		public float Weight => DeclaredWeight;

		public int Value => 10;

		public bool Usable => false;

		public bool UsableWithLmb => false;

		public IModItemWearable? Wearable => null;

		public bool DestroyAtZeroCondition => false;

		public string Tags => "dressing,medicine";

		public int SpawnFrequency => 1;

		public string TemplateId => "";

		public List<string> SpawnComponents => [];

		public Dictionary<string, string> CustomData => [];

		public float? WorldSpawnPerChunk => null;

		public ModItemDropSource? DropSources => null;

		public IModItemContainer? Container => null;

		public IModItemBattery? Battery => null;

		public IModItemLight? Light => null;

		public IModItemTool? Tool => null;

		public IModItemGun? Gun => null;

		public float DecayMinutes => 0f;

		public IModItemVisual? Visual => null;

		/// <summary>The vanilla label the dressing provides, so a vanilla recipe that asks for a dressing matches this item.</summary>
		public List<IModCraftingQuality> Qualities => [new ModCraftingQuality { Id = "dressing" }];
	}

	/// <summary>
	/// The recipe that makes the declared dressing out of a vanilla ripped
	/// dressing — declared the same way, so the whole path from a class next to
	/// the mod to a row in the game's recipe table has no registration call in it.
	/// </summary>
	[ModContent]
	public sealed class ExampleDressingRecipe : IModRecipeDefinition
	{
		public string Id => "example.dressing.recipe";

		public string Kind => ModContentKind.Recipe;

		public int SchemaVersion => 1;

		public string ResultItemId => "example.dressing";

		public bool ResultIsLiquid => false;

		public int ResultAmount => 1;

		public float ResultCondition => 1f;

		public bool DontDrainResultLiquid => false;

		public int Intelligence => 0;

		public string Category => ModRecipeCategory.Medicine;

		public bool IsRepair => false;

		/// <summary>Any condition is accepted: what the player finds is not this declaration's business.</summary>
		public List<IModRecipeIngredient> Ingredients => [new ModRecipeIngredient { ItemId = "rippeddressing", MinimumCondition = 0f }];
	}
}
