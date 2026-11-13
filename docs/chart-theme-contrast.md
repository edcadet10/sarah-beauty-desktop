# Read dashboard charts

Chart legends use readable body text beside colored swatches. Column-series numbers match their legend numbers, so matching a bar to its series does not depend only on color. Income/result use emerald; expenses use gold. Blue and purple distinguish additional series. Exact tables remain available beside every approximate plot.

Shapes and axes use theme-resource styles instead of captured fixed brushes. The palette has light, dark, and Windows contrast-theme mappings. The app follows Windows; no review-only theme override is committed.

## Contrast checks on November 12, 2026

Claim: legend text must reach 4.5:1 and meaningful plot colors 3:1 against their intended surfaces, using the [WCAG text](https://www.w3.org/WAI/WCAG22/Understanding/contrast-minimum.html) and [non-text](https://www.w3.org/WAI/WCAG22/Understanding/non-text-contrast.html) calculations as design checks.

The original named colors, read from the platform, disproved the assumption that the existing legend was readable enough on the light surface:

| Original color | Contrast | Normal text | Plot color |
| --- | ---: | --- | --- |
| SteelBlue | 4.07:1 | Fails | Passes |
| DarkOrange | 2.31:1 | Fails | Fails |
| SeaGreen | 4.21:1 | Fails | Passes |
| MediumPurple | 3.73:1 | Fails | Passes |

The current palette probe checks 42 text pairs and 24 accent/plot pairs across light and dark surfaces. All 66 checks pass. Legend labels use normal text colors; series colors appear in separate swatches and shapes.

Native column, line/point, horizontal bar, and four-series owner charts were captured and visually inspected in light and dark app-only test builds. Numbered labels and legends are present. Source totals and current balances remain correct; original fictional financial history, cancellation records, and issued PDF hashes remain unchanged. Plot-range and coordinate calculations are unchanged. Final Debug builds have zero warnings/errors.

The temporary dark test setting was removed. Runtime switching, actual Windows contrast themes, larger text, keyboard/touch behavior, and complete accessibility acceptance remain separate checks. Palette calculations and selected renders do not establish whole-app conformance.
