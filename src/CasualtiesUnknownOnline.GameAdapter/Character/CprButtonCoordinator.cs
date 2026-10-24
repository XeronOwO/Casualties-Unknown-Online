using System;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;
using Microsoft.Extensions.Logging;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace CasualtiesUnknownOnline.GameAdapter.Character;

/// <summary>
/// The wound view's CPR control (ticket `remote-medical-cpr.md`, stage 1c): the native panel's
/// <c>specialUseButton</c> cloned into the same parent, labelled from <see cref="CprButtonPolicy"/>'s state and routed to
/// the medical session family — the entry the ruling's KrokMP reference describes, built out of the game's own button
/// rather than a parallel CUO panel.
///
/// <para>
/// One control, two lives: while the focused remote body's heart has stopped it starts CPR (bare hands, the target's own
/// client decides the start against its arrest); once this client's own session runs it becomes the compression control,
/// and each press sends the measured interval of the rhythm. The clamp that keeps two presses from reading as one is
/// <see cref="CprButtonPolicy.IntervalSeconds"/>.
/// </para>
///
/// <para>
/// The clone MUST NOT keep the template's click: <c>Object.Instantiate</c> copies a UnityEvent with its PERSISTENT
/// listeners intact, so the event object is replaced before ours is added — otherwise pressing the CPR control would
/// also run whatever limb action the template's own button carries.
/// </para>
/// </summary>
internal sealed class CprButtonCoordinator(GameAdapterDomains domains)
{
	/// <summary>The clone's own name, so a run (and a reader recipe) can find it in the panel's children.</summary>
	internal const string ControlName = "CUO CPR Control";

	/// <summary>The ventilation clone's name, the second channel the ruling separates from the compressions.</summary>
	internal const string BreathControlName = "CUO CPR Breath";

	private readonly GameAdapterDomains _domains = domains;
	private GameObject? _control;
	private GameObject? _breath;
	private TextMeshProUGUI? _label;
	private ulong _target;
	private ulong _operationId;
	private float _lastPressSeconds;
	private float _lastBreathSeconds;
	private bool _subscribed;

	/// <summary>Subscribe once, on the first frame the panel is maintained with a focus open.</summary>
	private void EnsureSubscribed()
	{
		if (_subscribed)
		{
			return;
		}

		_domains.PlayerInteraction.MedicalOperations.StartAckReceived += OnStartAck;
		_subscribed = true;
	}

	/// <summary>Per frame while a remote focus is open: create the control once, then keep its state and label honest.</summary>
	internal void Update(WoundView view)
	{
		EnsureSubscribed();
		var display = RemoteMedicalView.DisplayBody;
		var state = CprButtonPolicy.Decide(
			hasRemoteFocus: RemoteMedicalView.IsOpen && display != null, // Unity object — ==
			patientInArrest: display != null && display.inCardiacArrest, // Unity object — ==
			ownCprRunning: _operationId != 0);

		if (state == CprButtonState.Hidden)
		{
			if (_control != null) // Unity object — ==
			{
				_control.SetActive(false);
			}

			if (_breath != null) // Unity object — ==
			{
				_breath.SetActive(false);
			}

			return;
		}

		if (_control == null || _control.transform.parent != view.specialUseButton.transform.parent) // Unity objects — ==
		{
			CreateControl(view);
			if (_control == null) // Unity object — ==
			{
				return;
			}
		}

		_control.SetActive(true);
		if (_label != null) // Unity object — ==
		{
			_label.text = state == CprButtonState.Compress
				? _domains.Localization.T("medical.cpr_compress")
				: _domains.Localization.T("medical.cpr_start");
		}

		// The breath is only offered while this client's own session runs: before that there is nothing to ventilate on
		// (the start covers both channels), and after it ends the panel's control hides the same way.
		_breath?.SetActive(state == CprButtonState.Compress);
	}

	/// <summary>The focus closed: the controls go with it, and the session state is forgotten (the host ends the operation).</summary>
	internal void Release()
	{
		if (_control != null) // Unity object — ==
		{
			Object.Destroy(_control);
			_control = null;
			_label = null;
		}

		if (_breath != null) // Unity object — ==
		{
			Object.Destroy(_breath);
			_breath = null;
		}

		_target = 0;
		_operationId = 0;
	}

	private void CreateControl(WoundView view)
	{
		var template = view.specialUseButton;
		if (template == null) // Unity object — ==
		{
			return;
		}

		_control = CreateClone(template, ControlName, _domains.Localization.T("medical.cpr_start"), OnPressed, rowOffset: 1);
		_breath = CreateClone(template, BreathControlName, _domains.Localization.T("medical.cpr_ventilate"), OnBreathPressed, rowOffset: 2);
	}

	/// <summary>
	/// One clone of the panel's own button: named, offset one row further down per <paramref name="rowOffset"/>, its
	/// click REPLACED (a cloned UnityEvent keeps the template's persistent listeners), and labelled from the catalogue.
	/// </summary>
	private GameObject? CreateClone(GameObject template, string name, string labelText, UnityAction onClick, int rowOffset)
	{
		var clone = Object.Instantiate(template, template.transform.parent);
		clone.name = name;
		var rect = clone.GetComponent<RectTransform>();
		var templateRect = template.GetComponent<RectTransform>();
		if (rect != null && templateRect != null) // Unity objects — ==
		{
			// Rows below the template so nothing overlaps; the anchors are the template's own, so the controls follow the
			// panel's layout instead of guessing at positions.
			rect.anchoredPosition = templateRect.anchoredPosition
				+ new Vector2(0f, -((Mathf.Abs(templateRect.rect.height) + 6f) * rowOffset));
		}

		var button = clone.GetComponent<Button>();
		if (button != null) // Unity object — ==
		{
			button.onClick = new Button.ButtonClickedEvent();
			button.onClick.AddListener(onClick);
			button.interactable = true;
		}

		var label = clone.GetComponentInChildren<TextMeshProUGUI>();
		if (label != null) // Unity object — ==
		{
			label.text = labelText;
			if (name == ControlName)
			{
				_label = label;
			}
		}

		clone.SetActive(false);
		return clone;
	}

	private void OnPressed()
	{
		if (!RemoteMedicalView.IsOpen)
		{
			return;
		}

		var target = RemoteMedicalView.TargetSteamId;
		if (target == 0)
		{
			return;
		}

		var operations = _domains.PlayerInteraction.MedicalOperations;

		if (_operationId == 0)
		{
			_target = target;
			_lastPressSeconds = Time.unscaledTime;
			operations.SendOtherStartRequest(target, itemInstanceId: 0, targetLimbIndex: -1, MedicalOperationKind.Cpr);
			_domains.Log.LogInformation("[Cpr] the wound view's control asked to start CPR on {Target}.", target);
			return;
		}

		var interval = CprButtonPolicy.IntervalSeconds(_lastPressSeconds, Time.unscaledTime);

		// The press is paid for HERE, not on the host: a member's own vitals are authoritative on their own client, so a
		// charge the host writes into its snapshot of the member never reaches the member's body (read on the machine, batch
		// 20261024-o: five host-charged presses left the operator's own stamina at 100). An operator at the floor cannot
		// compress — the refusal is the game's own deny sound, and nothing is sent, so the patient is not given a press
		// nobody made.
		if (!TryPayForThePress())
		{
			_domains.PlayerInteraction.FireUseRefused();
			_domains.Log.LogInformation("[Cpr] the press was refused: this operator has no stamina left.");
			return;
		}

		_lastPressSeconds = Time.unscaledTime;
		operations.SendOtherUpdate(_operationId, MedicalOperationUpdateAction.Compress, interval);
		_domains.Log.LogInformation("[Cpr] compression {Interval:F2}s sent to operation {Operation}.", interval, _operationId);
	}

	/// <summary>
	/// Charges this operator's own body for one press — stamina and a little heat, the reference's per-press cost — and
	/// answers whether the body could afford it. The numbers come from `CprRhythmPolicy`, the same rule the host reads for
	/// the patient's side, so the two halves cannot drift.
	/// </summary>
	private bool TryPayForThePress()
	{
		var body = PlayerCamera.main != null ? PlayerCamera.main.body : null; // Unity object — ==
		if (body == null) // Unity object — ==
		{
			return true;
		}

		if (body.stamina < CprRhythmPolicy.MinimumStamina)
		{
			return false;
		}

		body.stamina = Math.Max(0f, body.stamina - CprRhythmPolicy.StaminaPerPress);
		body.temperature += CprRhythmPolicy.TemperaturePerPress;
		return true;
	}

	/// <summary>
	/// The breath control: the oxygen channel's press. It only exists while this client's own session runs (the panel
	/// offers it then), and its interval is measured from the previous BREATH, not from the compressions — the two
	/// channels keep their own rhythm.
	/// </summary>
	private void OnBreathPressed()
	{
		if (!RemoteMedicalView.IsOpen || _operationId == 0)
		{
			return;
		}

		var interval = CprButtonPolicy.IntervalSeconds(_lastBreathSeconds, Time.unscaledTime);
		_lastBreathSeconds = Time.unscaledTime;
		_domains.PlayerInteraction.MedicalOperations.SendOtherUpdate(_operationId, MedicalOperationUpdateAction.Ventilate, interval);
		_domains.Log.LogInformation("[Cpr] breath {Interval:F2}s sent to operation {Operation}.", interval, _operationId);
	}

	/// <summary>
	/// The host's answer to a start: an ACCEPTED CPR acknowledgement for the target this control asked about is what
	/// makes the operation id known, so the next press becomes a compression. A refusal leaves the control in Start — the
	/// player can press again (the patient may have moved out of arrest, which the label follows).
	/// </summary>
	private void OnStartAck(MedicalOperationStartAckMsg ack)
	{
		if (ack.Kind != MedicalOperationKind.Cpr || ack.TargetSteamId != _target)
		{
			return;
		}

		if (!ack.Accepted)
		{
			_domains.Log.LogWarning("[Cpr] the host refused the CPR start on {Target}: {Reason}", _target, ack.RejectReason);
			return;
		}

		_operationId = ack.OperationId;
		_domains.Log.LogInformation("[Cpr] CPR accepted on {Target} as operation {Operation}; the control now compresses.", _target, _operationId);
	}
}
