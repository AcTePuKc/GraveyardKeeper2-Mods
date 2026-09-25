"""Small subprocess runner for the existing Chatterbox UI environment."""

from __future__ import annotations

import argparse
import sys
from pathlib import Path


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--text", required=True)
    parser.add_argument("--reference-audio", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--language-id", default="bg")
    args = parser.parse_args()

    ui_root = Path(__file__).resolve().parents[3] / "Chatterbox TTS UI"
    sys.path.insert(0, str(ui_root))
    from model_backends import BACKEND_MULTILINGUAL, get_supported_languages_for_backend, load_chatterbox_model

    supported = get_supported_languages_for_backend(BACKEND_MULTILINGUAL)
    if args.language_id not in supported:
        supported_ids = ", ".join(sorted(supported))
        raise RuntimeError(
            f"Chatterbox does not officially expose language '{args.language_id}'. "
            f"Supported ids: {supported_ids}"
        )
    model = load_chatterbox_model("ResembleAI/chatterbox", BACKEND_MULTILINGUAL, "cuda")
    audio = model.generate(
        args.text,
        audio_prompt_path=str(args.reference_audio),
        language_id=args.language_id,
    )
    import soundfile as sf

    samples = audio.detach().float().cpu().numpy()
    if samples.ndim > 1:
        samples = samples[0]
    args.output.parent.mkdir(parents=True, exist_ok=True)
    sf.write(str(args.output), samples, int(model.sr))
    print(f"Saved: {args.output}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
