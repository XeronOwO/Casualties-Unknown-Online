using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.World;

/// <summary>
/// The host trader's stock state (PURE data — the fields the trade actions
/// read and write): reputation/hostility/values, the private free-field flags,
/// the conversation flags, the stock list and the action gates (build health,
/// min hug reputation). The GameAdapter maps TraderScript ↔ this DTO; the
/// decisions live in <see cref="TradeStockMachine"/>. The stock items are the
/// protocol TraderItemMsg (id/value/preference/bought — the wire shape).
/// </summary>
internal readonly struct TradeStockState
{
	internal float Reputation { get; init; }

	internal float Hostility { get; init; }

	internal float ValueGiven { get; init; }

	internal float TotalValueGiven { get; init; }

	internal int FreeAmount { get; init; }

	internal bool FreeDressing { get; init; }

	internal bool DidHug { get; init; }

	internal bool StartedConvo { get; init; }

	internal bool DidMove { get; init; }

	internal float HaggleAmount { get; init; }

	/// <summary>TraderScript.character — the cannibal (2) takes different action branches.</summary>
	internal int Character { get; init; }

	/// <summary>TraderScript.build.health — a purchase gate (the trader is unusable below 200).</summary>
	internal float BuildHealth { get; init; }

	/// <summary>TraderScript.minHugReputation — the hug's acceptance gate.</summary>
	internal float MinHugReputation { get; init; }

	internal List<TraderItemMsg> Items { get; init; }
}
