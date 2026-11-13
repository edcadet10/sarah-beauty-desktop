# Sarah Beauty visual design

Use emerald, cream, and subtle gold, as selected by the owner. Emerald identifies primary actions. Cream provides the light canvas, warm white provides working surfaces, and gold stays a restrained decorative accent. Keep records, dates, amounts, and the next action easy to find. Beauty is a preference, not a test result.

| Role | Light | Dark |
| --- | --- | --- |
| Canvas | `#FBF8F0` | `#14251F` |
| Surface | `#FFFEFA` | `#1C3229` |
| Supporting surface | `#EEE6D4` | `#314036` |
| Primary text | `#22372D` | `#F8F4E8` |
| Secondary text | `#56665A` | `#C4CFBE` |
| Primary action | `#174E3C` | `#9FD7B4` |
| Gold accent | `#86682A` | `#D8BE82` |

The theme follows Windows. Light and dark dictionaries supply readable colors. The contrast-theme dictionary uses Windows system colors; it does not impose the brand palette. Preserve standard keyboard, focus, disabled, and validation behavior when styling controls. Labels and symbols must communicate state without relying only on color.

## Contrast check

Claim: the chosen custom text colors remain readable on their intended surfaces. Reject a normal-text pair below 4.5:1 or a meaningful accent indicator below 3:1, using the [WCAG text](https://www.w3.org/WAI/WCAG22/Understanding/contrast-minimum.html) and [non-text contrast](https://www.w3.org/WAI/WCAG22/Understanding/non-text-contrast.html) calculations as design checks. These checks do not establish whole-app accessibility conformance.

Cream-white text on the emerald primary action measures 9.50:1. The committed palette probe checks 42 text pairs and 24 accent/plot pairs across light and dark themes; all 66 pass. The lowest text ratio is 4.91:1. Gold is checked for indicator contrast but is not used for body text or as the only indication of state. [Chart checks](chart-theme-contrast.md) document the original legend failures, theme-aware swatches, and numbered series.

```powershell
pwsh -NoProfile -File tools/test-ui-palette.ps1
```

The probe reads the actual XAML brush values, including primary button normal, hover, and pressed colors. It does not measure all inherited control colors, charts, disabled controls, or Windows contrast-theme preferences.

## Native checks on November 12, 2026

The native light render shows emerald primary actions and selection indicators, a cream window canvas, and a small gold title-bar mark. Sign-in has a bounded form width, and long section titles wrap. Explicit control aliases are required: the initial shared accent fill override did not change the default primary button. The title bar also uses the canvas color rather than an unrelated system-tinted backdrop.

Runtime checks rejected two assumptions. A proposed path-based title-bar mark crashed at startup with an invalid-parameter error; replacing it with a native font icon restored startup. This does not establish the underlying framework cause. The first dark render retained black Windows caption buttons against the dark canvas; the window now applies the current title text color to normal and inactive caption buttons. The corrected dark render has readable caption controls.

Light and dark sign-in, dashboard, and customer screens were inspected at 1280 × 900. Dark dashboard checks also pass at 1280, 900, and 500 pixels, including exact amounts, detail actions, and a nested reconciliation shortcut. Dark mode was forced temporarily in an app-only test build. That override and private startup diagnostics were removed; the committed app follows Windows without changing system settings.

The desktop builds with zero warnings/errors. The themed executable reads retained fictional customer, cancellation, PDF, and reopened reconciliation records, refreshes the correct dashboard amounts, and reopens under the second owner. Financial history and issued PDF hashes remain unchanged. This visual change does not alter financial calculations or business schemas.

Grouped navigation and a responsive dashboard are implemented as separate changes. Runtime Windows contrast-theme inspection, full keyboard traversal, larger Windows text, entry-form refinement, and physical Surface touch checks remain pending. Inherited control colors and caption hover/pressed states still need the broader accessibility review. Do not call this a finished usability review.

References: [Microsoft color guidance](https://learn.microsoft.com/en-us/windows/apps/design/signature-experiences/color), [theme resources](https://learn.microsoft.com/en-us/windows/apps/develop/platform/xaml/xaml-theme-resources), and [Google editorial guidance](https://developers.google.com/style).
