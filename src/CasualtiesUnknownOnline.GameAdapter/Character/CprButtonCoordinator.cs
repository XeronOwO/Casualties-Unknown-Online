using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;
using Microsoft.Extensions.Logging;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

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

	private readonly GameAdapterDomains _domains = domains;
	private GameObject? _control;
	private Button? _button;
	private TextMeshProUGUI? _label;
	private ulong _target;
	private ulong _operationId;
	private float _lastPressSeconds;
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
	}

	/// <summary>The focus closed: the control goes with it, and the session state is forgotten (the host ends the operation).</summary>
	internal void Release()
	{
		if (_control != null) // Unity object — ==
		{
			Object.Destroy(_control);
			_control = null;
			_button = null;
			_label = null;
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

		var clone = Object.Instantiate(template, template.transform.parent);
		clone.name = ControlName;
		var rect = clone.GetComponent<RectTransform>();
		var templateRect = template.GetComponent<RectTransform>();
		if (rect != null && templateRect != null) // Unity objects — ==
		{
			// One row below the template so the two never overlap; the anchors are the template's own, so the control
			// follows the panel's layout instead of guessing at positions.
			rect.anchoredPosition = templateRect.anchoredPosition + new Vector2(0f, -Mathf.Abs(templateRect.rect.height) - 6f);
		}

		_button = clone.GetComponent<Button>();
		if (_button != null) // Unity object — ==
		{
			// A cloned UnityEvent keeps the template's PERSISTENT listeners; replacing the event drops them.
			_button.onClick = new Button.ButtonClickedEvent();
			_button.onClick.AddListener(OnPressed);
			_button.interactable = true;
		}

		_label = clone.GetComponentInChildren<TextMeshProUGUI>();
		if (_label != null) // Unity object — ==
		{
			_label.text = _domains.Localization.T("medical.cpr_start");
		}

		_control = clone;
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
		_lastPressSeconds = Time.unscaledTime;
		operations.SendOtherUpdate(_operationId, MedicalOperationUpdateAction.Compress, interval);
		_domains.Log.LogInformation("[Cpr] compression {Interval:F2}s sent to operation {Operation}.", interval, _operationId);
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
