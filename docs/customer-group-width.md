# Fill the grouped customer editor

Date: November 12, 2026. Source: `9f93851`.

Expanded customer groups centered their contents at the controls' desired widths, leaving addresses and longer notes in roughly 200-pixel columns even when the editor had more room. Their content now stretches across the group. This is one alignment property in the existing group helper; text behavior and business rules do not change.

Native before/after UI Automation measurements on this machine:

| Window width | Field | Before | After |
| --- | --- | ---: | ---: |
| 1280 | Billing address | 200 | 543 |
| 1280 | Sensitivities | 214 | 543 |
| 500 | Billing address | 200 | 360 |
| 500 | Sensitivities | 214 | 360 |

Both window sizes were visually inspected. The controls follow the available editor width, and all six complete multiline values remain intact during resize. Native unchanged-record/New, Keep editing, Discard, and second-owner restart checks pass. Stored customer/permission rows match the earlier checkpoint, with one original permission entry and zero financial movement. Debug builds with zero warnings/errors.

This is a scoped layout check, not a claim of complete usability/accessibility acceptance. Physical touch, larger text, other sections, real Google delivery, signing, and full release acceptance remain separate work.
