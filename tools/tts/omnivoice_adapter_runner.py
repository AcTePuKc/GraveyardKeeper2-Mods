"""Generate one line with the OmniVoice-BG desktop frontend/adapter.

This bridge intentionally mirrors the configuration used by the local
OmniVoice-BG PySide6 application.  It keeps the game test isolated from the
OmniVoice repository while still applying its Bulgarian frontend before
inference.
"""

from __future__ import annotations

import argparse
import json
from pathlib import Path
import sys


if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    sys.stderr.reconfigure(encoding="utf-8", errors="replace")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--omnivoice-root", type=Path, required=True)
    parser.add_argument("--model-dir", type=Path, required=True)
    parser.add_argument("--text", required=True)
    parser.add_argument("--reference-audio", type=Path, required=True)
    parser.add_argument("--reference-text", required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--report", type=Path, required=True)
    parser.add_argument("--steps", type=int, default=16)
    parser.add_argument("--seed", type=int, default=42)
    parser.add_argument("--time-context", choices=("neutral", "afternoon"), default="neutral")
    args = parser.parse_args()

    root = args.omnivoice_root.resolve()
    sys.path.insert(0, str(root / "src"))

    from omnivoice_bg.backends.omnivoice import OmniVoiceBGBackend  # noqa: PLC0415
    from omnivoice_bg.text import BulgarianFrontendPipeline  # noqa: PLC0415

    frontend_categories = frozenset(
        {
            "identifier",
            "phone_number",
            "date",
            "year",
            "time",
            "clock_statement",
            "contextual_cardinal",
        }
    )
    frontend = BulgarianFrontendPipeline(
        abbreviation_registry_path=root / "configs" / "abbreviation_registry.json",
        pronunciation_overrides_path=root / "configs" / "pronunciation_overrides.validated.json",
        enabled_number_categories=frontend_categories,
    )
    frontend_result = frontend.process(args.text, time_context=args.time_context)
    backend = OmniVoiceBGBackend(model_path=args.model_dir, language="bg")

    try:
        audio = backend.generate(
            frontend_result.candidate_text,
            reference_audio=args.reference_audio,
            reference_text=args.reference_text,
            num_step=args.steps,
            seed=args.seed,
        )
        backend.save_audio(audio, args.output)
        report = {
            "status": "ok",
            "backend": "omnivoice-bg",
            "frontend": "OmniVoice-BG desktop configuration",
            "original_text": frontend_result.original_text,
            "generated_text": frontend_result.candidate_text,
            "normalized_text": frontend_result.normalized_text,
            "normalization_changes": list(frontend_result.normalization_changes),
            "phrase_changes": list(frontend_result.phrase_changes),
            "number_candidates": [candidate.__dict__ for candidate in frontend_result.number_candidates],
            "reference_audio": str(args.reference_audio.resolve()),
            "reference_text": args.reference_text,
            "output": str(args.output.resolve()),
            "steps": args.steps,
            "seed": args.seed,
            "time_context": args.time_context,
        }
    except Exception as exc:
        report = {
            "status": "error",
            "backend": "omnivoice-bg",
            "frontend": "OmniVoice-BG desktop configuration",
            "original_text": args.text,
            "generated_text": frontend_result.candidate_text,
            "error": repr(exc),
        }
        raise
    finally:
        backend.unload()

    args.report.parent.mkdir(parents=True, exist_ok=True)
    args.report.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(f"Generated {args.output}")
    print(f"Frontend text: {frontend_result.candidate_text}")
    print(f"Report: {args.report}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
