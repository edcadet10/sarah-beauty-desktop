# Document dependencies

The PDF feature uses these versions:

| Dependency | Use | License/source |
|---|---|---|
| PDFsharp-MigraDoc 6.2.4 | Render client documents with automatic pagination | [MIT license at the pinned tag](https://github.com/empira/PDFsharp/blob/v6.2.4/LICENSE) |
| System.Security.Cryptography.Pkcs 10.0.15 | Override the older cryptography dependency selected by PDFsharp | [Microsoft package](https://www.nuget.org/packages/System.Security.Cryptography.Pkcs/10.0.15) |
| PdfPig 0.1.16 | Extract text from actual PDFs in tests; not shipped with the app | [Project and Apache-2.0 license](https://github.com/UglyToad/PdfPig) |

PDFsharp's current repository uses different licensing from the pinned 6.2.4 release. The version-specific MIT license and the installed package's license metadata were checked before use. Review licensing and behavior before upgrading this dependency. The copied 6.2.4 notice is included in build and publish output under `ThirdParty/PDFsharp`.

PDF rendering uses a custom resolver that reads Arial faces from the installed Windows fonts directory. The app does not redistribute those font files. See [PDFsharp font resolving](https://docs.pdfsharp.net/PDFsharp/Topics/Fonts/Font-Resolving.html).

On November 10, 2026, NuGet's vulnerability scan initially flagged the transitive PKCS 8.0.1 version. Adding the supported 10.0.15 override removed those reported advisories. The production business project then had no reported vulnerable packages in the configured NuGet source. This records that scan's result; repeat it before release.
