#!/usr/bin/env python3
"""Build compact Unity-ready audio clips from the local Sonniss GDC 2026 bundle.

The source bundle is never modified. Output clips are resampled to 48 kHz,
normalized conservatively, faded to avoid clicks, and encoded as Ogg/Vorbis.
"""

from __future__ import annotations

import argparse
import math
import shutil
from dataclasses import dataclass
from pathlib import Path

import numpy as np
import soundfile as sf
from scipy.signal import resample_poly


DEFAULT_SOURCE = Path(r"D:\GameSound\GDC 2026\Sonniss.com-GDC2026-GameAudioBundle")
DEFAULT_OUTPUT = Path("Assets/Resources/Audio/GDC2026")
TARGET_SAMPLE_RATE = 48_000


@dataclass(frozen=True)
class ClipSpec:
    source: str
    start: float = 0.0
    duration: float | None = None
    reverse: bool = False
    mono: bool = False
    target_db: float = -3.0
    fade_ms: float = 8.0


@dataclass(frozen=True)
class PoolSpec:
    source: str
    prefix: str
    count: int
    mono: bool = True
    target_db: float = -4.0
    max_event_seconds: float = 2.2


CLIPS: dict[str, ClipSpec] = {
    "ui_click": ClipSpec(
        "Epic Stock Media - Board Game - Sound Set Kit for Tabletop and Digital Games/"
        "UIClick_UI Button Analog Vintage Double Click Neutral Dry Press 11_ESM_BG.wav",
        mono=True,
        target_db=-7.0,
    ),
    "ui_open": ClipSpec(
        "Cinematic Sound Design - Ultra Transitions & Impacts/Woosh Sweep Slide Infographics Basic.wav",
        target_db=-7.0,
    ),
    "ui_close": ClipSpec(
        "Cinematic Sound Design - Ultra Transitions & Impacts/Woosh Sweep Slide Infographics Basic.wav",
        reverse=True,
        target_db=-8.0,
    ),
    "ui_tab": ClipSpec(
        "Cinematic Sound Design - Interface & Infographics/Interface Pop High Short.wav",
        duration=0.35,
        mono=True,
        target_db=-10.0,
    ),
    "ui_confirm": ClipSpec(
        "Cinematic Sound Design - Interface & Infographics/Interface Accept Glassy Snap.wav",
        mono=True,
        target_db=-6.0,
    ),
    "ui_cancel": ClipSpec(
        "Cinematic Sound Design - UI Interaction Elements/Deny Muted.wav",
        duration=0.55,
        mono=True,
        target_db=-8.0,
    ),
    "ui_error": ClipSpec(
        "Cinematic Sound Design - System & UI Feedback Elements/Interface Deny Low Fat Dark.wav",
        mono=True,
        target_db=-6.0,
    ),
    "ui_locked": ClipSpec(
        "Epic Stock Media - HD Lock And Mechanism Sound Design Kit/"
        "MECHLtch_Click Deep Mechanism Latch Button Nearfield Thunk 02_ESM_HDLM.wav",
        mono=True,
        target_db=-7.0,
    ),
    "notification": ClipSpec(
        "CB_Sounddesign - Applicable Sounds - Organic UI and Building Games SFX/"
        "UIMisc_Kalimba 3 Up_CB Sounddesign_APPlicable Sounds.wav",
        target_db=-8.0,
    ),
    "ready": ClipSpec(
        "Cinematic Sound Design - Hybrid Game & UI Elements/Game Entry Happy Short.wav",
        target_db=-6.0,
    ),
    "toggle": ClipSpec(
        "InMotionAudio - USA Hotel/MECHClik_USALightSwitch_On05_InMotionAudio_USAHotel.wav",
        mono=True,
        target_db=-9.0,
    ),
    "slider": ClipSpec(
        "InMotionAudio - Saxophone/MUSCInst_KeyPress64_InMotionAudio_Saxophone.wav",
        duration=0.16,
        mono=True,
        target_db=-14.0,
    ),
    "interact_start": ClipSpec(
        "Cinematic Sound Design - Interface & Infographics/Interface Percussion Snap.wav",
        duration=0.40,
        mono=True,
        target_db=-9.0,
    ),
    "interact_loop": ClipSpec(
        "Cinematic Sound Design - User Interface/Readout Thin Long.wav",
        start=0.25,
        duration=1.15,
        mono=True,
        target_db=-13.0,
        fade_ms=25.0,
    ),
    "interact_cancel": ClipSpec(
        "Cinematic Sound Design - System & UI Feedback Elements/Interface Arp Reveal Down Long.wav",
        duration=0.85,
        mono=True,
        target_db=-9.0,
    ),
    "jump": ClipSpec(
        "344 Audio - Elemental Palette Designed Vol. 1/"
        "WINDDsgn_Wind, Rush, Whoosh, Long x5 01_344 Audio_Elemental Palette Designed Vol 1.wav",
        mono=True,
        target_db=-9.0,
    ),
    "land": ClipSpec(
        "InMotionAudio - Instrument Case/OBJLug_CaseDown_Concrete12_InMotionAudio_InstrumentCase.wav",
        duration=0.65,
        mono=True,
        target_db=-8.0,
    ),
    "water_splash": ClipSpec(
        "Just Sound Effects - Highlands of Norway/"
        "WATRLap_Small Waves sloshing onto Rock Slabs at huge Glacial Lake _JSE_HoN_Quad.wav",
        start=2.0,
        duration=1.0,
        mono=True,
        target_db=-8.0,
    ),
    "water_move": ClipSpec(
        "Epic Stock Media - Public Spaces - Storms Lakes Parks and Rural Nature Exteriors/"
        "WATRLap_Summer Tennessee Lake Dock Water Ripples Wake Wave Gentle 05 Distant_ESM_CPS.wav",
        start=5.0,
        duration=0.9,
        mono=True,
        target_db=-14.0,
        fade_ms=18.0,
    ),
    "water_move_02": ClipSpec(
        "Epic Stock Media - Public Spaces - Storms Lakes Parks and Rural Nature Exteriors/"
        "WATRLap_Summer Tennessee Lake Dock Water Ripples Wake Wave Gentle 05 Distant_ESM_CPS.wav",
        start=18.0,
        duration=0.9,
        mono=True,
        target_db=-14.0,
        fade_ms=18.0,
    ),
    "water_move_03": ClipSpec(
        "Just Sound Effects - Highlands of Norway/"
        "WATRLap_Small Waves sloshing onto Rock Slabs at huge Glacial Lake _JSE_HoN_Quad.wav",
        start=6.0,
        duration=0.9,
        mono=True,
        target_db=-14.0,
        fade_ms=18.0,
    ),
    "water_move_04": ClipSpec(
        "Just Sound Effects - Highlands of Norway/"
        "WATRLap_Small Waves sloshing onto Rock Slabs at huge Glacial Lake _JSE_HoN_Quad.wav",
        start=18.0,
        duration=0.9,
        mono=True,
        target_db=-14.0,
        fade_ms=18.0,
    ),
    "teleport": ClipSpec(
        "CB_Sounddesign - Applicable Sounds - Organic UI and Building Games SFX/"
        "GAMEMisc_Magic Creation 23_CB Sounddesign_APPlicable Sounds.wav",
        duration=1.35,
        mono=True,
        target_db=-7.0,
    ),
    "equip": ClipSpec(
        "Epic Stock Media - Fantasy Game 2 - Sound Kit for Enchanted Realms/"
        "CLOTHFlp_Action Inventory Open Flip Cloth Canvas Bag Slide Light 02_ESM_FG2.wav",
        mono=True,
        target_db=-10.0,
    ),
    "pickup": ClipSpec(
        "Epic Stock Media - Fantasy Game 2 - Sound Kit for Enchanted Realms/"
        "UIAlert_Collect Scifi Futuristic Electronic Bass Burst Sweep Heavy 04_ESM_FG2.wav",
        mono=True,
        target_db=-10.0,
    ),
    "place": ClipSpec(
        "InMotionAudio - Saxophone/MUSCInst_Case_SaxPlace09_InMotionAudio_Saxophone.wav",
        mono=True,
        target_db=-8.0,
    ),
    "animal_place": ClipSpec(
        "Cinematic Sound Design - Cartoon Impacts/Cartoon Pops Random Sequence Reverb.wav",
        duration=0.55,
        mono=True,
        target_db=-9.0,
    ),
    "coin_spend": ClipSpec(
        "Cinematic Sound Design - Hybrid Game & UI Elements/Foley Coin Flip Single Fast.wav",
        mono=True,
        target_db=-8.0,
    ),
    "coin_gain": ClipSpec(
        "Cinematic Sound Design - UI Interaction Elements/Ting Coins.wav",
        target_db=-7.0,
    ),
    "gem_spend": ClipSpec(
        "Epic Stock Media - Elemental Mutation Whooshes and Impacts/"
        "GLASMvmt_Whoosh Glass Crystal Fragments Sharp Shards Dry 05_ESM_EMWI.wav",
        duration=0.85,
        target_db=-9.0,
    ),
    "clock_tick": ClipSpec(
        "344 Audio - Antique Clocks/CLOCKTick_Crooked Antique Clock_344 Audio_Antique Clocks.wav",
        start=0.25,
        duration=0.65,
        mono=True,
        target_db=-12.0,
    ),
    "egg_ready": ClipSpec(
        "Cinematic Sound Design - User Interface/Button Arp Twinkle.wav",
        duration=1.45,
        target_db=-7.0,
    ),
    "belt_drop": ClipSpec(
        "Epic Stock Media - HD Lock And Mechanism Sound Design Kit/"
        "MACHMech_Mechanism Counting Machine Interact Loose Container Short 01_ESM_HDLM.wav",
        mono=True,
        target_db=-9.0,
    ),
    "hatch_tick": ClipSpec(
        "344 Audio - Antique Typewriter/"
        "COMType_Typewriter Space Key, Typewriter 05_344 Audio_Antiques - Typewriter.wav",
        duration=0.20,
        mono=True,
        target_db=-11.0,
    ),
    "egg_crack": ClipSpec(
        "344 Audio - Dinosaurs Vol. 2/ANMLRept_Dinosaur Egg Hatching 02_344 Audio_Dinosaurs Vol 2.wav",
        mono=True,
        target_db=-5.0,
    ),
    "major_reward": ClipSpec(
        "Epic Stock Media - Anime Game/"
        "DSGNStngr_Power Up Bright Positive Successful Light Saturation Crash Shimmer 05_ESM_AG.wav",
        target_db=-5.0,
    ),
    "element_fire": ClipSpec(
        "Epic Stock Media - Elemental Mutation Whooshes and Impacts/"
        "FIREWhsh_Whoosh Fire Deep Growl Monster Saturated Crisp 03_ESM_EMWI.wav",
        duration=1.15,
        mono=True,
        target_db=-10.0,
    ),
    "element_electric": ClipSpec(
        "InMotionAudio - Arc/ELECArc_ArcDesign15_InMotionAudio_Arc.wav",
        duration=0.72,
        mono=True,
        target_db=-8.0,
    ),
    "happy_plucks": ClipSpec(
        "Cinematic Sound Design - User Interface/Interface Plucks Happy.wav",
        target_db=-7.0,
    ),
    "animal_frog": ClipSpec(
        "Epic Stock Media - Synthesized Nature Loops and Sounds/"
        "ANMLAmph_Animal Frog Echo Short Squished Frequency Formant 03_ESM_SNLS.wav",
        mono=True,
        target_db=-13.0,
    ),
    "animal_bird": ClipSpec(
        "CB_Sounddesign - Applicable Sounds - Organic UI and Building Games SFX/"
        "TOONMisc_Bird Flutes 3_CB Sounddesign_APPlicable Sounds.wav",
        mono=True,
        target_db=-12.0,
    ),
    "board_reset": ClipSpec(
        "Epic Stock Media - Board Game - Sound Set Kit for Tabletop and Digital Games/"
        "GAMEBoard_Event Board Reset Organic Multiple Pieces Wood Small 02_ESM_BG.wav",
        mono=True,
        target_db=-8.0,
    ),
    "conveyor_upgrade": ClipSpec(
        "Epic Stock Media - Tower Defense Game/"
        "ROBTMvmt_Tower Deploy Hitech Robot Motor Dark Thump Servo Whine 04_ESM_TDG.wav",
        mono=True,
        target_db=-7.0,
    ),
    "album_open": ClipSpec(
        "344 Audio - Antique Books/"
        "PAPRMisc_Antique Books Flicking Through Pages 11_344 Audio_Antiques - Books.wav",
        duration=1.1,
        target_db=-9.0,
    ),
    "album_card": ClipSpec(
        "Epic Stock Media - Board Game - Sound Set Kit for Tabletop and Digital Games/"
        "PAPRHndl_Game Play Cards Dry Show Flip Toss Disgard Near 12_ESM_BG.wav",
        mono=True,
        target_db=-11.0,
    ),
    "feed_offer": ClipSpec(
        "Cinematic Sound Design - Cartoon & Animation Vol 2/Cartoon Bubbles Short.wav",
        duration=0.55,
        mono=True,
        target_db=-10.0,
    ),
    "roulette_start": ClipSpec(
        "Sonic Bat - Vintage Radio/SBvr_Mode Select Wheel 006.wav",
        mono=True,
        target_db=-8.0,
    ),
    "social_ring": ClipSpec(
        "CB_Sounddesign - Applicable Sounds - Organic UI and Building Games SFX/"
        "UIMisc_Xylophone Ringtone 2_CB Sounddesign_APPlicable Sounds.wav",
        duration=1.25,
        target_db=-9.0,
    ),
    "gift_send": ClipSpec(
        "Cinematic Sound Design - Hybrid Game & UI Elements/Cofetti Whoosh Pluck Spill.wav",
        duration=1.25,
        target_db=-8.0,
    ),
    "gift_open": ClipSpec(
        "344 Audio - Christmas Vol. 1/"
        "MAGMisc_Wrapping Paper, Opening Present 1_344 Audio_Christmas.wav",
        start=0.1,
        duration=2.2,
        mono=True,
        target_db=-8.0,
    ),
    "zoo_day": ClipSpec(
        "Just Sound Effects - Highlands of Norway/"
        "AMBSwmp_Meadow Pipits calling many Insects humming Wind blowing through Grass_JSE_HoN_Stereo.wav",
        start=12.0,
        duration=24.0,
        target_db=-14.0,
        fade_ms=80.0,
    ),
    "wind_soft": ClipSpec(
        "Epic Stock Media - Public Spaces - Storms Lakes Parks and Rural Nature Exteriors/"
        "AMBPark_Berlin City Humboldthain Park Strong Wind On Trees Foliage Traffic Wash 03_ESM_CPS.wav",
        start=28.0,
        duration=18.0,
        target_db=-18.0,
        fade_ms=80.0,
    ),
    "water_loop": ClipSpec(
        "Epic Stock Media - Public Spaces - Storms Lakes Parks and Rural Nature Exteriors/"
        "WATRLap_Summer Tennessee Lake Dock Water Ripples Wake Wave Gentle 05 Distant_ESM_CPS.wav",
        start=32.0,
        duration=18.0,
        target_db=-16.0,
        fade_ms=80.0,
    ),
    "conveyor_loop": ClipSpec(
        "Victor Ermakov - Industrial Ambiences - Ship Repair Factory/"
        "MACHInd_Crane Onboard Ride Squeaks Motors_CW.wav",
        start=18.0,
        duration=14.0,
        mono=True,
        target_db=-18.0,
        fade_ms=80.0,
    ),
    "animal_distant": ClipSpec(
        "344 Audio - East Coast America Vol. 1/"
        "AMBSubn_Ambience, Forest Crickets, Birds, Connecticut 02_344 Audio_East Coast America.wav",
        start=8.0,
        duration=18.0,
        target_db=-18.0,
        fade_ms=80.0,
    ),
    "food_shop": ClipSpec(
        "Sonic Bat - Bars & Restaurants Ambience/SBbra_Shopping Mall Food Court B 001.wav",
        start=120.0,
        duration=18.0,
        target_db=-20.0,
        fade_ms=80.0,
    ),
}


POOLS = (
    PoolSpec("TheWorkRoom - Flip Flops/FFG002.wav", "step_grass", 6, target_db=-10.0, max_event_seconds=1.0),
    PoolSpec("TheWorkRoom - Flip Flops/FFW003.wav", "step_hard", 6, target_db=-10.0, max_event_seconds=1.0),
    PoolSpec(
        "David Dumais Audio - Melee Weapons Sound Effects Pack 2/"
        "SWSH_SWING IMPACTS Quick Heavy Weapon Swing To Thud Impact Var 01_DDUMAIS_MWP2.wav",
        "hammer_swing",
        4,
        target_db=-8.0,
        max_event_seconds=1.2,
    ),
    PoolSpec(
        "344 Audio - Historical Weapons Vol. 2/"
        "WEAPBlnt_Spear And Stick Impact, Wooden MKH 2_344 Audio_Medieval Weapons Vol 2.wav",
        "hammer_impact",
        4,
        target_db=-7.0,
        max_event_seconds=1.1,
    ),
    PoolSpec(
        "344 Audio - Dog Vocalisations Vol. 1/"
        "ANMLDog_Dog Barks, Multiple, Indoors, Perspective,_344 Audio_Dog Vocalisations_02.wav",
        "animal_bark",
        3,
        target_db=-13.0,
        max_event_seconds=1.2,
    ),
    PoolSpec(
        "344 Audio - Dog Vocalisations Vol. 1/"
        "ANMLDog_Dog Shuffle, Grunt, Movement, Lying Down_344 Audio_Dog Vocalisations.wav",
        "animal_grunt",
        3,
        target_db=-14.0,
        max_event_seconds=1.2,
    ),
    PoolSpec(
        "344 Audio - Dinosaurs Vol. 1/ANMLRept_Hatchling Calling Out_344 Audio_Dinosaurs.wav",
        "animal_hatchling",
        1,
        target_db=-13.0,
        max_event_seconds=1.1,
    ),
    PoolSpec(
        "344 Audio - Dinosaurs Vol. 2/ANMLRept_Dinosaur Eating Meat 01_344 Audio_Dinosaurs Vol 2.wav",
        "chew",
        4,
        target_db=-15.0,
        max_event_seconds=1.0,
    ),
)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--source", type=Path, default=DEFAULT_SOURCE)
    parser.add_argument("--output", type=Path, default=DEFAULT_OUTPUT)
    parser.add_argument("--clean", action="store_true")
    return parser.parse_args()


def load_audio(path: Path) -> tuple[np.ndarray, int]:
    data, sample_rate = sf.read(path, dtype="float32", always_2d=True)
    if not np.isfinite(data).all():
        data = np.nan_to_num(data, copy=False)
    return data, sample_rate


def load_audio_segment(
    path: Path,
    start: float,
    duration: float | None,
) -> tuple[np.ndarray, int]:
    with sf.SoundFile(path) as source:
        sample_rate = source.samplerate
        first = max(0, min(len(source), int(start * sample_rate)))
        source.seek(first)
        frames = -1 if duration is None else max(0, int(duration * sample_rate))
        data = source.read(frames=frames, dtype="float32", always_2d=True)
    if not np.isfinite(data).all():
        data = np.nan_to_num(data, copy=False)
    return data, sample_rate


def to_mono(data: np.ndarray) -> np.ndarray:
    if data.shape[1] == 1:
        return data
    return np.mean(data, axis=1, keepdims=True)


def resample(data: np.ndarray, sample_rate: int) -> np.ndarray:
    if sample_rate == TARGET_SAMPLE_RATE:
        return data
    divisor = math.gcd(sample_rate, TARGET_SAMPLE_RATE)
    return resample_poly(
        data,
        TARGET_SAMPLE_RATE // divisor,
        sample_rate // divisor,
        axis=0,
    ).astype(np.float32, copy=False)


def normalize(data: np.ndarray, target_db: float) -> np.ndarray:
    peak = float(np.max(np.abs(data))) if data.size else 0.0
    if peak <= 1e-6:
        return data
    target_peak = 10.0 ** (target_db / 20.0)
    gain = min(target_peak / peak, 10.0 ** (12.0 / 20.0))
    return np.clip(data * gain, -1.0, 1.0)


def apply_fades(data: np.ndarray, sample_rate: int, fade_ms: float) -> np.ndarray:
    if data.size == 0 or fade_ms <= 0.0:
        return data
    fade_samples = min(int(sample_rate * fade_ms / 1000.0), data.shape[0] // 3)
    if fade_samples <= 1:
        return data
    result = data.copy()
    curve = np.sin(np.linspace(0.0, np.pi * 0.5, fade_samples, dtype=np.float32))
    result[:fade_samples] *= curve[:, None]
    result[-fade_samples:] *= curve[::-1, None]
    return result


def slice_audio(data: np.ndarray, sample_rate: int, start: float, duration: float | None) -> np.ndarray:
    first = max(0, min(data.shape[0], int(start * sample_rate)))
    if duration is None:
        return data[first:]
    last = max(first, min(data.shape[0], first + int(duration * sample_rate)))
    return data[first:last]


def write_clip(path: Path, data: np.ndarray) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    # libsndfile's Windows Ogg writer can overflow its CFFI callback stack when
    # handed a long buffer in one call. Chunked writes avoid that native crash.
    with sf.SoundFile(
        path,
        mode="w",
        samplerate=TARGET_SAMPLE_RATE,
        channels=data.shape[1],
        format="OGG",
        subtype="VORBIS",
    ) as output:
        chunk_size = TARGET_SAMPLE_RATE
        for first in range(0, data.shape[0], chunk_size):
            output.write(data[first : first + chunk_size])


def build_direct(source_root: Path, output_root: Path) -> list[tuple[str, str]]:
    manifest: list[tuple[str, str]] = []
    for output_name, spec in CLIPS.items():
        source_path = source_root / Path(spec.source)
        if not source_path.is_file():
            raise FileNotFoundError(source_path)

        data, sample_rate = load_audio_segment(source_path, spec.start, spec.duration)
        if spec.reverse:
            data = data[::-1].copy()
        if spec.mono:
            data = to_mono(data)
        data = resample(data, sample_rate)
        data = normalize(data, spec.target_db)
        data = apply_fades(data, TARGET_SAMPLE_RATE, spec.fade_ms)
        write_clip(output_root / f"{output_name}.ogg", data)
        manifest.append((output_name, spec.source))
    return manifest


def moving_average(values: np.ndarray, window: int) -> np.ndarray:
    if window <= 1:
        return values
    kernel = np.ones(window, dtype=np.float32) / window
    return np.convolve(values, kernel, mode="same")


def find_events(
    data: np.ndarray,
    sample_rate: int,
    count: int,
    max_event_seconds: float,
) -> list[tuple[int, int]]:
    mono = np.max(np.abs(data), axis=1)
    envelope = moving_average(mono, max(1, int(sample_rate * 0.006)))
    peak = float(np.max(envelope)) if envelope.size else 0.0
    if peak <= 1e-6:
        return []

    noise = float(np.quantile(envelope, 0.55))
    threshold = max(peak * 0.055, noise * 3.0, 0.001)
    active = np.flatnonzero(envelope >= threshold)
    if active.size == 0:
        return []

    max_gap = int(sample_rate * 0.13)
    min_length = int(sample_rate * 0.045)
    max_length = int(sample_rate * max_event_seconds)
    pad_before = int(sample_rate * 0.025)
    pad_after = int(sample_rate * 0.09)

    events: list[tuple[int, int]] = []
    start = int(active[0])
    previous = start
    for index in active[1:]:
        index = int(index)
        if index - previous > max_gap:
            end = previous + 1
            if end - start >= min_length:
                events.append((max(0, start - pad_before), min(data.shape[0], end + pad_after)))
            start = index
        previous = index

    end = previous + 1
    if end - start >= min_length:
        events.append((max(0, start - pad_before), min(data.shape[0], end + pad_after)))

    trimmed: list[tuple[int, int]] = []
    for start, end in events:
        if end - start > max_length:
            end = start + max_length
        if end > start:
            trimmed.append((start, end))
        if len(trimmed) >= count:
            break

    if len(trimmed) >= count:
        return trimmed

    # Fallback for recordings with a constant noise floor: pick separated peaks.
    separation = max_length
    order = np.argsort(envelope)[::-1]
    centers: list[int] = []
    for raw_index in order:
        index = int(raw_index)
        if any(abs(index - center) < separation for center in centers):
            continue
        centers.append(index)
        if len(centers) >= count:
            break
    centers.sort()

    fallback: list[tuple[int, int]] = []
    half = max_length // 2
    for center in centers:
        start = max(0, center - half)
        end = min(data.shape[0], start + max_length)
        start = max(0, end - max_length)
        fallback.append((start, end))
    return fallback


def build_pools(source_root: Path, output_root: Path) -> list[tuple[str, str]]:
    manifest: list[tuple[str, str]] = []
    for spec in POOLS:
        source_path = source_root / Path(spec.source)
        if not source_path.is_file():
            raise FileNotFoundError(source_path)

        data, sample_rate = load_audio(source_path)
        events = find_events(data, sample_rate, spec.count, spec.max_event_seconds)
        if len(events) < spec.count:
            raise RuntimeError(f"Only {len(events)} events found in {source_path}; expected {spec.count}")

        for index, (first, last) in enumerate(events[: spec.count], start=1):
            event = data[first:last]
            if spec.mono:
                event = to_mono(event)
            event = resample(event, sample_rate)
            event = normalize(event, spec.target_db)
            event = apply_fades(event, TARGET_SAMPLE_RATE, 8.0)
            output_name = f"{spec.prefix}_{index:02d}"
            write_clip(output_root / f"{output_name}.ogg", event)
            manifest.append((output_name, spec.source))
    return manifest


def main() -> int:
    args = parse_args()
    source_root = args.source.resolve()
    output_root = args.output.resolve()
    if not source_root.is_dir():
        raise FileNotFoundError(source_root)

    if args.clean and output_root.exists():
        shutil.rmtree(output_root)
    output_root.mkdir(parents=True, exist_ok=True)

    manifest = build_direct(source_root, output_root)
    manifest.extend(build_pools(source_root, output_root))
    total_bytes = sum(path.stat().st_size for path in output_root.glob("*.ogg"))
    print(f"Built {len(manifest)} clips in {output_root}")
    print(f"Total size: {total_bytes / (1024 * 1024):.2f} MiB")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
