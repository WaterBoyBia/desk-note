from pathlib import Path

from PIL import Image


ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "icon" / "icon.png"
OUTPUT = ROOT / "src" / "DeskNote.App" / "Resources" / "desk-note.ico"
SIZES = (16, 20, 24, 32, 40, 48, 64, 128, 256)


def main() -> None:
    with Image.open(SOURCE) as source:
        if source.format != "PNG":
            raise ValueError(f"Expected PNG source, got {source.format}")
        if source.width != source.height:
            raise ValueError("Icon source must be square")

        rgba = source.convert("RGBA")
        OUTPUT.parent.mkdir(parents=True, exist_ok=True)
        rgba.save(OUTPUT, format="ICO", sizes=[(size, size) for size in SIZES])

    with Image.open(OUTPUT) as generated:
        actual_sizes = {size[0] for size in generated.ico.sizes()}
        missing = set(SIZES) - actual_sizes
        if missing:
            raise ValueError(f"Generated icon is missing sizes: {sorted(missing)}")

    print(f"Generated {OUTPUT.relative_to(ROOT)} from {SOURCE.relative_to(ROOT)}")


if __name__ == "__main__":
    main()
