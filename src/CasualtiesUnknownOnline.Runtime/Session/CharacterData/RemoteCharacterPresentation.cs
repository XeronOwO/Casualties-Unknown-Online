using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.CharacterData;

/// <summary>
/// The pure, Unity-free remote-character presentation model. It is the single
/// typed shape that describes what a remote display surface is allowed to read
/// and replay: the source snapshot plus the derived presentation-finished
/// states (face, body pose, medical readouts).
///
/// This is NOT a copy of the wire model used for gameplay/restore. It is the
/// presentation seam: source facts are produced by the character-data capture
/// path, then <see cref="State.From(CharacterDataMsg)"/> normalizes them into
/// one immutable model consumed by every remote display applier. This makes the
/// native read-point family explicit instead of leaving each UI to re-derive
/// fields from raw snapshots ad hoc.
/// </summary>
internal static class RemoteCharacterPresentation
{
	/// <summary>
	/// Face presentation: the body-level latches, the FacialExpression child
	/// component fields, the face-driving vitals and the owner's exact mouth
	/// state. These are exactly the fields the native sprite formula and the
	/// remote head-mouth replay read.
	/// </summary>
	internal sealed record FaceState(
		bool Disfigured,
		bool EyeGone,
		bool BothEyesGone,
		int DisfiguredIndex,
		float DisfiguredTimeFullSkin,
		float EyeTimeHealed,
		HeadMouthState HeadMouth,
		float EatTime,
		FacePresentationVitals Vitals)
	{
		internal static FaceState From(CharacterHealthMsg health) => new(
			health.Disfigured,
			health.EyeGone,
			health.BothEyesGone,
			health.DisfiguredIndex,
			health.DisfiguredTimeFullSkin,
			health.EyeTimeHealed,
			health.HeadMouth,
			health.EatTime,
			FacePresentationVitals.From(health));
	}

	/// <summary>
	/// Body-pose presentation: the owner's computed leg-speed multiplier, the
	/// input that drives the weakness/slouch portion of the CrouchAmount
	/// animator parameter on a frozen render clone.
	/// </summary>
	internal sealed record BodyPoseState(float LegSpeedMult)
	{
		internal static BodyPoseState From(CharacterHealthMsg health) =>
			new(health.LegSpeedMult);
	}

	/// <summary>
	/// Medical-display presentation: the fields a native WoundView or moodle
	/// readout needs that are normally produced by a live Body/component Update
	/// and therefore cannot be reconstructed by raw snapshot mapping alone.
	/// </summary>
	internal sealed record MedicalState(
		bool Alive,
		float HeartRate,
		float BloodPressure,
		float RespiratoryRate,
		float OpiateAmount,
		float OpiateTolerance,
		float OpiateReception,
		float ActualOpiateReception,
		float AntidepressantsAmount,
		float AntidepressantsCurrentAmount,
		bool MindwipeScriptPresent)
	{
		internal static MedicalState From(CharacterHealthMsg health) => new(
			health.Alive,
			health.HeartRate,
			health.BloodPressure,
			health.RespiratoryRate,
			health.OpiateAmount,
			health.OpiateTolerance,
			health.OpiateReception,
			health.ActualOpiateReception,
			health.AntidepressantsAmount,
			health.AntidepressantsCurrentAmount,
			health.MindwipeScriptPresent);

		/// <summary>
		/// Native <c>Body.Update</c> recomputes breathing as
		/// <c>alive &amp;&amp; respiratoryRate &gt; 10</c> (Body.cs:2770). This is
		/// the presentation-finished value, not the raw respiratory rate.
		/// </summary>
		public bool Breathing => Alive && RespiratoryRate > 10f;

		/// <summary>
		/// Native WoundView respiratory line reads
		/// <c>Body.respiratoryRateReadout</c>, generated only by the live body's
		/// circulation pass (Body.cs:931); this is the projected readout string.
		/// </summary>
		public string RespiratoryRateReadout =>
			Math.Round(RespiratoryRate * 0.25f).ToString("0") + "/m";
	}

	/// <summary>
	/// One immutable remote-character presentation. It carries the authoritative
	/// source snapshot (<see cref="Source"/>) plus the derived presentation
	/// states. Only this type is allowed to cross into the adapter display
	/// appliers; raw per-field helpers no longer form a separate public path.
	/// </summary>
	internal sealed record State(
		CharacterDataMsg Source,
		FaceState? Face,
		BodyPoseState? BodyPose,
		MedicalState? Medical,
		RemoteInventorySnapshot? Inventory)
	{
		public CharacterHealthMsg? Health => Source.Health;

		public IReadOnlyList<CharacterLimbMsg> Limbs => Source.Limbs;

		public IReadOnlyList<CharacterItemMsg> Items => Source.Items;

		public int HandSlot => Source.HandSlot;

		public static State From(CharacterDataMsg data)
		{
			var health = data.Health;
			return new State(
				data,
				health is null ? null : FaceState.From(health),
				health is null ? null : BodyPoseState.From(health),
				health is null ? null : MedicalState.From(health),
				RemoteInventorySnapshot.From(data));
		}

		public static State From(CharacterHealthMsg? health, IReadOnlyList<CharacterLimbMsg>? limbs)
		{
			var data = new CharacterDataMsg
			{
				Health = health,
				Limbs = limbs is null ? [] : [.. limbs],
			};
			return From(data);
		}
	}
}
