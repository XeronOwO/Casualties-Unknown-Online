# IP direct shows the purchase notice when Steam is not ready

- Status: Done — the notice is built and read on the release build (2026-11-11, wording corrected the same day to the user's own meaning: with Steam not initialized the IP section shows one paragraph and a confirm button; `ip.create_host` refuses with "Read the notice above and continue" until it is confirmed, confirming removes both, and the verb then creates the session (`transport IpDirect, role Host`). The zh-CN paragraph is the agent's draft from the user's brief (decision 2026-11-11: the agent drafts, the user reviews later). Reading: docs/evidence/acceptance/ip-direct-notice-20261111-a.md.
- Priority: High
- Category: UI / transport
- Source: the user (2026-11-09): when Steam is not initialized and the player uses the IP-direct transport, the page shows a text and a button telling the player to support the official game; the acknowledgement lasts for this run only and must be clicked again after a restart. The simplified-Chinese text carries the extra paragraph about the discount the CUO localization wiki team negotiated for the China region, asking those who can to buy the official game.

## What the player sees

- The IP-direct controls (`ip.create_host`, `ip.address`, `ip.join`) are behind the transport toggle `home.transport.ip`. A player whose Steam is not initialized reaches them through that toggle today, with no word about owning the game.
- With Steam not initialized (or without a licence the transport can see), the first IP action shows a panel: one paragraph of text and one button ("I own a copy — continue"). Confirming lets this run proceed; the choice is not written anywhere, so the next launch shows the panel again.
- The paragraph is the ordinary "please support the official release" sentence for every language; `zh-CN` swaps in the wiki team's paragraph about the China-region discount.

## The chain

- `src/CasualtiesUnknownOnline.Plugin/OnlineUiHost.cs` — the home page's transport toggle and the IP section's controls.
- `src/CasualtiesUnknownOnline.Plugin/IpDirectActions.cs` — the two verbs the panel guards.
- The Steam state the panel asks: `ISteamService`'s initialization answer, the same one the capability report prints.

## Acceptance

1. A client whose Steam is not initialized opens the IP section, acts, and sees the notice before the first IP action; confirming lets the action run and the session proceed.
2. A restarted client shows the notice again — nothing is persisted.
3. With `zh-CN` active the second paragraph is the wiki team's text; every other language shows the plain sentence.

## Design

- `docs/design/surfaces/online-window.md` — the Online UI's page structure and its control vocabulary: the notice is a page-level panel, a text and one confirm button, drawn with the same parts as the other pages.
- `docs/design/game/ui-and-settings.md` — the game's own UI and settings surfaces, named so the notice reads as part of the game rather than a browser dialog.

## Open questions

- The exact licensing signal the panel may ask (the Steam app id's `BIsSubscribedApp` vs the transport's own state) is read from the 1.0 code before the design is fixed; the fallback is "Steam not initialized", which the user's report names.

## Wording correction (2026-11-11)

The first draft carried two sentences the user never asked for (a claim that no licence is checked, and a wiki-page phrasing). The user's own words: the Steam-not-ready notice asks the player to support the official game; the simplified-Chinese version adds that the Chinese localization wiki team worked hard to arrange a discount for the China region, and everyone hopes those who can will support the game. Both catalogue texts were rewritten to that meaning and the build re-read (gates 834/0).

