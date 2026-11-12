# Size common entry controls for touch

Text and passphrase entry frames and combo-box frames use a 44-effective-pixel minimum. Checkboxes use a minimum 44 × 44 target. Existing primary action buttons already have a 44-pixel minimum height. Keep the native control templates, focus behavior, and theme colors.

[Microsoft's touch guidance](https://learn.microsoft.com/en-us/windows/apps/develop/input/touch-interactions) treats some wide 32-pixel controls as touchable and suggests considering 44 × 44 targets for a touch-optimized interface. Larger controls take more vertical space; this change does not prove faster or more accurate touch use.

## Geometry checks on November 12, 2026

Claim: the shared resource change must increase the entry area, not merely make its label look larger. Reject it if the measured controls do not gain the expected 12 effective pixels or native selection stops working.

UI Automation bounds include the header. A 59-pixel text-control bound therefore did not establish a 44-pixel editing area. The test first measured the unchanged controls and checked the native template's entry-frame resource, then measured the same labels after the resource change. Window DPI was read directly; this display's scale was 1.

| Control | Before, including header | After, including header |
| --- | ---: | ---: |
| Owner selection | 64 | 76 |
| Passphrase | 59 | 71 |
| Customer search | 59 | 71 |
| Customer name | 59 | 71 |
| Preferred contact | 64 | 76 |
| Include archived checkbox | 32 | 44 |

Every tested entry gained 12 effective pixels with its label unchanged, consistent with the template minimum increasing from 32 to 44. Native dropdown selection still works. Dashboard geometry/actions pass at 1280/900/500 pixels, and same-name customer references and selection still pass at 1280/500 pixels. Financial history and issued PDF hashes remain unchanged. Debug builds cleanly, and the selected-customer render was inspected.

This is a shared entry-control sizing change. It does not certify every internal sub-button, popup item, chart target, or navigation target. Physical Surface touch, touch-keyboard occlusion, larger text, and broader keyboard/state acceptance remain pending. Related fields can use responsive rows to offset the extra form height.
