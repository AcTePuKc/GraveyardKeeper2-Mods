"""Transfer a voice reference onto another Bulgarian translated line.

This is a test-only operation. It writes only to
generated_audio/tts_test/voice_transfer/<model>/ and never modifies the game.
"""

from __future__ import annotations

import argparse
import json
import os
import subprocess
import sys
import tempfile
from pathlib import Path

from tts_test import (
    MODELS,
    OMNIVOICE_ROOT,
    BULGARIAN_TOOLKIT,
    load_bulgarian_translations,
)


PROJECT_ROOT = Path(__file__).resolve().parents[2]
OUTPUT_ROOT = PROJECT_ROOT / "generated_audio" / "tts_test" / "voice_transfer"


def _read_reference_text(reference_audio: Path, explicit: str | None) -> str:
    if explicit and explicit.strip():
        return explicit.strip()
    candidates = [
        reference_audio.with_suffix(".json"),
        reference_audio.parent / f"{reference_audio.stem}.json",
    ]
    for candidate in candidates:
        if candidate.is_file():
            payload = json.loads(candidate.read_text(encoding="utf-8"))
            value = str(payload.get("bulgarian_text") or "").strip()
            if value:
                return value
    raise ValueError(
        "За OmniVoice е нужен текстът на voice reference WAV-а. "
        "Дай --voice-reference-text или постави съответния .json до WAV файла."
    )


def _run(command: list[str], cwd: Path, output_dir: Path) -> int:
    output_dir.mkdir(parents=True, exist_ok=True)
    log_path = output_dir / "last_run.log"
    display = " ".join(f'"{part}"' if " " in part else part for part in command)
    print(f"Команда: {display}")
    with log_path.open("w", encoding="utf-8") as log:
        result = subprocess.run(command, cwd=cwd, stdout=log, stderr=subprocess.STDOUT, check=False)
    print(f"Изходен код: {result.returncode}; лог: {log_path}")
    return result.returncode


def _run_omnivoice(text: str, reference_audio: Path, reference_text: str, output_dir: Path, seed: int) -> int:
    output = output_dir / "transferred.wav"
    command = [
        str(MODELS["omnivoice-bg"].python),
        str(Path(__file__).resolve().parent / "omnivoice_adapter_runner.py"),
        "--omnivoice-root", str(OMNIVOICE_ROOT),
        "--model-dir", str(Path(os.environ.get("OMNIVOICE_MODEL_DIR", OMNIVOICE_ROOT / "models" / "omnivoice-bg"))),
        "--text", text,
        "--reference-audio", str(reference_audio),
        "--reference-text", reference_text,
        "--output", str(output),
        "--report", str(output_dir / "report.json"),
        "--seed", str(seed),
    ]
    return _run(command, OMNIVOICE_ROOT, output_dir)


def _run_v7(text: str, reference_audio: Path, output_dir: Path, seed: int) -> int:
    model_dir = Path(os.environ.get("BG_TTS_V7_MODEL_DIR", r"C:\WebStuff\BG-TTS-V7"))
    codec_dir = Path(os.environ.get("MIOCODEC_MODEL_DIR", r"C:\WebStuff\MioCodec-25Hz-24kHz"))
    with tempfile.TemporaryDirectory(prefix="voxbench_voice_transfer_") as temp:
        manifest = Path(temp) / "manifest.jsonl"
        manifest.write_text(
            json.dumps({"id": "transferred", "text": text, "alignment_text": text}, ensure_ascii=False) + "\n",
            encoding="utf-8",
        )
        command = [
            str(MODELS["bgtts-v7"].python),
            str(BULGARIAN_TOOLKIT / "scripts" / "benchmark_bgtts_v7.py"),
            "--manifest", str(manifest),
            "--model-dir", str(model_dir),
            "--codec-dir", str(codec_dir),
            "--reference-audio", str(reference_audio),
            "--output-dir", str(output_dir),
            "--report", str(output_dir / "report.json"),
            "--seed", str(seed),
        ]
        return _run(command, BULGARIAN_TOOLKIT, output_dir)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--content-key", default="1_intro_prison_wake_18_a_1")
    parser.add_argument("--voice-reference", type=Path, required=True)
    parser.add_argument("--voice-reference-text")
    parser.add_argument("--model", choices=("omnivoice-bg", "bgtts-v7", "both"), default="both")
    parser.add_argument("--seed", type=int, default=42)
    args = parser.parse_args()

    if not args.voice_reference.is_file():
        raise FileNotFoundError(args.voice_reference)
    content = load_bulgarian_translations().get(args.content_key)
    if not content:
        raise ValueError(f"Липсва български текст за key: {args.content_key}")
    reference_text = _read_reference_text(args.voice_reference, args.voice_reference_text)
    models = ["omnivoice-bg", "bgtts-v7"] if args.model == "both" else [args.model]
    exit_codes: dict[str, int] = {}
    for model in models:
        output_dir = OUTPUT_ROOT / model / args.content_key
        output_dir.mkdir(parents=True, exist_ok=True)
        if model == "omnivoice-bg":
            exit_codes[model] = _run_omnivoice(content["text"], args.voice_reference.resolve(), reference_text, output_dir, args.seed)
        else:
            exit_codes[model] = _run_v7(content["text"], args.voice_reference.resolve(), output_dir, args.seed)
        metadata = {
            "content_key": args.content_key,
            "content_text": content["text"],
            "content_source": content["source"],
            "voice_reference": str(args.voice_reference.resolve()),
            "voice_reference_text": reference_text,
            "model": model,
            "seed": args.seed,
            "exit_code": exit_codes[model],
            "note": "Test-only voice transfer. Never copy this output into mod/VoiceOvers/bg.",
        }
        (output_dir / "transfer.json").write_text(json.dumps(metadata, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    return 0 if all(code == 0 for code in exit_codes.values()) else 1


if __name__ == "__main__":
    raise SystemExit(main())
