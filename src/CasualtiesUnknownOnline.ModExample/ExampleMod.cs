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
///
/// It authors BEHAVIOUR too — the one thing a content payload cannot carry, and
/// therefore the one thing registered in code: <c>example.dressing</c> gets a use
/// action and a limb action through <see cref="IModItemActions"/>, and
/// <c>example.tonic</c> a drink effect through <see cref="IModLiquidEffects"/>,
/// each keyed by content this mod owns. Two shapes with NO function sit beside
/// them on purpose, because they are supported rather than broken:
/// <c>example.placebo</c> declares itself usable and registers nothing, and the
/// tonic declares <c>HealthUsable</c> with only its drink half registered. Either
/// way the gesture completes, nothing on the body changes, and the framework says
/// which content asked for a function nobody supplied.
/// </summary>
[CuoMod("cuo.example", "CUO Example", "0.1.0", NetworkMode = NetworkMode.Synchronized,
	Permissions = ModPermission.SendNetworkMessage | ModPermission.RegisterCommand | ModPermission.ExecuteHostAction
		| ModPermission.RegisterContent | ModPermission.AuthorEffects | ModPermission.RegisterLocalCommand)]
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
			// The CODE half of the nested seam: the framework's data class carries the mod's own tool
			// implementation in its Tool member, and the provider builds the game's attack from the interface.
			Tool = ExampleComputedTool.Instance,
			Usable = true,
			UsableWithLmb = true,
			TemplateId = "bandage",
		});

		// A declared flag with NO registered function, on purpose: the item says it is
		// usable and this mod supplies no use action, which is a supported outcome rather
		// than a broken one — the gesture completes, nothing on the body changes, and one
		// line names the content that asked for a function nobody supplied. It is also
		// what a mod sees while it is still writing the action.
		//
		// It also declares NO tags, which is the second supported shape and stays that
		// way: this definition is the regression fixture for a tagless declaration (the
		// `Tags` default), the shape that used to throw inside the game's own
		// `ItemInfo.HasTag`. Adding a tag here would retire the only live case that
		// proves the empty declaration is legal.
		context.Content.TryRegister(new ModItemDefinition
		{
			Id = "example.placebo",
			DisplayName = "Example Placebo",
			Description = "Declared usable with no action registered — the framework reports instead of throwing.",
			Category = "nospawn",
			TemplateId = "bandage",
			Usable = true,
		});

		// The liquid half, in the shape that makes it reachable at all: content first (the
		// registration rail asks the content store who owns the id), then a liquid TILE —
		// a mod liquid has no container item of its own, so the world fluid grid is the
		// one place a player can meet it. SpawnAmount stays 0: this liquid is placed by a
		// run, and one that spawned would change world generation for every other scenario.
		context.Content.TryRegister(new ModLiquidDefinition
		{
			Id = "example.tonic",
			DisplayName = "Example Tonic",
			Description = "Declared by the CUO example mod; drinking it runs the example's own effect.",
			ColorR = 0.35f,
			ColorG = 0.85f,
			ColorB = 0.55f,
			ValuePerLiter = 4f,
			HealthUsable = true,

			// Both flags the game gates `onHealthUse` on are declared, while only the drink
			// half is registered below, so the topical apply AND the injection reach the
			// framework's reporting delegate. Declaring `HealthUsable` alone would leave the
			// injection call site gated off entirely — a flag the game never reads is not a
			// fixture for a call site.
			Injectable = true,
		});
		context.Content.TryRegister(new ModLiquidTileDefinition
		{
			Id = "example.tonic.pool",
			DisplayName = "Example Tonic",
			Description = "The world-fluid face of example.tonic.",
			LiquidId = "example.tonic",
			TintR = 0.35f,
			TintG = 0.85f,
			TintB = 0.55f,
		});

		RegisterAuthoredBehaviour(context);

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
		// The mod's own sentences: an answer a PLAYER reads is rendered by that player's client, so the mod embeds one catalogue per language it speaks.
		SampleCatalogues.Register(context);
		// The shipped NON-LOCAL sample: declared HostRelayed, so a member's line runs on the host and the answer comes back keyed (stage 3 of the console contract).
		context.ConsoleCommands.Register(new ConsoleCommandDefinition(
			"examplehost", "Ask the HOST to echo a line — the answer names who asked", HostEchoFromConsole,
			scope: CommandScope.HostRelayed,
			parameters: [new ConsoleCommandParameter("text")],
			usage: "/examplehost <text>"));
		context.Commands.Register(new ModCommand("whoami", c => $"requester:{c.RequesterSteamId}", isHostAction: true));
		context.ConsoleCommands.Register(new ConsoleCommandDefinition(
			"exampleecho", "Send the example's declared echo packet", EchoFromConsole,
			parameters: [new ConsoleCommandParameter("text")], usage: "/exampleecho <text>"));
		context.ConsoleCommands.Register(new ConsoleCommandDefinition(
			"exampleweight", "Set the weight the attribute-declared Example Dressing reports", SetDeclaredWeightFromConsole,
			parameters: [new ConsoleCommandParameter("weight", new DelegateCompleter(_ => [new CommandCompletion("0.5"), new CommandCompletion("1")]))],
			aliases: ["eweight"], usage: "/exampleweight <weight>"));
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

	/// <summary>
	/// The three registered functions, each standing where a native delegate stands:
	/// the dressing's use action (<c>Body.UseItem</c>), the dressing's limb action (the
	/// game's own wound view on this client, and CUO's cross-player limb-tool chain on
	/// the treated player's own client), and the tonic's drink effect. Each one logs the
	/// state it moved, before and after, because what a fixture owes a run is a reading
	/// somebody else can check.
	///
	/// Every registration answers with a verdict rather than throwing, so this method
	/// reads each one instead of assuming it: a rail that does not hold (the definition
	/// does not declare the flag the native call site is gated on, the content is not
	/// this mod's, the half is already registered) refuses the function and logs why, and
	/// a fixture that reported success while nothing was installed would be worse than one
	/// that failed loudly.
	/// </summary>
	private static void RegisterAuthoredBehaviour(IModContext context)
	{
		var use = context.ItemActions.TryRegisterUse("example.dressing", action =>
		{
			var before = action.Item.Condition;
			action.Item.AddCondition(-0.1f);
			action.Body.AddVital(ModBodyVital.Happiness, 1f);
			context.Logger.LogInformation("[Example] {Item} used: condition {Before:0.###} -> {After:0.###}.",
				action.ItemId, before, action.Item.Condition);
		});

		var limb = context.ItemActions.TryRegisterLimbUse("example.dressing", action =>
		{
			if (action.Limb is not { } treated)
			{
				// The native limb call always names one; this is the compiler's null, not the
				// game's. Reported rather than returned from in silence: a fixture is judged
				// by the lines it wrote, and a branch that writes none is unreadable either way.
				context.Logger.LogWarning("[Example] {Item} limb action ran with no limb — nothing was applied.",
					action.ItemId);
				return;
			}

			var bleedBefore = treated.GetVital(ModLimbVital.Bleed);
			treated.SetVital(ModLimbVital.Bleed, 0f);
			treated.AddVital(ModLimbVital.BandageSlow, 0.5f);
			action.Item.AddCondition(-0.1f);
			context.Logger.LogInformation(
				"[Example] {Item} applied to {Limb} (#{Index}): bleed {BleedBefore:0.###} -> {BleedAfter:0.###}, condition {Condition:0.###}.",
				action.ItemId, treated.Name, treated.Index, bleedBefore, treated.GetVital(ModLimbVital.Bleed),
				action.Item.Condition);
		});

		// Only the DRINK half is registered: the tonic's own declaration carries
		// HealthUsable, so applying or injecting it reaches the game's limb call site with
		// no function behind it — the reporting path, exercised on purpose.
		var drink = context.LiquidEffects.TryRegisterDrink("example.tonic", effect =>
		{
			var before = effect.Body.GetVital(ModBodyVital.Thirst);
			effect.Body.AddVital(ModBodyVital.Thirst, -20f);
			context.Logger.LogInformation("[Example] {Liquid} drunk ({Amount:0.#} ml): thirst {Before:0.#} -> {After:0.#}.",
				effect.LiquidId, effect.AmountMl, before, effect.Body.GetVital(ModBodyVital.Thirst));
		});

		context.Logger.LogInformation(
			"[Example] authored behaviour registered — dressing use={Use}, dressing limb={Limb}, tonic drink={Drink}.",
			use, limb, drink);
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

	private ConsoleAnswer HostEchoFromConsole(ICommandInvocation ctx)
	{
		var text = string.Join(" ", ctx.Arguments);
		_context?.Logger.LogInformation("[Example] host echo for {Requester}: {Text}", ctx.RequesterSteamId, text);
		return ConsoleAnswer.Ok($"host echo for {ctx.RequesterSteamId}: {text}", "cuo.example.hostecho.answer", ctx.RequesterSteamId.ToString(), text);
	}


	private ConsoleAnswer EchoFromConsole(ICommandInvocation ctx)
	{
		if (ctx.Arguments.Count == 0)
		{
			return ConsoleAnswer.Refused("usage: /exampleecho <text>");
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
				? ConsoleAnswer.Ok($"broadcast \"{text}\" to the other members")
				: ConsoleAnswer.Ok($"reported \"{text}\" — the host and the other members run the chain")
			: ConsoleAnswer.Refused("the send was refused locally — see the log");
	}

	private ConsoleAnswer SetDeclaredWeightFromConsole(ICommandInvocation ctx)
	{
		if (ctx.Arguments.Count == 0
			|| !float.TryParse(ctx.Arguments[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var weight)
			|| weight < 0f)
		{
			return ConsoleAnswer.Refused("usage: /exampleweight <weight>");
		}

		DeclaredWeight = weight;
		_context!.Logger.LogInformation(
			"[Example] the declared dressing now reports weight {Weight}; the next item-table rebuild carries it.", weight);
		return ConsoleAnswer.Ok($"the declared dressing reports weight {weight} from the next item-table rebuild");
	}

	/// <summary>
	/// The mod's OWN implementation of the nested tool contract: <see cref="IModItemDefinition.Tool"/> is
	/// interface-typed, so a mod that computes a value hands over this class instead of filling in the
	/// framework's sealed data class. The framework reads the interface and builds the game's own attack from
	/// these values; nothing here is the framework's shape.
	///
	/// <para>
	/// It is a plain class, not a content declaration: only a <c>[ModContent]</c> class is a declaration, so
	/// this one needs no owner and no registration.
	/// </para>
	/// </summary>
	public sealed class ExampleComputedTool : IModItemTool
	{
		public static ExampleComputedTool Instance { get; } = new();

		public float Damage => 1000f;

		public float StructuralDamage => 10f;

		public float AttackCooldownMultiplier => 1f;

		public float Distance => 3f;

		public float KnockBack => 0f;

		public float Cooldown => 0.5f;

		public string AttackAnimation => "";

		/// <summary>The value the machine row reads: a custom attack must drain exactly this much stamina.</summary>
		public float StaminaUse => 30f;

		public bool Piercing => false;

		public List<string> SwingSounds => ["knife"];

		public float Volume => 1f;

		public float RotateAmount => 45f;

		public bool PhysicalSwing => true;

		public bool DoAttackAnimation => true;

		public bool MetalMoreDamage => false;

		public float ConditionLossOnHit => 0.01f;
	}

	/// <summary>
	/// An ATTRIBUTE-declared item whose tool member is the mod's own <see cref="ExampleComputedTool"/>: the
	/// scan reads the member, sees an <see cref="IModItemTool"/> that is not the framework's class, and the
	/// game's item table must carry the attack those values build.
	/// </summary>
	[ModContent]
	public sealed class ExampleCleaver : IModItemDefinition
	{
		public string Id => "example.cleaver";

		public string Kind => ModContentKind.Item;

		public int SchemaVersion => 1;

		public string DisplayName => "Example Cleaver";

		public string Description => "Declared by attribute with the mod's own tool implementation.";

		public string Category => "nospawn";

		public float Weight => 2f;

		public int Value => 10;

		public bool Usable => true;

		public bool UsableWithLmb => true;

		public bool UsableOnLimb => false;

		public IModItemWearable? Wearable => null;

		public bool DestroyAtZeroCondition => false;

		public int RecognitionMinimum => 0;

		public bool OnlyHoldInHands => false;

		public bool Combineable => false;

		public bool IgnoreDepression => false;

		public bool ScaleWeightWithCondition => false;

		public bool AutoAttack => false;

		public float JumpHeightMultiplierChange => 0f;

		public float SlotRotation => 0f;

		public ModItemDecayCondition DecayConditions => ModItemDecayCondition.None;

		public string Tags => "tool";

		public int SpawnFrequency => 1;

		public string TemplateId => "bandage";

		public List<string> SpawnComponents => [];

		public Dictionary<string, string> CustomData => [];

		public float? WorldSpawnPerChunk => null;

		public ModItemDropSource? DropSources => null;

		public IModItemContainer? Container => null;

		public IModItemBattery? Battery => null;

		public IModItemLight? Light => null;

		public IModItemTool? Tool => ExampleComputedTool.Instance;

		public IModItemGun? Gun => null;

		public float DecayMinutes => 0f;

		public IModItemVisual? Visual => null;

		public List<IModCraftingQuality> Qualities => [];
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

		/// <summary>
		/// True because this mod authors a use action for it (see
		/// <see cref="RegisterAuthoredBehaviour"/>): the flag is the gate BOTH of the
		/// game's use call sites read, and the registration rail refuses an action the
		/// gate could never reach. The declaration and the registration are one fact
		/// stated twice, which is why the fixture carries both.
		/// </summary>
		public bool Usable => true;

		public bool UsableWithLmb => false;

		/// <summary>
		/// True for the same reason on the limb half, and what makes the game offer the dressing on a limb: CUO's
		/// cross-player limb-tool chain asks exactly this flag plus a non-null limb action.
		/// </summary>
		public bool UsableOnLimb => true;

		/// <summary>The declared garment: placement plus its five statistics, the one slice the wear flow reads.</summary>
		public IModItemWearable? Wearable => new ModItemWearable
		{
			Limb = "UpTorso",
			SlotId = "outertorso",
			CanBeHeld = true,
			Armor = 4f,
			Isolation = 1.2f,
			HitDurabilityLossMultiplier = 0.75f,
			VisualOffset = 5
		};

		public bool DestroyAtZeroCondition => false;

		/// <summary>
		/// Declared as zero on purpose: this is what a definition that wrote its own
		/// name and description asks for, and the framework writes it into the game's
		/// own recognition gate rather than leaving the data class's leftover 2 there.
		/// </summary>
		public int RecognitionMinimum => 0;

		public bool OnlyHoldInHands => false;

		public bool Combineable => false;

		public bool IgnoreDepression => false;

		public bool ScaleWeightWithCondition => false;

		public bool AutoAttack => false;

		public float JumpHeightMultiplierChange => 0f;

		public float SlotRotation => 0f;

		public ModItemDecayCondition DecayConditions => ModItemDecayCondition.None;

		public string Tags => "dressing,medicine";

		public int SpawnFrequency => 1;

		/// <summary>
		/// A vanilla prefab base, so the adapter can materialize a runtime object for
		/// this declaration: without one the dressing is static item info only, and no
		/// acceptance row can read a live instance's own presentation (its hover text,
		/// for example). It is also the shape a real content mod ships.
		/// </summary>
		public string TemplateId => "bandage";

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
