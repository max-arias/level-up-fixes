from __future__ import annotations

from html import escape
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
OUTPUT = ROOT / "build" / "quality-chance-chart.svg"
WIDTH = 1226
HEIGHT = 700
PLOT_LEFT = 76
PLOT_RIGHT = 22
PLOT_TOP = 58
PLOT_BOTTOM = 140
PLOT_WIDTH = WIDTH - PLOT_LEFT - PLOT_RIGHT
PLOT_HEIGHT = HEIGHT - PLOT_TOP - PLOT_BOTTOM

EARLY_WEIGHTS = [79.0, 15.0, 4.0, 1.0, 1.0]
LATE_WEIGHTS = [20.0, 41.0, 21.0, 10.0, 8.0]
QUALITY_CHANCE = 4.0
QUALITY_WEIGHTS = [70.0, 20.0, 8.0, 2.0]

TIER_SERIES = [
    ("Common (Tier 1)", "#f5f5f5", 0),
    ("Uncommon (Tier 2)", "#7bd64a", 1),
    ("Legendary (Tier 3)", "#e9683f", 2),
    ("Lunar", "#6fa9e8", 3),
    ("Boss", "#eee95c", 4),
]
QUALITY_SERIES = [
    ("Any quality", "#ffffff", QUALITY_CHANCE, False),
    ("Quality Uncommon", "#75d65a", QUALITY_CHANCE * QUALITY_WEIGHTS[0] / 100.0, True),
    ("Quality Rare", "#c08cff", QUALITY_CHANCE * QUALITY_WEIGHTS[1] / 100.0, True),
    ("Quality Epic", "#ff9b4a", QUALITY_CHANCE * QUALITY_WEIGHTS[2] / 100.0, True),
    ("Quality Legendary", "#ff5b5b", QUALITY_CHANCE * QUALITY_WEIGHTS[3] / 100.0, True),
]


def lerp(a: float, b: float, t: float) -> float:
    return a + (b - a) * t


def tier_probabilities(tokens: int) -> list[float]:
    t1_to_2 = min(1.0, max(0.0, tokens / 10.0))
    t2_to_3 = min(1.0, max(0.0, (tokens - 10.0) / 20.0))
    middle = [(early + late) * 0.5 for early, late in zip(EARLY_WEIGHTS, LATE_WEIGHTS)]
    weights = [
        lerp(early, lerp(mid, late, t2_to_3), t1_to_2)
        for early, mid, late in zip(EARLY_WEIGHTS, middle, LATE_WEIGHTS)
    ]
    total = sum(weights)
    return [weight * 100.0 / total for weight in weights]


def x_position(tokens: int) -> float:
    return PLOT_LEFT + (tokens - 1) * PLOT_WIDTH / 49.0


def y_position(percent: float) -> float:
    return PLOT_TOP + PLOT_HEIGHT - percent * PLOT_HEIGHT / 100.0


def polyline(points: list[tuple[float, float]], color: str, width: float = 2.5, dashed: bool = False) -> str:
    point_text = " ".join(f"{x:.2f},{y:.2f}" for x, y in points)
    dash = ' stroke-dasharray="8 6"' if dashed else ""
    return f'<polyline points="{point_text}" fill="none" stroke="{color}" stroke-width="{width}"{dash}/>'


def text(x: float, y: float, value: str, size: int, color: str = "#e8e8e8", anchor: str = "start", weight: str = "normal") -> str:
    return (
        f'<text x="{x:.2f}" y="{y:.2f}" fill="{color}" font-family="Arial, sans-serif" '
        f'font-size="{size}px" text-anchor="{anchor}" font-weight="{weight}">{escape(value)}</text>'
    )


def build_svg() -> str:
    parts = [
        f'<svg xmlns="http://www.w3.org/2000/svg" width="{WIDTH}" height="{HEIGHT}" viewBox="0 0 {WIDTH} {HEIGHT}">',
        '<rect width="100%" height="100%" fill="#292929"/>',
        f'<rect x="{PLOT_LEFT}" y="{PLOT_TOP}" width="{PLOT_WIDTH}" height="{PLOT_HEIGHT}" fill="#303030" stroke="#171717"/>',
        text(WIDTH / 2, 25, "LevelUpChoices default tier probabilities with Item Qualities promotion", 18, weight="bold", anchor="middle"),
        text(WIDTH / 2, 45, "Quality promotion is post-roll: base tier probabilities stay unchanged; quality rates are shown as dashed lines.", 12, "#bcbcbc", anchor="middle"),
    ]

    for percent in range(0, 101, 10):
        y = y_position(percent)
        parts.append(f'<line x1="{PLOT_LEFT}" y1="{y:.2f}" x2="{WIDTH - PLOT_RIGHT}" y2="{y:.2f}" stroke="#454545" stroke-width="1"/>')
        parts.append(text(PLOT_LEFT - 10, y + 4, f"{percent}%", 11, "#c8c8c8", anchor="end"))

    for tokens in range(1, 51, 5):
        x = x_position(tokens)
        parts.append(f'<line x1="{x:.2f}" y1="{PLOT_TOP}" x2="{x:.2f}" y2="{PLOT_TOP + PLOT_HEIGHT}" stroke="#252525" stroke-width="1"/>')
        parts.append(text(x, PLOT_TOP + PLOT_HEIGHT + 20, str(tokens), 11, "#c8c8c8", anchor="middle"))

    for label, color, index in TIER_SERIES:
        points = [(x_position(tokens), y_position(tier_probabilities(tokens)[index])) for tokens in range(1, 51)]
        parts.append(polyline(points, color, 2.8))

    for label, color, percent, dashed in QUALITY_SERIES:
        points = [(x_position(1), y_position(percent)), (x_position(50), y_position(percent))]
        parts.append(polyline(points, color, 2.2, dashed))

    parts.extend([
        text(WIDTH / 2, HEIGHT - 105, "Tokens used", 13, anchor="middle"),
        f'<text x="18" y="{PLOT_TOP + PLOT_HEIGHT / 2:.2f}" fill="#e8e8e8" font-family="Arial, sans-serif" '
        f'font-size="13px" text-anchor="middle" transform="rotate(-90 18 {PLOT_TOP + PLOT_HEIGHT / 2:.2f})">Chance</text>',
    ])

    legend_x = PLOT_LEFT + 6
    legend_y = HEIGHT - 49
    for index, (label, color, _) in enumerate(TIER_SERIES):
        x = legend_x + index * 205
        parts.append(f'<line x1="{x}" y1="{legend_y - 4}" x2="{x + 25}" y2="{legend_y - 4}" stroke="{color}" stroke-width="3"/>')
        parts.append(text(x + 32, legend_y, label, 11, "#eeeeee"))

    quality_legend_y = HEIGHT - 16
    for index, (label, color, _, dashed) in enumerate(QUALITY_SERIES):
        x = legend_x + index * 205
        dash = ' stroke-dasharray="8 6"' if dashed else ""
        parts.append(f'<line x1="{x}" y1="{quality_legend_y - 4}" x2="{x + 25}" y2="{quality_legend_y - 4}" stroke="{color}" stroke-width="3"{dash}/>')
        parts.append(text(x + 32, quality_legend_y, label, 11, "#eeeeee"))

    parts.append('</svg>')
    return "\n".join(parts)


if __name__ == "__main__":
    OUTPUT.write_text(build_svg(), encoding="utf-8")
    print(OUTPUT)
