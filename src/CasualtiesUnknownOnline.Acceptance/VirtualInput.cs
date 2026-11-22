using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using UnityTime = UnityEngine.Time;
using SwingProbe = CasualtiesUnknownOnline.Acceptance.SwingProbePatch;

namespace CasualtiesUnknownOnline.GameAdapter.Acceptance;

/// <summary>
/// The acceptance channel's VIRTUAL MOUSE AND KEYBOARD: the staged state a batch writes, and the lifecycle that installs and removes
/// the patches answering the game's own input queries from it.
///
/// <para>
/// It exists because OS-level input costs the user a permission every batch, and because a row that must see a click, a key or a drag
/// the batch cannot produce with a real device has no other instrument. The answering half lives in
/// <see cref="InputQueryPatches"/> — the postfix on <c>UnityEngine.Input.mousePosition</c>, the prefixes on the three mouse-button
/// methods, and the replacement of the game's <c>Input.GetKey*</c> call sites; this type is what a recipe reaches through HotRepl by
/// the name <c>VirtualInput</c>, so every staged value and every counter a batch reads stays here even where the counter is kept by
/// the answering side.
/// </para>
///
/// <para>
/// WHAT IS PATCHABLE AND WHY (measured on this build's <c>UnityEngine.InputLegacyModule</c>):
/// <c>mousePosition</c> is a MANAGED getter (<c>Input.get_mousePosition_Injected(out Vector3)</c>) — a body the ordinary managed patcher
/// detours. <c>GetMouseButton</c>, <c>GetMouseButtonDown</c> and <c>GetMouseButtonUp</c> are <c>extern</c> +
/// <c>[MethodImpl(InternalCall)]</c>: no IL body, which the managed patcher cannot detour. HarmonyX ships a NATIVE detour backend for
/// exactly that shape, so "no IL" is not a verdict — it is a reason to try the native path and RECORD what the backend answers. The
/// install therefore patches each entry separately and reports per method, so a refusal is named (which method, which exception)
/// rather than guessed from the absence of a body.
/// </para>
///
/// <para>
/// FRAME SEMANTICS: a press is DOWN for exactly the frame it is pressed in and HELD afterwards; a release is UP for exactly the frame
/// it is released in and nothing afterwards. Every query in the same frame answers the same value (the state is stamped, never
/// consumed), because a click that reports down to the game but not to the UI would be a defect the batch could never see. The batch
/// advances the frame itself: <see cref="Press"/> and <see cref="Release"/> stamp the CURRENT <c>Time.frameCount</c> plus one.
/// </para>
///
/// <para>
/// OFF BY DEFAULT AND NEVER A PRODUCT FEATURE: nothing calls <see cref="Install"/> except the acceptance driver's recipe,
/// <see cref="Uninstall"/> puts the real input back, and every prefix answers "not mine" whenever no batch staged a state. A player's
/// session never reaches this type, and a production install does not carry the assembly at all.
/// </para>
/// </summary>
internal static class VirtualInput
{
	private const string HarmonyId = "CasualtiesUnknownOnline.GameAdapter.VirtualInput";

	/// <summary>One button's or key's staged state, with the frame each transition happened in.</summary>
	private sealed class ButtonState
	{
		internal bool Held;
		internal bool DownPending;
		internal int DownFrame = -1;
		internal int UpFrame = -1;
	}

	private static readonly Dictionary<int, ButtonState> Buttons = [];

	/// <summary>
	/// Keys a batch staged for the KEYBOARD (the inventory key, the console key, …): the game reads bindings through `Input.GetKeyDown(KeyCode)`, so a
	/// virtual KEY is the same mechanism as a virtual MOUSE button — one staged state per key, answered by the same three prefixes. Empty until a batch
	/// stages one: every other key is the real keyboard's.
	/// </summary>
	private static readonly Dictionary<KeyCode, ButtonState> Keys = [];

	private static Harmony? _harmony;
	private static Vector3? _pointer;

	/// <summary>True while a staged pointer is in force.</summary>
	internal static bool Enabled => _pointer.HasValue;

	/// <summary>True once the patches are installed.</summary>
	internal static bool Installed => _harmony != null;

	/// <summary>How many button methods the backend accepted (0 — the pointer still works — up to 3).</summary>
	internal static int ButtonPatches => InputQueryPatches.ButtonPatches;

	/// <summary>
	/// Installs the patches and answers one report line per target: the pointer, each button method, every replaced call site, the drag
	/// gesture's own key read, the drag-use cue tracker's read and the key-bind capture. The report IS the result — a target the backend
	/// refuses must be named with its exception, so the next step is decided on evidence. The answering targets belong to
	/// <see cref="InputQueryPatches"/>; this method is the channel's lifecycle and its one place of assembly.
	/// </summary>
	internal static string Install(out bool installed)
	{
		installed = false;
		if (_harmony != null)
		{
			installed = true;
			return "already installed" + (InstallReport is null ? "" : " | " + InstallReport);
		}

		var harmony = new Harmony(HarmonyId);
		var inputReport = InputQueryPatches.Install(harmony, out var failures);
		_harmony = harmony;

		// The drag gesture's own key read, the cue tracker's read and the key-bind capture are their own files: each patches a different
		// target family, and each reports what it did so this report names every patched site in one place.
		var report = inputReport + " | " + DragInputPatch.Install(harmony) + " | " + CueInputPatch.Install(harmony) + " | " + KeybindCapturePatch.Install(harmony);
		installed = failures == 0;
		InstallReport = report;
		return report;
	}

	/// <summary>Removes every patch and the staged state in one step; the real input answers again immediately.</summary>
	internal static void Uninstall()
	{
		_pointer = null;
		Buttons.Clear();
		InputQueryPatches.ResetInstallCensus();
		if (_harmony == null)
		{
			return;
		}

		_harmony.UnpatchSelf();
		_harmony = null;
	}

	/// <summary>Stages the pointer at a screen position (the game's own coordinates: origin bottom-left).</summary>
	internal static void MoveTo(float x, float y) => _pointer = new Vector3(x, y, 0f);

	/// <summary>Drops the staged pointer, so the real one answers without uninstalling the patch.</summary>
	internal static void Clear() => _pointer = null;

	/// <summary>
	/// Presses a button: HELD from now on, DOWN for this frame only. A second press of an already-held button keeps its original press frame, so a
	/// repeated call cannot turn one click into two.
	/// </summary>
	internal static void Press(int button)
	{
		if (!Buttons.TryGetValue(button, out var state))
		{
			state = new ButtonState();
			Buttons[button] = state;
		}

		if (!state.Held)
		{
			state.Held = true;

			// The NEXT frame, not this one: the eval that stages a press can run AFTER the game's own Update in its frame (measured 2026-10-27: a
			// same-frame stamp was never seen by `PlayerCamera`'s `Input.GetKeyDown(attack)` and the click did nothing), so the press is stamped for
			// the frame the game will read next.
			state.DownFrame = UnityTime.frameCount + 1;
			state.UpFrame = -1;
		}
	}

	/// <summary>Releases a button: UP for this frame only, then nothing.</summary>
	internal static void Release(int button)
	{
		if (!Buttons.TryGetValue(button, out var state))
		{
			state = new ButtonState();
			Buttons[button] = state;
		}

		if (state.Held)
		{
			state.Held = false;
			state.UpFrame = UnityTime.frameCount + 1;
		}
	}

	/// <summary>Drops every staged button (the pointer keeps its own staged value).</summary>
	internal static void ClearButtons() => Buttons.Clear();

	/// <summary>
	/// Presses one KEY for the game's own reads: HELD from now on, DOWN on the frame the game reads next. Same stamping rule as a button, and for the
	/// same reason — the eval that stages a press can run after the game's own Update for that frame.
	///
	/// <para>
	/// A press is a PRESS even when the key is already staged as held: the down frame is re-stamped every time. Keeping the original frame (so a repeated
	/// call could not become two clicks) cost a batch — a press staged while the game was not reading (a CUO modal suppresses `PlayerCamera.HandleInput`
	/// by design) left an ancient frame behind, so every later press answered "held" and never "down" (measured 2026-10-28: `keyDownAnswers: 0` across
	/// 23,738 queries, then `radialOpen: true` on the first press after a release).
	/// </para>
	/// </summary>
	internal static void PressKey(KeyCode key)
	{
		if (!Keys.TryGetValue(key, out var state))
		{
			state = new ButtonState();
			Keys[key] = state;
		}

		state.Held = true;
		state.DownPending = true;
		state.DownFrame = UnityTime.frameCount + 1;
		state.UpFrame = -1;
		RecordKey(key, "down");
	}

	/// <summary>Releases one staged key: UP on the frame the game reads next, then nothing.</summary>
	internal static void ReleaseKey(KeyCode key)
	{
		if (!Keys.TryGetValue(key, out var state))
		{
			state = new ButtonState();
			Keys[key] = state;
		}

		if (state.Held)
		{
			state.Held = false;
			state.UpFrame = UnityTime.frameCount + 1;
			RecordKey(key, "up");
		}
	}

	/// <summary>
	/// The last few key edges a batch staged, oldest first, as `Key:down@frame`. A row that saw one speed survive another's press needs to know
	/// whether its own batch re-staged that key or never released it (measured 2026-11-23: the game issued Normal and SuperFast in one frame).
	/// </summary>
	internal static string KeyEdges
	{
		get { lock (_keyEdges) { return string.Join(" ", _keyEdges); } }
	}

	private static readonly Queue<string> _keyEdges = [];

	private static void RecordKey(KeyCode key, string edge)
	{
		lock (_keyEdges)
		{
			_keyEdges.Enqueue($"{key}:{edge}@{UnityTime.frameCount}");
			while (_keyEdges.Count > 24)
			{
				_keyEdges.Dequeue();
			}
		}
	}

	/// <summary>Drops every staged key, so the real keyboard answers again.</summary>
	internal static void ClearKeys() => Keys.Clear();

	/// <summary>How many keys a batch currently stages (the install report and the acceptance reads quote it).</summary>
	internal static int StagedKeyCount => Keys.Count;

	/// <summary>The staged pointer, when a batch has one.</summary>
	internal static bool TryGetStaged(out Vector3 position)
	{
		if (_pointer is { } staged)
		{
			position = staged;
			return true;
		}

		position = default;
		return false;
	}

	/// <summary>What the staged state answers for a button and a query, when a batch staged that button at all.</summary>
	internal static bool TryAnswer(int button, ButtonQuery query, out bool value)
	{
		if (!Buttons.TryGetValue(button, out var state))
		{
			value = false;
			return false;
		}

		value = AnswerFor(state, query);
		return true;
	}

	/// <summary>The same question for a KEY: false when the key is not staged, so the real keyboard answers.</summary>
	internal static bool TryAnswerKey(KeyCode key, ButtonQuery query, out bool value)
	{
		if (!Keys.TryGetValue(key, out var state))
		{
			value = false;
			return false;
		}

		// A DOWN edge answers EVERY reader in its frame, the way Unity's own GetKeyDown does. Two consumers
		// read the same press on this build — the camera's own attack path and the minigame's hand update —
		// and a first-reader-wins stamp left the second one with nothing (measured 2026-11-18: a staged
		// press flipped `handClicking` while `handStartedClicking` stayed false for 25,045 key queries, so
		// the bandage minigame performed zero actions). The edge therefore stays pending through its frame
		// and is cleared once the frame has passed.
		if (query == ButtonQuery.Down)
		{
			if (state.DownPending || state.DownFrame == UnityTime.frameCount)
			{
				value = true;
				return true;
			}

			if (UnityTime.frameCount > state.DownFrame)
			{
				state.DownPending = false;
			}

			value = false;
			return true;
		}

		value = AnswerFor(state, query);
		if (query == ButtonQuery.Held && value)
		{
			RecordHeldAnswer(key);
		}

		return true;
	}

	/// <summary>Which of the three questions a prefix is answering.</summary>
	internal enum ButtonQuery
	{
		Held,
		Down,
		Up,
	}

	/// <summary>How many KEY questions the game asked for a staged key (answered by <see cref="InputQueryPatches"/>).</summary>
	internal static int KeyQueries => InputQueryPatches.KeyQueries;

	/// <summary>How many of those KEY questions the staged state answered with "down".</summary>
	internal static int KeyDownAnswers => InputQueryPatches.KeyDownAnswers;

	/// <summary>How many of those KEY questions the staged state answered "held" (the split the down tally cannot give).</summary>
	internal static int KeyHeldAnswers => InputQueryPatches.KeyHeldAnswers;

	/// <summary>The last key names the seam answered "held" for (see <see cref="InputQueryPatches.HeldTrueLog"/>).</summary>
	internal static string HeldTrueLog => HeldLog;

	/// <summary>
	/// The last key names the seam answered "held" for, recorded where the answer is made so both the public prefixes and
	/// the replaced call sites inside the game's own methods report into the same list (measured 2026-11-22: the game's
	/// movement line reads `GetKey(KeyBinds.GetBind("right"))` and the body still stands).
	/// </summary>
	internal static void RecordHeldAnswer(KeyCode key)
	{
		HeldLog = (HeldLog + " " + key.ToString()).TrimEnd();
		if (HeldLog.Length > 160)
		{
			HeldLog = HeldLog.Substring(HeldLog.Length - 160);
		}
	}

	private static string HeldLog = "";

	/// <summary>
	/// How many times the game actually swung an empty hand. It separates "the held key never reached the swing" from "the
	/// swing happened and missed" — a distinction the damage reading alone cannot make.
	/// </summary>
	internal static long Swings => SwingProbe.Swings;

	/// <summary>
	/// How many times the game's own swing entry ran. It sits one step earlier than <see cref="Swings"/>: a row that reads
	/// zero here knows the attack branch was never taken, while a row that reads a use but no swing knows the hand's path
	/// ended before the swing.
	/// </summary>
	internal static long Uses => SwingProbe.Uses;

	/// <summary>How many times the world took damage at all: a swing that never hurt anything never reached a block.</summary>
	internal static long Hurts => SwingProbe.Hurts;

	/// <summary>What the swing counter's install did, so a row can tell a missing seam from a game that never swung.</summary>
	internal static string SwingSeam => SwingProbe.Seam;

	/// <summary>The gate reading taken inside the game's own attack method at its first entry, or null before that.</summary>
	internal static string? AttackGates => InputQueryPatches.AttackGates;

	internal static string? ClickGates => InputQueryPatches.ClickGates;

	/// <summary>
	/// The report the first install produced, kept so a later reader can still see whether a target was patched, refused or
	/// missing. `Install` answers "already installed" on every call after the first, and the acceptance assembly's own log
	/// lines never reach `BepInEx/logs/latest.log`, so without this the answer is lost the moment the plugin finishes waking.
	/// </summary>
	internal static string? InstallReport { get; private set; }

	/// <summary>How many MOUSE-BUTTON questions the game asked.</summary>
	internal static int ButtonQueries => InputQueryPatches.ButtonQueries;

	/// <summary>How many of those button questions the staged state answered with "down".</summary>
	internal static int ButtonDownAnswers => InputQueryPatches.ButtonDownAnswers;

	/// <summary>How many times <c>PlayerCamera.HandleInput</c> was entered.</summary>
	internal static int HandleInputEntries => InputQueryPatches.HandleInputEntries;

	/// <summary>How many times <c>PlayerCamera.HandleAttacks</c> was entered.</summary>
	internal static int HandleAttacksEntries => InputQueryPatches.HandleAttacksEntries;

	/// <summary>The number of call sites the last replacement rewrote (0 means the install looked at the wrong method).</summary>
	internal static int HandleInputReplacements => InputQueryPatches.HandleInputReplacements;

	/// <summary>The one frame-stamped answer both staged families share: a press is DOWN for its own frame, held after it.</summary>
	private static bool AnswerFor(ButtonState state, ButtonQuery query)
	{
		var frame = UnityTime.frameCount;
		return query switch
		{
			ButtonQuery.Held => state.Held,
			ButtonQuery.Down => state.DownFrame == frame,
			ButtonQuery.Up => state.UpFrame == frame,
			_ => false,
		};
	}
}
