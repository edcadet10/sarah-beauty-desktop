# Sarah Beauty visual design

Use deep plum for primary actions, warm ivory for the light canvas, white for working surfaces, and blush for supporting surfaces. Keep color restrained so records, dates, amounts, and the next action remain easy to find. This is the initial direction while owner feedback is pending; beauty is a preference, not a test result.

The theme follows Windows. Light and dark dictionaries supply readable colors. The contrast-theme dictionary uses Windows system colors; it does not impose the brand palette. Preserve standard keyboard, focus, disabled, and validation behavior when styling controls. Labels and symbols must communicate state without relying only on color.

## Contrast check

Claim: the chosen custom text colors remain readable on their intended surfaces. Reject a normal-text pair below 4.5:1 or a meaningful accent indicator below 3:1, using the [WCAG text](https://www.w3.org/WAI/WCAG22/Understanding/contrast-minimum.html) and [non-text contrast](https://www.w3.org/WAI/WCAG22/Understanding/non-text-contrast.html) calculations as design checks. These checks do not establish whole-app accessibility conformance.

The first blush button candidate (`#C58DA8` with white text) measured 2.71:1 and was rejected. White on the selected plum (`#633F59`) measures 8.80:1. The committed palette probe checks 42 text pairs and six accent/surface pairs across light and dark themes; all pass. The lowest text ratio is 5.03:1.

```powershell
pwsh -NoProfile -File tools/test-ui-palette.ps1
```

The probe reads the actual XAML brush values, including primary button normal, hover, and pressed colors. It does not measure all inherited control colors, charts, disabled controls, or Windows contrast-theme preferences.

## Native checks on November 12, 2026

The first 1280 × 900 native capture disproved the assumption that overriding shared accent fill brushes changed the primary button: the default style retained the system accent through a resource alias. Explicit primary-button and navigation indicator aliases corrected this. A second native capture shows the plum sign-in button and selection indicator. Sign-in has a bounded form width, and long section titles wrap.

The desktop builds with zero warnings/errors. The themed executable reads retained fictional customer, cancellation, PDF, and reopened reconciliation records, refreshes the correct dashboard amounts, and reopens under the second owner. Financial history and issued PDF hashes remain unchanged. This visual change does not alter financial calculations or business schemas.

Runtime dark/contrast-theme inspection, narrow layouts, keyboard traversal, larger Windows text, and physical Surface touch checks remain pending. Navigation grouping, dashboard hierarchy, and entry-form layout are separate focused changes. Do not call this a finished usability review.

References: [Microsoft color guidance](https://learn.microsoft.com/en-us/windows/apps/design/signature-experiences/color), [theme resources](https://learn.microsoft.com/en-us/windows/apps/develop/platform/xaml/xaml-theme-resources), and [Google editorial guidance](https://developers.google.com/style).
