# Localization TODO (separate backlog)

## Tutorial V1 coverage (2026-07-16)
- [x] Added editable RU/EN keys for tutorial progress and all 13 desktop/touch steps.
- [x] Added `Tools/Tutorial V1/Synchronize RU-EN Localization` and setup validation for missing/empty/corrupted values.
- [x] Updated the starter-egg RU/EN copy to describe the real marked, free conveyor interaction instead of an automatic grant.
- [x] Live Bridge checks confirmed RU/EN switching and tutorial layout at `1280x720`, `800x600` and portrait `390x844`.

## Tutorial V2 UI checkpoint (2026-07-17)
- [x] Removed skip controls from the active UI/runtime localization contract and added `UI/Tutorial/Reward`.
- [x] Approved and implemented the final ten-task lesson table, including all dynamic context copy.
- [x] Added `UI/Tutorial/ClaimReward`, `UI/Tutorial/Continue` and `UI/Tutorial/RewardClaimed`; synchronization and setup validation now cover 66 active RU/EN keys.
- [x] RU layout was checked through Unity Bridge at `1280x720`, `800x600` and portrait `390x844` without clipping.
- [x] Live RU/EN switching updates the task panel, including its completion reward label.
- [x] `RemoteProfilePopup` now uses shared localization and refreshes its title/stat labels on live RU/EN fallback-language changes; both languages were confirmed through Unity Bridge without reopening the popup.
- [ ] Repeat the layout and live RU/EN checks for the final ten-task copy on clean desktop/mobile profiles.

## Gameplay UI polish (2026-07-19)
- [x] Added RU/EN keys `UI/Conveyor/ActivatedBonus`, `UI/EggCatalog/BaseChanceNote` and `UI/Income/BigPetBadge` with non-empty code fallbacks.
- [x] Confirmed the Russian BigPet income badge and conveyor active-bonus text in the live scene before the final cleanup.
- [x] Added live language-change refresh for the full conveyor chance scroll, conveyor income row and BigPet badge; RU/EN switching was verified through Unity Bridge.
- [x] Added RU/EN keys `UI/BigPet/Header`, `UI/BigPet/Normal` and `UI/BigPet/Upgraded`; the redesigned selector refreshes all three labels on a live language change.

## Mirra world leaderboards (2026-08-04)
- [x] Added RU/EN `UI/Leaderboards/*` copy for five board titles, columns, loading/empty/current-player/cache states and the donation popup.
- [x] Added a separate current-score fallback for Mirra responses that do not expose the local player's rank outside top-N.
- [ ] Verify live RU/EN switching, long platform display names and donation prices in the final world-board layout through Play Mode and a Mirra WebGL build.

## Priority 0 - stability
- [ ] Add strict fallback behavior: if key exists but selected language value is empty, use fallback language (EN) and never render empty text.
- [ ] Add runtime guard for dynamic localization calls: missing key -> fallback text + one warning in log (no spam).
- [ ] Add validation report before build: missing keys, empty values, duplicate keys.

## Priority 1 - current UI coverage
- [x] Active first-party UI components and localization bindings use TMP; legacy `Text` remains only in excluded vendor/recovery content.
- [ ] Finalize `LocalizationRows_Proposed_UI.csv`: remove unused rows, keep only real UI keys.
- [ ] Apply `LocalizedText` to static UI labels in active gameplay scenes/prefabs.
- [ ] Replace hardcoded dynamic strings in code with keys (friends, lobby, conveyor, profile popup).
- [ ] Ensure newly added profile/stat labels are in table (`UI/Profile/*`).

## Priority 2 - workflow
- [ ] Sync Google Sheet -> project assets in one repeatable step.
- [ ] Keep generated reports in `Assets/Igrodelnya2.0/Localization/Reports/` for QA.
- [ ] Add a short key naming convention doc (`UI/...`, `Popup/...`, `Net/...`).
- [ ] Add PR checklist item: "new user-facing text must be localized".

## Priority 3 - future safety
- [ ] Add playmode smoke test: switch RU/EN at runtime and verify key critical screens update.
- [ ] Add unresolved-key dashboard from `AutoBindUnresolved_*.txt` to track progress by module.
