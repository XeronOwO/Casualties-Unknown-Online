using System;
using System.Collections.Generic;
using System.Reflection;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CasualtiesUnknownOnline.GameAdapter.Character;

/// <summary>
/// The owner-side capture/apply of the dynamic <c>[Saveable]</c> limb
/// components that Mapster cannot see (SplintLimb, TourniquetScript,
/// ChilledLimb). The wire form reuses <see cref="ComponentStateMsg"/> so the
/// 1 Hz character snapshot, cross-player item-use results and reconnect
/// restores all carry the same component state. It owns no game state — the
/// body/limb remain the owner.
///
/// <para>
/// The apply is TWO different operations and every call site names the one it
/// means; they are never defaulted to each other:
/// </para>
/// <list type="bullet">
/// <item><see cref="ApplyAuthoritative"/> — the body that OWNS the limb, whose
/// state the message describes. The carried list is the truth for the types
/// this codec owns: what it names is added or updated, and what it OMITS is
/// removed. Removal is the half a splint removal cannot work without: the
/// authority clears the component on its stored copy, and with no receiving half
/// the target's local component survived and its own next report carried it
/// straight back (batch <c>20261011-b</c>: the removal never happened, and
/// driving it twice awarded two splints).</item>
/// <item><see cref="ApplyDisplay"/> — a body CUO created and parked as a
/// display-only copy, where the components may carry STATE but must never run
/// native behaviour: the game's own <c>Start</c> registers a wound-view icon
/// whose removal predicate dereferences the limb, so an instance that runs it
/// turns a rebuilt display body into a per-frame exception storm
/// (<c>SplintLimb.Start</c> → <c>OnLimbWound.Update</c>). The instances are
/// therefore created DISABLED, and a limb that does not belong to a display copy
/// is refused outright.</item>
/// </list>
/// </summary>
internal static class LimbComponentStateCodec
{
	/// <summary>
	/// Apply the authority's limb state to a body that owns the limb. See the
	/// type remarks: the message's list is complete for the types this codec
	/// owns, so an omitted type is a REMOVAL.
	/// </summary>
	internal static void ApplyAuthoritative(Limb limb, List<ComponentStateMsg>? states) =>
		ApplyCore(limb, states, display: false);

	/// <summary>
	/// Apply a peer's limb state to a display-only body CUO renders and never
	/// simulates. See the type remarks: the components are inert state holders
	/// here, and a body CUO did not create is refused.
	/// </summary>
	internal static void ApplyDisplay(Limb limb, List<ComponentStateMsg>? states) =>
		ApplyCore(limb, states, display: true);

	internal static List<ComponentStateMsg> Capture(Limb limb)
	{
		var states = new List<ComponentStateMsg>();

		var splint = limb.GetComponent<SplintLimb>();
		if (splint != null) // Unity object — ==
		{
			states.Add(new ComponentStateMsg
			{
				TypeName = nameof(SplintLimb),
				Fields =
				[
					FloatField("condition", splint.condition),
					FloatField("conditionLossMinute", splint.conditionLossMinute),
					StringField("item", splint.item),
				],
			});
		}

		var tourniquet = limb.GetComponent<TourniquetScript>();
		if (tourniquet != null) // Unity object — ==
		{
			states.Add(new ComponentStateMsg
			{
				TypeName = nameof(TourniquetScript),
				Fields =
				[
					FloatField("condition", tourniquet.condition),
					FloatField("timeApplied", tourniquet.timeApplied),
				],
			});
		}

		var chilled = limb.GetComponent<ChilledLimb>();
		if (chilled != null) // Unity object — ==
		{
			states.Add(new ComponentStateMsg
			{
				TypeName = nameof(ChilledLimb),
				Fields =
				[
					FloatField("timeLeft", chilled.timeLeft),
					FloatField("maxTime", chilled.maxTime),
				],
			});
		}

		return states;
	}

	private static void ApplyCore(Limb limb, List<ComponentStateMsg>? states, bool display)
	{
		if (states is null)
		{
			// A null list claims nothing — neither a component to carry nor one to remove — and the apply of
			// "no information" is nothing at all. (The wire cannot express this: the message's list is
			// initialized empty and every CUO-side producer of a limb row fills it from a capture, so an
			// EMPTY list is the complete-and-empty list a removal sends.)
			return;
		}

		if (display && !RemoteMedicalDisplayBody.IsDisplayCopy(limb))
		{
			// The display half's promise — state, never behaviour — holds only on a body CUO parked as a
			// display copy. A live body (a render clone, a real player's own body) reaching this call is a
			// caller defect; refusing here is what keeps it from becoming the storm the game's own
			// components produce when they run on a body CUO only renders.
			return;
		}

		var carried = new HashSet<string>(StringComparer.Ordinal);
		foreach (var state in states)
		{
			carried.Add(state.TypeName);
			switch (state.TypeName)
			{
				case nameof(SplintLimb):
					var splint = limb.GetComponent<SplintLimb>();
					if (splint == null) // Unity object — ==
					{
						splint = limb.gameObject.AddComponent<SplintLimb>();
					}

					if (display)
					{
						// Defence in depth on top of the parked hierarchy: a disabled behaviour receives no
						// Start/Update/OnEnable while its fields stay readable and writable, so a display
						// copy that were somehow activated still could not register the game's own icon.
						// The load-bearing guard for a LIVE body is IsDisplayCopy above — that is the shape
						// the storm came from — and this covers the parked copy underneath it.
						// `tools/acceptance/recipes/limb-component-apply.cs` reproduces the pair on a live
						// client: `mode=add-disabled` then `mode=status` reads the game's own Start unrun,
						// and the same component left enabled reads it run.
						splint.enabled = false;
					}

					RestoreFields(splint, state.Fields);
					break;
				case nameof(TourniquetScript):
					var tourniquet = limb.GetComponent<TourniquetScript>();
					if (tourniquet == null) // Unity object — ==
					{
						tourniquet = limb.gameObject.AddComponent<TourniquetScript>();
					}

					if (display)
					{
						tourniquet.enabled = false;
					}

					RestoreFields(tourniquet, state.Fields);
					break;
				case nameof(ChilledLimb):
					var chilled = limb.GetComponent<ChilledLimb>();
					if (chilled == null) // Unity object — ==
					{
						chilled = limb.gameObject.AddComponent<ChilledLimb>();
					}

					if (display)
					{
						chilled.enabled = false;
					}

					RestoreFields(chilled, state.Fields);
					break;
			}
		}

		RemoveAbsent(limb, carried);
	}

	/// <summary>
	/// The removal half: the types this codec owns that the message does NOT carry
	/// are gone from the limb. The game's own teardown is deliberately not used —
	/// <c>SplintLimb.TakeOff</c> and <c>TourniquetScript.TakeOff</c> instantiate
	/// the removed item and hand it to the limb's owner
	/// (<c>AutoPickUpItem</c>), while the design gives the removed item to the
	/// OPERATOR (the authority grants it) — so the component is destroyed and the
	/// limb's own flags arrive as the message's limb fields.
	/// <para>
	/// <c>DestroyImmediate</c>, not <c>Destroy</c>: the receiver re-reports its
	/// character snapshot in the same call that applies this message
	/// (<c>MedicalOperationApply.ApplyProgress</c> →
	/// <c>ReportInventoryChanged</c>), and a frame-deferred destruction would let
	/// that report capture the component that is on its way out and carry it back
	/// to the authority — the very regression this half exists to close. These
	/// three components have no <c>OnDestroy</c>, so nothing else runs here.
	/// </para>
	/// </summary>
	private static void RemoveAbsent(Limb limb, HashSet<string> carried)
	{
		if (!carried.Contains(nameof(SplintLimb)))
		{
			DestroyIfPresent<SplintLimb>(limb);
		}

		if (!carried.Contains(nameof(TourniquetScript)))
		{
			DestroyIfPresent<TourniquetScript>(limb);
		}

		if (!carried.Contains(nameof(ChilledLimb)))
		{
			DestroyIfPresent<ChilledLimb>(limb);
		}
	}

	private static void DestroyIfPresent<T>(Limb limb)
		where T : Component
	{
		var component = limb.GetComponent<T>();
		if (component != null) // Unity object — ==
		{
			Object.DestroyImmediate(component);
		}
	}

	private static void RestoreFields(Component component, List<ComponentFieldMsg> fields)
	{
		foreach (var field in fields)
		{
			var target = component.GetType().GetField(
				field.Name,
				BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
			if (target is null || target.IsStatic || target.IsInitOnly)
			{
				continue;
			}

			switch (field.Kind)
			{
				case SaveableFieldKind.Float:
					target.SetValue(component, field.FloatValue);
					break;
				case SaveableFieldKind.Int:
					target.SetValue(component, field.IntValue);
					break;
				case SaveableFieldKind.Bool:
					target.SetValue(component, field.BoolValue);
					break;
				case SaveableFieldKind.String:
					target.SetValue(component, field.StringValue);
					break;
				case SaveableFieldKind.StringList:
					target.SetValue(component, field.StringList);
					break;
				case SaveableFieldKind.Enum:
					target.SetValue(component, Enum.ToObject(target.FieldType, field.IntValue));
					break;
			}
		}
	}

	private static ComponentFieldMsg FloatField(string name, float value) => new()
	{
		Name = name,
		Kind = SaveableFieldKind.Float,
		FloatValue = value,
	};

	private static ComponentFieldMsg StringField(string name, string value) => new()
	{
		Name = name,
		Kind = SaveableFieldKind.String,
		StringValue = value,
	};
}
