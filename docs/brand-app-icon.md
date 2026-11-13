# Use the Sarah Beauty Windows icon

The unpackaged Windows app now uses an emerald tile with cream and pale-gold sparkles. The executable embeds `Assets/AppIcon.ico`; the existing window icon call uses the same file. This replaces the starter X in those two places. MSIX template logos are outside this change.

`Assets/AppMark.svg` is the editable vector source. On Windows, run `pwsh -NoProfile -File tools/build-app-icon.ps1` to regenerate the committed icon. The script has separate path, tile, painting, frame, and file-writing functions. It supports only the vector commands used in this source. Normal builds use the committed icon and do not require regeneration.

## Check the result

The claim was that the new mark survives compilation and native startup at small Windows icon sizes. A default executable icon, mismatched running-window image, invalid frame, or failed startup would reject that claim.

- The ICO contains ten valid, transparent-corner PNG frames: 16, 20, 24, 32, 40, 48, 64, 96, 128, and 256 pixels. Directory offsets, lengths, dimensions, and the final file length agree. Regeneration preserves the complete file hash.
- Visual inspection covers 16, 32, and 256 pixels. Cream against emerald measures 8.71:1 and pale gold against emerald 5.33:1 using WCAG relative luminance. These measurements apply to the full colors, not every antialiased edge or the tile against every desktop background.
- Windows Debug builds with zero warnings or errors. The self-contained publish contains 575 files totaling 313,814,533 bytes, including the new SVG. Windows extracts the expected icon from that executable; the running window also exposes it. Both observed 32-pixel images match the committed ICO frame exactly.
- The published app opens the existing fictional TEST business. The second owner can read the deliberately edited multiline loan terms and unchanged multiline evidence, then close normally. Independent saved-record comparison preserves the customer and loan facts, exact CR/LF/CRLF values, one original customer permission, and zero financial movements.

This check establishes the observed executable/window rendering and native reopen on the development computer. It does not establish physical Surface rendering, a cleared Explorer icon cache, every Windows scale/theme, clean-machine installation, or production readiness.

Microsoft's [icon construction guide](https://learn.microsoft.com/en-us/windows/apps/design/iconography/app-icon-construction) explains size selection and the minimum ICO sizes. Its [compiler resource options](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/compiler-options/resources) describe embedding an ICO for File Explorer. The visual palette follows the user's emerald, cream, and subtle-gold choice.
