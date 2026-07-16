# Tutorial V1

Status on 2026-07-16: implementation and Unity editor validation are complete. Runtime acceptance on desktop offline, desktop online and mobile/touch is still required.

## Runtime flow

| Index | Stable ID | Completion condition |
|---:|---|---|
| 0 | `learn_movement` | Player walks 5 horizontal meters. |
| 1 | `find_home` | Player reaches the local home entry marker. |
| 2 | `reach_conveyor` | Player reaches the local conveyor. |
| 3 | `acquire_starter_egg` | A starter egg exists in inventory/local progress; a free `egg1` is granted once if needed. |
| 4 | `return_home` | Player returns to the local home entry. |
| 5 | `place_starter_egg` | An egg is placed in a free cell on the local base. |
| 6 | `hatch_starter_egg` | The starter egg hatches; its timer is capped at 5 seconds. |
| 7 | `meet_starter_animal` | Guaranteed `Capybara` is present in the local cell. |
| 8 | `wait_first_income` | The animal has positive collectible income. |
| 9 | `collect_first_income` | Positive income is collected. |
| 10 | `make_first_expansion` | Another egg is acquired, a field is unlocked, or the conveyor is upgraded. |
| 11 | `claim_album_reward` | The first animal discovery reward is claimed. |
| 12 | `continue_independently` | Player confirms the next independent goal and finishes the tutorial. |

The manager starts from `GameEntryPoint` after the save, player, inventory and item storage are ready. Targets are resolved from `RemoteBasesApplier.GetLocalSlotRoot()`; remote fields and conveyors are rejected both at interaction and signal boundaries.

## Persistence and idempotence

`TutorialSaveData` stores the schema version, stable step ID/index, completion/skip flags, starter egg/animal grant flags and UTC timestamps. State is serialized after every transition and critical reward flag:

- Mirra SDK key: `Tutorial.State.V1`;
- Dummy/PlayerPrefs key: `Tutorial.State.V1`;
- the legacy `EndTutorial` bool remains the completion migration/fallback.

Inventory and local-field facts are checked again after resume. If the app closes between granting and saving the tutorial flag, the existing egg/animal advances the state instead of issuing a duplicate.

## UI, localization and ads

- Editable view prefab: `Assets/Resources/Tutorial/TutorialView.prefab`.
- Runtime binding: `TutorialView` finds the authored message, progress, skip and arrow nodes in that prefab.
- Target presentation: safe-area screen arrow plus pulsing renderer highlight on the resolved world target.
- Conflicting buttons are disabled during onboarding; the album is blocked until `claim_album_reward`.
- Skip uses `UniversalDecisionPopup` when available and an inline confirmation fallback otherwise.
- `AdsManager` pauses timed interstitials during onboarding and applies a 45-second grace period afterward.
- Localization contains 33 RU/EN keys, including separate desktop/touch hints for all 13 steps.

Editor utilities:

- `Tools/Tutorial V1/Synchronize RU-EN Localization` repairs/adds the owned tutorial keys.
- `Tools/Tutorial V1/Validate Setup` checks all stable IDs, prefab nodes and RU/EN values.
- Batch entry point: `TutorialV1EditorTools.BatchSynchronizeAndValidate`.

## Analytics

Events:

- `tutorial_started` / `tutorial_resumed`;
- `tutorial_step_started` / `tutorial_step_completed`;
- `tutorial_skipped` / `tutorial_completed`.

Every event includes `step_id`, `step_index`, `elapsed_sec`, `input_mode` and `online_mode`. Provider-wide naming/version/platform policy remains in the separate P0.1 analytics task.

## Required acceptance passes

Before changing the handoff status to complete, record all of these passes:

1. Desktop offline, clean profile: finish all 13 steps without debug assistance; confirm no interstitial appears.
2. Desktop offline resume: close/reopen during egg grant, placed egg, hatch and album steps; confirm exact resume and no duplicate egg/animal/reward.
3. Desktop online with another occupied slot: finish the flow and confirm every arrow, highlight and interaction stays on the local base.
4. Mobile/touch viewport: finish the flow using joystick/action controls; verify touch copy, safe area and no blocked required controls.
5. After completion/skip: verify disabled UI is restored and the first possible interstitial is delayed by at least 45 seconds.

Also inspect analytics output with a configured provider. A missing provider intentionally emits only one warning per session and does not block gameplay.
