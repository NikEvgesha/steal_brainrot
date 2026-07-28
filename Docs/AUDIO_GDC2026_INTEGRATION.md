# GDC 2026 audio integration

## Source and license

- Source bundle: `D:\GameSound\GDC 2026\Sonniss.com-GDC2026-GameAudioBundle`
- Bundle: Sonniss `#GameAudioGDC Bundle Part 9` (347 WAV files).
- The included `Readme.txt` and `License - GDC Game Audio.pdf` identify the bundle as royalty-free and permit personal or commercial use without attribution.
- The original bundle is never modified. Keep its license files together with the archived source package.
- Coin collection additionally uses LordTomorrow's `Coin Splash`, distributed under CC0. Its original file and license record are stored in `Tools/audio/sources/CC0/OpenGameArt`.

## Generated game assets

- Output: `Assets/Resources/Audio/GDC2026`
- Current result: 88 Ogg/Vorbis clips, 48 kHz, 2.27 MiB total.
- Short source fragments are normalized conservatively and receive short fades to avoid clicks.
- Positional gameplay sounds are converted to mono; wide ambience remains stereo.
- Rebuild command:

```powershell
python -u Tools\audio\build_gdc2026_audio.py --clean
```

The exact source file, offset, duration, channel mode and target level for every derived clip live in `Tools/audio/build_gdc2026_audio.py`. Project-local CC0 originals live under `Tools/audio/sources`.

## Runtime design

- `GameAudioId` is the stable event vocabulary used by gameplay code.
- `GameAudioCatalog` maps those events to one clip, a randomized pool or a layered cue.
- `SoundManager` loads the compact Resources collection, applies per-event cooldowns, routes UI/SFX/environment buses, provides 2D/3D one-shots and owns runtime loop sources.
- One-shot callers can apply a runtime pitch multiplier. Sequential coin collections spread the rising combo across 20 steps and reset after a one-second gap.
- Global pointer feedback covers ordinary buttons, toggles and sliders. Explicit event calls cover meaningful results so audio is not tied to a specific UI layout.
- Legacy serialized clips remain as fallbacks where they already existed.

## Connected gameplay areas

- movement: animation-event grass/hard footsteps, landing, water and teleport; the ordinary jump is intentionally silent;
- inventory/economy: pickup, equip, placement, sell, coins, gems and insufficient funds;
- interaction hold flow: start, progress, completion and cancellation;
- eggs: conveyor drop, placement, timer, speed-up, ready state, hatch ticks/crack/reveal and elemental/rare accents;
- animals and BigPet: placement, income, selection, feeding, chewing, XP, level-up and unlock;
- world progression: hammer, territory unlock, conveyor ambience/upgrade/activation and claim-all zone;
- UI/features: generic controls, album, food shop, roulette and Tutorial V2 tasks/rewards;
- ambience: zoo day bed plus catalogued wind, water, conveyor, distant animals and food-shop loops.

## Manual acceptance checklist

1. Start Play Mode and confirm `SoundManager.LoadedEventClipCount == 88`.
2. Move over grass and a hard surface, then sprint with Shift. Confirm that footsteps follow foot contacts, use varied pitch and that the ordinary jump is silent.
3. Buy/place/speed up/hatch an egg; test both ordinary and rare/elemental outcomes.
4. Collect income from several animals less than one second apart. Confirm that the short coin-splash cue rises gradually across roughly 20 collections, resets after a pause and has no additional reward-bell layer.
5. Feed and level BigPet; unlock a field with the hammer; buy a conveyor upgrade.
6. Open the album, switch tabs/cards and claim a reward.
7. Spin the roulette and listen for sector ticks and the stop cue.
8. Check UI/SFX/music sliders and pause behavior.
9. Swim and confirm that movement uses the selected `water_move_04` cue without audible repetition artifacts.
10. Collect gems and confirm that their magical cue is distinct from the coin sound. Then pick up, equip, place and sell an item; verify successful and failed purchases.
11. Listen for repetition, clipping and excessive loudness for at least five minutes.
12. Repeat the critical pass in WebGL because browser audio activation and compression can differ from Editor playback.
