# Localization TODO (separate backlog)

## Tutorial V1 coverage (2026-07-16)
- [x] Added 33 editable RU/EN keys for tutorial progress, skip confirmation and all 13 desktop/touch steps.
- [x] Added `Tools/Tutorial V1/Synchronize RU-EN Localization` and setup validation for missing/empty/corrupted values.
- [ ] Run the tutorial in Play Mode while switching RU/EN on desktop and touch viewports; verify wrapping and safe-area placement.

## Priority 0 - stability
- [ ] Add strict fallback behavior: if key exists but selected language value is empty, use fallback language (EN) and never render empty text.
- [ ] Add runtime guard for dynamic localization calls: missing key -> fallback text + one warning in log (no spam).
- [ ] Add validation report before build: missing keys, empty values, duplicate keys.

## Priority 1 - current UI coverage
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
