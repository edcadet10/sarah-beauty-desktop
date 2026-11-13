# Unlock from the keyboard

The `818ec4f` download does not submit sign-in when Enter is pressed in the PIN input. A native probe posts keyboard messages only to the app's verified process/focus window. Enter leaves sign-in open, while Space on the Sign in button authenticates, providing a positive control for the same keyboard route. Every saved table remains exact.

## Submit the selected credential

Password and PIN inputs now handle Enter through the same guarded sign-in action as the button. Held-key repeats and disabled/unloaded views do not start another attempt. Errors remain on the sign-in screen, credentials clear after each attempt and controls recover for retry. Authentication, owner selection, lockout and financial authority are unchanged.

Microsoft's [keyboard event guide](https://learn.microsoft.com/en-us/windows/apps/develop/input/keyboard-events) describes focus, routed key events and the built-in Space/Enter behavior of buttons. The correction handles credential-field Enter explicitly rather than relying on the button to receive a key while another control has focus.

## Try Enter, repetition and rejection

The claim is that Enter from either credential field performs the existing sign-in action once and retains error recovery. A false success, a repeat-triggered attempt, an unusable retry or changed business facts rejects the claim.

Native corrected probes pass PIN Enter, password Enter, ignored held-key repeat, a wrong-PIN error and correct retry. Actual owner headers identify Morgan for PIN and Alex for password. All stored business tables, customer, one posted expense and two balanced journal lines remain exact. These tests use window-specific messages and accessibility focus; they do not inject global keyboard/mouse input or establish physical Surface keyboard/touch acceptance.

Separate Tab probes reach owner/method/credential/Sign in in three steps, customer Save in eight, Money Save draft in fourteen, and both PIN-setting actions in four. Keyboard Space opens New customer and Prepare movement without saving anything. One longer traversal stopped when focus left the owned app; that interruption is not counted as a pass. Separate completed probes cover the remaining paths, with every saved table retained.

Windows Debug has zero warnings/errors and self-contained publish succeeds. Business/test/recovery/tool sources match the complete 1,700-case pass. The successful [GitHub `a6f3b9f` preview](https://github.com/edcadet10/sarah-beauty-desktop/actions/runs/43444824612/artifacts/14047403190) matches all 620 paths, sizes and SHA-256 hashes. With the exact downloaded executable's outbound access blocked, Enter/repeat/error/retry and both owner headers pass with all saved tables retained. The owned firewall rule is removed afterward. This immutable source is the new default preview.
