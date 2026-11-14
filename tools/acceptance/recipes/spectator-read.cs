// recipe: spectator-read
// args: none
// returns: ok, cameraBody, cameraPain, woundBody, woundPain, woundBodyIsCamera, hudBodyIsWound, gateActive,
//          woundLimb3Prosthetic, woundLimb3Item, woundLimb5Pain, woundLimb5Skin, cameraLimb4Prosthetic, shaderPain, shaderBlood,
//          shaderLowOxy, shaderBrainDamage, shaderBloodloss, shaderBlur, shaderDistort,
//          blackAmount, sleepFill, sleepFillAlpha, ecgActive, vignetteAlpha, consciousnessFadeAlpha,
//          worldSoundsTotal, error
//
// What the spectator's own client is presenting: the camera's local body, the body the native
// WoundView limb strip draws (WoundView.cs:437-502, including the prosthetic icon at 502), the
// body the spectator gate holds for the HUD, and the screen shader's pain value
// (PlayerCamera.HandleScreenShaders at PlayerCamera.cs:3255-3339). All three must name the
// watched body while the mode is on; the local body's own values are the control reading.
((System.Func<string>)(() => {
	var camera = PlayerCamera.main;
	if (camera == null || camera.body == null) { return "{\"ok\":false,\"error\":\"no-camera\"}"; }
	var view = WoundView.view;
	if (view == null || view.body == null) { return "{\"ok\":false,\"error\":\"no-wound-view\"}"; }

	var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
	var staticFlags = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;

	var materialField = typeof(PlayerCamera).GetField("mainScreenPass", flags);
	var material = materialField != null ? materialField.GetValue(camera) as Material : null;
	var blurField = typeof(PlayerCamera).GetField("blurPass", flags);
	var blur = blurField != null ? blurField.GetValue(camera) as Material : null;
	var bonusField = typeof(PlayerCamera).GetField("bonusPass", flags);
	var bonus = bonusField != null ? bonusField.GetValue(camera) as Material : null;
	var damageField = typeof(PlayerCamera).GetField("damagePass", flags);
	var damage = damageField != null ? damageField.GetValue(camera) as Material : null;
	var read = (System.Func<Material, string, float>)((mat, key) => mat != null ? mat.GetFloat(key) : -1f);
	var smoothField = typeof(PlayerCamera).GetField("painSmooth", flags);
	var painSmooth = smoothField != null ? (float)smoothField.GetValue(camera) : -1f;

	var gateType = System.Type.GetType("CasualtiesUnknownOnline.GameAdapter.SpectatorPresentationGate, CasualtiesUnknownOnline.GameAdapter");
	var hudProperty = gateType != null ? gateType.GetProperty("HudBody", staticFlags) : null;
	var hud = hudProperty != null ? hudProperty.GetValue(null) as Body : null;
	var totalSoundsProperty = gateType != null ? gateType.GetProperty("TotalWorldSoundsHeard", staticFlags) : null;
	var totalSounds = totalSoundsProperty != null ? (int)totalSoundsProperty.GetValue(null) : -1;
	var activeProperty = gateType != null ? gateType.GetProperty("IsActive", staticFlags) : null;
	var gateActive = activeProperty != null && (bool)activeProperty.GetValue(null);
	var wound = view.body;
	var limb = wound.limbs != null && wound.limbs.Length > 3 ? wound.limbs[3] : null;
	var cameraLimb = camera.body.limbs != null && camera.body.limbs.Length > 4 ? camera.body.limbs[4] : null;
	var woundPainLimb = wound.limbs != null && wound.limbs.Length > 5 ? wound.limbs[5] : null;

	var inv = System.Globalization.CultureInfo.InvariantCulture;
	return "{\"ok\":true"
		+ ",\"cameraBody\":\"" + (camera.body.name ?? "").Replace("\"", "'") + "\""
		+ ",\"cameraPain\":" + camera.body.averagePain.ToString("0.###", inv)
		+ ",\"woundBody\":\"" + (wound.name ?? "").Replace("\"", "'") + "\""
		+ ",\"woundPain\":" + wound.averagePain.ToString("0.###", inv)
		+ ",\"woundBodyIsCamera\":" + (wound == camera.body ? "true" : "false")
		+ ",\"hudBodyIsWound\":" + (hud != null && hud == wound ? "true" : "false")
		+ ",\"gateActive\":" + (gateActive ? "true" : "false")
		+ ",\"woundLimb3Prosthetic\":" + (limb != null && limb.prostheticInfo.hasProsthetic ? "true" : "false")
		+ ",\"woundLimb3Item\":\"" + (limb != null && limb.prostheticInfo.itemId != null ? limb.prostheticInfo.itemId.Replace("\"", "'") : "") + "\""
		+ ",\"woundLimb5Pain\":" + (woundPainLimb != null ? woundPainLimb.pain.ToString("0.###", inv) : "-1")
		+ ",\"woundLimb5Skin\":" + (woundPainLimb != null ? woundPainLimb.skinHealth.ToString("0.###", inv) : "-1")
		+ ",\"cameraLimb4Prosthetic\":" + (cameraLimb != null && cameraLimb.prostheticInfo.hasProsthetic ? "true" : "false")
		+ ",\"cameraLimb4Item\":\"" + (cameraLimb != null && cameraLimb.prostheticInfo.itemId != null ? cameraLimb.prostheticInfo.itemId.Replace("\"", "'") : "") + "\""
		+ ",\"shaderPain\":" + read(material, "_Pain").ToString("0.####", inv)
		+ ",\"shaderBlood\":" + read(material, "_BloodAmount").ToString("0.####", inv)
		+ ",\"shaderLowOxy\":" + read(material, "_LowOxyAmt").ToString("0.####", inv)
		+ ",\"shaderStamina\":" + read(material, "_Stamina").ToString("0.####", inv)
		+ ",\"shaderFrost\":" + read(material, "_FrostAmount").ToString("0.####", inv)
		+ ",\"shaderOverheat\":" + read(material, "_OverheatAmount").ToString("0.####", inv)
		+ ",\"shaderBadFocus\":" + read(material, "_BadFocusAmount").ToString("0.####", inv)
		+ ",\"shaderInsanity\":" + read(material, "_InsanityFX").ToString("0.####", inv)
		+ ",\"shaderDirtyness\":" + read(material, "_Dirtyness").ToString("0.####", inv)
		+ ",\"shaderBlur\":" + read(blur, "_BlurIntensity").ToString("0.####", inv)
		+ ",\"shaderHunger\":" + read(blur, "_HungerIntensity").ToString("0.####", inv)
		+ ",\"shaderSickness\":" + read(blur, "_SicknessAmount").ToString("0.####", inv)
		+ ",\"shaderBrainDamage\":" + read(damage, "_BrainDamage").ToString("0.####", inv)
		+ ",\"shaderBloodloss\":" + read(damage, "_Bloodloss").ToString("0.####", inv)
		+ ",\"shaderEyeDamage\":" + read(damage, "_EyeDamage").ToString("0.####", inv)
		+ ",\"shaderDistort\":" + read(bonus, "_Distort").ToString("0.####", inv)
		+ ",\"painSmooth\":" + painSmooth.ToString("0.###", inv)
		+ ",\"blackAmount\":" + camera.blackAmount.ToString("0.####", inv)
		+ ",\"sleepFill\":" + (camera.sleepFill != null ? camera.sleepFill.fillAmount.ToString("0.####", inv) : "-1")
		+ ",\"sleepFillAlpha\":" + (camera.sleepFill != null ? camera.sleepFill.color.a.ToString("0.####", inv) : "-1")
		+ ",\"ecgActive\":" + (camera.consciousnessECG != null && camera.consciousnessECG.active ? "true" : "false")
		+ ",\"vignetteAlpha\":" + (camera.consciousnessVignette != null ? camera.consciousnessVignette.color.a.ToString("0.####", inv) : "-1")
		+ ",\"consciousnessFadeAlpha\":" + (camera.consciousnessFade != null ? camera.consciousnessFade.color.a.ToString("0.####", inv) : "-1")
		+ ",\"worldSoundsTotal\":" + totalSounds.ToString(System.Globalization.CultureInfo.InvariantCulture)
		+ "}";
}))()
