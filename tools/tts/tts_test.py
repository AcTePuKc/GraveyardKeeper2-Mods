"""Run isolated Bulgarian voice-cloning tests for translated game lines.

The script only reads translation/reference files and writes under
generated_audio/tts_test/<model>/. It never writes to mod/VoiceOvers/bg.
"""

from __future__ import annotations

import argparse
import csv
import json
import os
import subprocess
import sys
import tempfile
from dataclasses import dataclass
from datetime import UTC, datetime
from pathlib import Path


PROJECT_ROOT = Path(__file__).resolve().parents[2]
TRANSLATIONS_ROOT = PROJECT_ROOT / "translations"
PACKAGES_ROOT = TRANSLATIONS_ROOT / "packages"
REFERENCES_ROOT = PROJECT_ROOT / "references"
PORTUGUESE_ROOT = REFERENCES_ROOT / "portuguese_reference" / "extracted" / "VoiceOvers" / "pt-br"
OUTPUT_ROOT = PROJECT_ROOT / "generated_audio" / "tts_test"

BULGARIAN_TOOLKIT = Path(os.environ.get("BULGARIAN_TOOLKIT", r"C:\WebStuff\Bulgarian-Speech-Toolkit"))
OMNIVOICE_ROOT = Path(os.environ.get("OMNIVOICE_ROOT", r"C:\WebStuff\Omnivoice-BG"))

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    sys.stderr.reconfigure(encoding="utf-8", errors="replace")


@dataclass(frozen=True)
class ModelSpec:
    model_id: str
    label: str
    voice_cloning: bool
    language_status: str
    python: Path


MODELS = {
    "bgtts-v7": ModelSpec(
        "bgtts-v7",
        "BG-TTS-V7",
        True,
        "Bulgarian local model",
        OMNIVOICE_ROOT / ".venv" / "Scripts" / "python.exe",
    ),
    "omnivoice-bg": ModelSpec(
        "omnivoice-bg",
        "OmniVoice-BG + Bulgarian frontend/adapter",
        True,
        "Bulgarian local model with validated frontend",
        OMNIVOICE_ROOT / ".venv" / "Scripts" / "python.exe",
    ),
}


def _clean(value: object) -> str:
    return str(value or "").strip()


def load_bulgarian_translations() -> dict[str, dict[str, str]]:
    """Return key -> Bulgarian text plus the source package."""
    result: dict[str, dict[str, str]] = {}
    for csv_path in sorted(PACKAGES_ROOT.glob("*.csv")):
        with csv_path.open("r", encoding="utf-8-sig", newline="") as handle:
            for row in csv.DictReader(handle):
                key = _clean(row.get("key"))
                text = _clean(row.get("bg"))
                if key and text and key not in result:
                    result[key] = {"text": text, "source": str(csv_path.resolve())}

    json_path = TRANSLATIONS_ROOT / "voiceover_lines_bg.json"
    if json_path.is_file():
        try:
            payload = json.loads(json_path.read_text(encoding="utf-8"))
        except json.JSONDecodeError:
            payload = {}
        for language in payload.get("languages", []):
            if language.get("language") != "bg":
                continue
            for item in language.get("data", []):
                key = _clean(item.get("key"))
                text = _clean(item.get("value"))
                if key and text and key not in result:
                    result[key] = {"text": text, "source": str(json_path.resolve())}
    return result


def load_portuguese_catalog() -> dict[str, str]:
    path = REFERENCES_ROOT / "voiceover_lines.json"
    payload = json.loads(path.read_text(encoding="utf-8"))
    for language in payload.get("languages", []):
        if language.get("language") == "pt-br":
            return {
                _clean(item.get("key")): _clean(item.get("value"))
                for item in language.get("data", [])
                if _clean(item.get("key")) and _clean(item.get("value"))
            }
    return {}


def resolve_case(key: str, reference_override: Path | None = None) -> dict[str, str]:
    bg = load_bulgarian_translations().get(key)
    if not bg:
        raise ValueError(f"Липсва български превод за key: {key}")

    pt = load_portuguese_catalog().get(key)
    reference = reference_override or (PORTUGUESE_ROOT / f"{key}.wav")
    if not pt:
        raise ValueError(f"Липсва португалският reference текст за key: {key}")
    if not reference.is_file():
        raise FileNotFoundError(f"Липсва Portuguese reference WAV: {reference}")
    if reference.suffix.lower() != ".wav":
        raise ValueError(f"Reference файлът трябва да е WAV: {reference}")
    return {
        "key": key,
        "bg_text": bg["text"],
        "bg_source": bg["source"],
        "pt_text": pt,
        "reference_audio": str(reference.resolve()),
    }


def _base_manifest(case: dict[str, str]) -> dict[str, str]:
    return {
        "id": case["key"],
        "text": case["bg_text"],
        "alignment_text": case["bg_text"],
    }


def _run(command: list[str], cwd: Path, output_dir: Path) -> int:
    output_dir.mkdir(parents=True, exist_ok=True)
    log_path = output_dir / "last_run.log"
    display = " ".join(f'"{part}"' if " " in part else part for part in command)
    print(f"Команда: {display}")
    with log_path.open("w", encoding="utf-8") as log:
        completed = subprocess.run(
            command,
            cwd=cwd,
            stdout=log,
            stderr=subprocess.STDOUT,
            check=False,
        )
    print(f"Изходен код: {completed.returncode}; лог: {log_path}")
    return completed.returncode


def run_bgtts_v7(case: dict[str, str], output_dir: Path, seed: int) -> int:
    model_dir = Path(os.environ.get("BG_TTS_V7_MODEL_DIR", r"C:\WebStuff\BG-TTS-V7"))
    codec_dir = Path(os.environ.get("MIOCODEC_MODEL_DIR", r"C:\WebStuff\MioCodec-25Hz-24kHz"))
    with tempfile.TemporaryDirectory(prefix="voxbench_game_tts_") as temp:
        manifest = Path(temp) / "manifest.jsonl"
        manifest.write_text(json.dumps(_base_manifest(case), ensure_ascii=False) + "\n", encoding="utf-8")
        command = [
            str(MODELS["bgtts-v7"].python),
            str(BULGARIAN_TOOLKIT / "scripts" / "benchmark_bgtts_v7.py"),
            "--manifest", str(manifest),
            "--model-dir", str(model_dir),
            "--codec-dir", str(codec_dir),
            "--reference-audio", case["reference_audio"],
            "--output-dir", str(output_dir),
            "--report", str(output_dir / "report.json"),
            "--seed", str(seed),
        ]
        return _run(command, BULGARIAN_TOOLKIT, output_dir)


def run_omnivoice(case: dict[str, str], output_dir: Path, seed: int) -> int:
    model_dir = Path(os.environ.get("OMNIVOICE_MODEL_DIR", str(OMNIVOICE_ROOT / "models" / "omnivoice-bg")))
    command = [
        str(MODELS["omnivoice-bg"].python),
        str(Path(__file__).resolve().parent / "omnivoice_adapter_runner.py"),
        "--omnivoice-root", str(OMNIVOICE_ROOT),
        "--model-dir", str(model_dir),
        "--text", case["bg_text"],
        "--reference-audio", case["reference_audio"],
        "--reference-text", case["pt_text"],
        "--output", str(output_dir / f"{case['key']}.wav"),
        "--report", str(output_dir / "report.json"),
        "--seed", str(seed),
    ]
    return _run(command, OMNIVOICE_ROOT, output_dir)


RUNNERS = {
    "bgtts-v7": run_bgtts_v7,
    "omnivoice-bg": run_omnivoice,
}


def write_metadata(output_dir: Path, case: dict[str, str], model_ids: list[str], exit_codes: dict[str, int]) -> None:
    payload = {
        "created_at": datetime.now(UTC).isoformat(),
        "key": case["key"],
        "bulgarian_text": case["bg_text"],
        "bulgarian_source": case["bg_source"],
        "portuguese_reference_text": case["pt_text"],
        "portuguese_reference_audio": case["reference_audio"],
        "models": model_ids,
        "exit_codes": exit_codes,
        "note": "Test output only. Never copy this file into mod/VoiceOvers/bg.",
    }
    output_dir.mkdir(parents=True, exist_ok=True)
    (output_dir / f"{case['key']}.json").write_text(
        json.dumps(payload, ensure_ascii=False, indent=2) + "\n", encoding="utf-8"
    )


def list_models() -> int:
    for spec in MODELS.values():
        state = "ready-path" if spec.python.is_file() else "missing-python"
        print(f"{spec.model_id}: {spec.label} | VC=yes | {spec.language_status} | {state}")
    print("VC-only default profiles: " + ", ".join(MODELS))
    return 0


def main() -> int:
    parser = argparse.ArgumentParser(description="Isolated Bulgarian VC test for translated game lines")
    sub = parser.add_subparsers(dest="command", required=True)
    sub.add_parser("list", help="List the configured VC model profiles")

    resolve = sub.add_parser("resolve", help="Show Bulgarian/PT text and reference without generating audio")
    resolve.add_argument("--key", required=True)
    resolve.add_argument("--reference", type=Path)

    generate = sub.add_parser("generate", help="Generate one translated line with one VC model")
    generate.add_argument("--key", required=True)
    generate.add_argument("--model", required=True, help="Model id or all-vc")
    generate.add_argument("--reference", type=Path, help="Optional replacement Portuguese WAV")
    generate.add_argument("--seed", type=int, default=42)
    generate.add_argument("--dry-run", action="store_true")
    args = parser.parse_args()

    if args.command == "list":
        return list_models()

    try:
        case = resolve_case(args.key, args.reference)
    except (FileNotFoundError, ValueError, json.JSONDecodeError) as exc:
        print(f"ГРЕШКА: {exc}", file=sys.stderr)
        return 2

    if args.command == "resolve":
        print(json.dumps(case, ensure_ascii=False, indent=2))
        return 0

    if args.model in {"all-vc", "all-bg-vc", "both"}:
        model_ids = list(MODELS)
    else:
        model_ids = [args.model]
    unknown = [model_id for model_id in model_ids if model_id not in MODELS]
    if unknown:
        print(f"ГРЕШКА: неизвестен модел: {', '.join(unknown)}", file=sys.stderr)
        return 2

    exit_codes: dict[str, int] = {}
    for model_id in model_ids:
        output_dir = OUTPUT_ROOT / model_id
        if args.dry_run:
            print(f"DRY RUN {model_id}: {output_dir}")
            exit_codes[model_id] = 0
            continue
        print(f"\n=== {model_id} / {case['key']} ===")
        exit_codes[model_id] = RUNNERS[model_id](case, output_dir, args.seed)

    if not args.dry_run:
        for model_id in model_ids:
            model_dir = OUTPUT_ROOT / model_id
            write_metadata(model_dir, case, [model_id], {model_id: exit_codes[model_id]})
    return 0 if all(code == 0 for code in exit_codes.values()) else 1


if __name__ == "__main__":
    raise SystemExit(main())
