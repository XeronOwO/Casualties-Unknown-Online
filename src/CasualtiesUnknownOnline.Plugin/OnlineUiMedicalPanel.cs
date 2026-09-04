using CasualtiesUnknownOnline.Runtime.Session.CharacterData;
using UnityEngine;

namespace CasualtiesUnknownOnline;

/// <summary>
/// The read-only CUO medical panel for one remote player. It renders the full
/// projected character-health snapshot plus limb facts that already ride the
/// 1 Hz character-data stream. It never mutates remote clones or sends
/// authority-changing messages; it is a display surface only.
/// </summary>
internal sealed class OnlineUiMedicalPanel
{
	private const float Width = 560f;
	private const float Height = 640f;
	private const float CloseButtonSize = 24f;
	private const float LabelWidth = 190f;
	private const float ValueWidth = 160f;

	private ulong? _target;
	private Rect _rect;
	private Vector2 _scroll;

	internal bool IsVisible => _target.HasValue;

	internal Rect Bounds => _rect;

	internal void Open(ulong steamId)
	{
		_target = steamId;
		_scroll = Vector2.zero;
	}

	internal void Close() => _target = null;

	internal bool Contains(Vector2 point) => _rect.Contains(point);

	internal void Draw(OnlineUiContext ctx)
	{
		if (_target is not { } target)
		{
			return;
		}

		var evt = Event.current;
		if (evt != null && evt.type == EventType.KeyDown && evt.keyCode == KeyCode.Escape)
		{
			Close();
			evt.Use();
			return;
		}

		var name = ctx.DisplayName(target);
		var rect = new Rect((Screen.width - Width) * 0.5f, (Screen.height - Height) * 0.5f, Width, Height);
		_rect = rect;
		OnlineUiTheme.DrawBackground(rect);
		GUILayout.BeginArea(new Rect(rect.x + 8f, rect.y + 8f, rect.width - 16f, rect.height - 16f));

		GUILayout.BeginHorizontal();
		GUILayout.Label(ctx.F("medical.title", name), OnlineUiTheme.Title());
		GUILayout.FlexibleSpace();
		if (GUILayout.Button("×", OnlineUiTheme.CloseButton(), GUILayout.Width(CloseButtonSize), GUILayout.Height(CloseButtonSize)))
		{
			Close();
		}

		GUILayout.EndHorizontal();

		if (!ctx.Vitals.TryGetMedical(target, out var medical))
		{
			GUILayout.Label(ctx.T("medical.empty"), OnlineUiTheme.MutedLabel());
			GUILayout.EndArea();
			return;
		}

		_scroll = GUILayout.BeginScrollView(_scroll, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
		DrawGeneralSection(ctx, medical);
		DrawNutritionSection(ctx, medical);
		DrawCirculationSection(ctx, medical);
		DrawTraumaSection(ctx, medical);
		DrawStatusSection(ctx, medical);
		DrawLimbSection(ctx, medical);
		GUILayout.EndScrollView();
		GUILayout.EndArea();
	}

	private static void DrawGeneralSection(OnlineUiContext ctx, RemoteMedicalSnapshot medical)
	{
		DrawSectionTitle(ctx, "medical.section.general");
		DrawValue(ctx, "medical.alive", medical.Alive ? ctx.T("common.yes") : ctx.T("common.no"));
		DrawValue(ctx, "medical.conscious", medical.Conscious ? ctx.T("common.yes") : ctx.T("common.no"));
		DrawValue(ctx, "medical.brain_health", medical.BrainHealth);
		DrawValue(ctx, "medical.consciousness", medical.Consciousness);
		DrawValue(ctx, "medical.temperature", medical.Temperature);
	}

	private static void DrawNutritionSection(OnlineUiContext ctx, RemoteMedicalSnapshot medical)
	{
		DrawSectionTitle(ctx, "medical.section.nutrition");
		DrawValue(ctx, "medical.hunger", medical.Hunger);
		DrawValue(ctx, "medical.thirst", medical.Thirst);
		DrawValue(ctx, "medical.stamina", medical.Stamina);
		DrawValue(ctx, "medical.energy", medical.Energy);
		DrawValue(ctx, "medical.happiness", medical.Happiness);
	}

	private static void DrawCirculationSection(OnlineUiContext ctx, RemoteMedicalSnapshot medical)
	{
		DrawSectionTitle(ctx, "medical.section.circulation");
		DrawValue(ctx, "medical.blood_volume", medical.BloodVolume);
		DrawValue(ctx, "medical.blood_oxygen", medical.BloodOxygen);
		DrawValue(ctx, "medical.heart_rate", medical.HeartRate);
		DrawValue(ctx, "medical.respiratory_rate", medical.RespiratoryRate);
		DrawValue(ctx, "medical.blood_pressure", medical.BloodPressure);
		DrawValue(ctx, "medical.blood_vessel_size", medical.BloodVesselSize);
		DrawValue(ctx, "medical.fibrillation", medical.FibrillationProgress);
		DrawValue(ctx, "medical.adrenaline", medical.Adrenaline);
	}

	private static void DrawTraumaSection(OnlineUiContext ctx, RemoteMedicalSnapshot medical)
	{
		DrawSectionTitle(ctx, "medical.section.trauma");
		DrawValue(ctx, "medical.shock", medical.Shock);
		DrawValue(ctx, "medical.sickness", medical.SicknessAmount);
		DrawValue(ctx, "medical.septic_shock", medical.SepticShock);
		DrawValue(ctx, "medical.radiation", medical.RadiationSickness);
		DrawValue(ctx, "medical.internal_bleeding", medical.InternalBleeding);
		DrawValue(ctx, "medical.hemothorax", medical.Hemothorax);
		DrawValue(ctx, "medical.pain_shock", medical.PainShock);
		DrawValue(ctx, "medical.trauma", medical.TraumaAmount);
		DrawValue(ctx, "medical.stroke", medical.StrokeAmount);
		DrawValue(ctx, "medical.venom", medical.VenomCurrent);
		DrawValue(ctx, "medical.wetness", medical.Wetness);
		DrawValue(ctx, "medical.bad_sleep", medical.BadSleepAmount);
		DrawValue(ctx, "medical.dirtyness", medical.Dirtyness);
		DrawValue(ctx, "medical.immunity", medical.Immunity);
	}

	private static void DrawStatusSection(OnlineUiContext ctx, RemoteMedicalSnapshot medical)
	{
		DrawSectionTitle(ctx, "medical.section.status");
		DrawValue(ctx, "medical.disfigured", medical.Disfigured ? ctx.T("common.yes") : ctx.T("common.no"));
		DrawValue(ctx, "medical.eye_gone", medical.EyeGone ? ctx.T("common.yes") : ctx.T("common.no"));
		DrawValue(ctx, "medical.both_eyes_gone", medical.BothEyesGone ? ctx.T("common.yes") : ctx.T("common.no"));
		DrawValue(ctx, "medical.pulmonary_embolism", medical.HasPulmonaryEmbolism ? ctx.T("common.yes") : ctx.T("common.no"));
		DrawValue(ctx, "medical.opiate", medical.OpiateAmount);
		DrawValue(ctx, "medical.sleeping_pills", medical.SleepingPillsAmount);
		DrawValue(ctx, "medical.antidepressants", medical.AntidepressantsAmount);
		DrawValue(ctx, "medical.mindwipe", medical.MindwipeScriptActive ? ctx.T("common.yes") : ctx.T("common.no"));
	}

	private static void DrawLimbSection(OnlineUiContext ctx, RemoteMedicalSnapshot medical)
	{
		DrawSectionTitle(ctx, "medical.section.limbs");
		if (medical.Limbs.Count == 0)
		{
			GUILayout.Label(ctx.T("medical.no_limbs"), OnlineUiTheme.MutedLabel());
			return;
		}

		foreach (var limb in medical.Limbs)
		{
			GUILayout.BeginHorizontal();
			GUILayout.Label(ctx.F("medical.limb_index", limb.Index, LimbMarker(limb)), OnlineUiTheme.Label(), GUILayout.Width(LabelWidth));
			GUILayout.Label(ctx.F("medical.limb_skin", limb.SkinHealth), OnlineUiTheme.MutedLabel(), GUILayout.Width(ValueWidth));
			GUILayout.Label(ctx.F("medical.limb_muscle", limb.MuscleHealth), OnlineUiTheme.MutedLabel(), GUILayout.Width(ValueWidth));
			GUILayout.Label(ctx.F("medical.limb_pain", limb.Pain), OnlineUiTheme.MutedLabel(), GUILayout.Width(ValueWidth));
			GUILayout.Label(ctx.F("medical.limb_bleed", limb.BleedAmount), OnlineUiTheme.MutedLabel(), GUILayout.Width(ValueWidth));
			GUILayout.Label(LimbState(ctx, limb), OnlineUiTheme.MutedLabel());
			GUILayout.EndHorizontal();
		}
	}

	private static string LimbMarker(RemoteLimbSnapshot limb)
	{
		if (limb.IsHead)
		{
			return "H";
		}

		return limb.IsVital ? "V" : limb.Index.ToString();
	}

	private static string LimbState(OnlineUiContext ctx, RemoteLimbSnapshot limb)
	{
		if (limb.Dismembered)
		{
			return ctx.T("medical.state_dismembered");
		}

		if (limb.Broken)
		{
			return ctx.T("medical.state_broken");
		}

		if (limb.Dislocated)
		{
			return ctx.T("medical.state_dislocated");
		}

		if (limb.Infected)
		{
			return ctx.T("medical.state_infected");
		}

		if (limb.BlockedBleeding)
		{
			return ctx.T("medical.state_bleeding_blocked");
		}

		return "";
	}

	private static void DrawSectionTitle(OnlineUiContext ctx, string key)
	{
		GUILayout.Space(6f);
		GUILayout.Label(ctx.T(key), OnlineUiTheme.Section());
	}

	private static void DrawValue(OnlineUiContext ctx, string labelKey, float value)
	{
		GUILayout.BeginHorizontal();
		GUILayout.Label(ctx.T(labelKey), OnlineUiTheme.MutedLabel(), GUILayout.Width(LabelWidth));
		GUILayout.Label(value.ToString("F1"), OnlineUiTheme.Label(), GUILayout.Width(ValueWidth));
		GUILayout.EndHorizontal();
	}

	private static void DrawValue(OnlineUiContext ctx, string labelKey, string value)
	{
		GUILayout.BeginHorizontal();
		GUILayout.Label(ctx.T(labelKey), OnlineUiTheme.MutedLabel(), GUILayout.Width(LabelWidth));
		GUILayout.Label(value, OnlineUiTheme.Label(), GUILayout.Width(ValueWidth));
		GUILayout.EndHorizontal();
	}
}
