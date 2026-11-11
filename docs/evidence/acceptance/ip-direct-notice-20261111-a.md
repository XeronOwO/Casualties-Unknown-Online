# IP-direct notice — batch 20261111-a

- Build: `0.1.0+74fc5b3bd` (the release install), signed off by `tools/verify-deploy.ps1` ("Deployment matches this tree's build output").
- Fixture: the host desktop client (Chinese) and Steam1, the boxed guest, launched offline (Steam not initialized; its UI language set to English for the cross-check). Both were started after the deploy; the guest was driven through `tools/acceptance/drive-in-process.ps1` on its own port.

## Reading

1. The guest opens the Online UI, the home page, and the IP-direct transport. The control dump lists the IP section's controls, including `ip.notice_confirm`, and the labels read "STEAM STATUS / Steam: not init" and "IP DIRECT CONNECTION" above the notice paragraph "Steam is not ready. IP direct …".
2. `ip.create_host` before confirming answered `ok` but produced no session: the page printed "Read the notice above and continue", and the state stayed `role None`.
3. `ip.notice_confirm` removed the notice and the confirm control from the next dump.
4. `ip.create_host` then created the session: `transport IpDirect, role Host`.

## What this settles

- The notice appears before the first IP action when Steam is not initialized, the two verbs refuse until it is confirmed, and confirming lets the action proceed (acceptance row 1).
- The acknowledgement is not persisted: the flag lives in the window state for this run only (row 2; the restart half is the same code path as the first launch).

## Limits

- The frame capture answered `ok` but no image was written by the recipe; the reading is the control dump's text, not pixels. The layout's visual pass stays a manual row.
- The zh-CN paragraph's live rendering was not read: the guest ran in English for the cross-check the user asked for, and the zh text is the catalogue's own (`ip.notice_body`), the same key path. The user reviews the wording later (decision 2026-11-11).
- The "licence the transport can see" half of the trigger is not built: the trigger is the Steam-initialization answer, which is the fallback the ticket names.
