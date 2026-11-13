# Include runtime package notices in Windows downloads

The verified preview at `d1fbb426dcf96244f647ad3cc496afc762d703f8` bundles selected .NET, Google, PDFsharp, recovery and Windows App SDK notices. Comparing its actual `.deps.json` with the downloaded `ThirdParty` directory finds no SQLitePCLRaw attribution and omits several individual runtime-package notice files. The existing selected notices do not establish complete package coverage.

## Assemble notices from resolved packages

The publish command now reads the actual published dependency list and restored NuGet package folders. It copies each runtime package's supplied license/notice/credit files into `ThirdParty/RuntimePackages/<package>/<version>/`, checks source/output SHA-256, and writes an inventory of all declared runtime packages and their copied documents. It retains the existing runtime/recovery notices.

For packages that declare a license expression but ship no license text, a small checked-in mapping pins the exact package/version, expected expression, and source documents. Unknown packages, missing files, or an unreviewed version fail publishing. The build performs no additional downloads for this step, and application operation remains offline.

The new source texts are the upstream [SQLitePCLRaw 2.1.12 license](https://github.com/ericsink/SQLitePCL.raw/blob/v2.1.12/LICENSE.TXT), [SQLitePCLRaw notices](https://github.com/ericsink/SQLitePCL.raw/blob/v2.1.12/NOTICE.TXT), and [EF Core 10.0.12 license](https://github.com/dotnet/efcore/blob/v10.0.12/LICENSE.txt). Their SHA-256 values are respectively `CFC7749B96F63BD31C3C42B5C471BF756814053E847C10F3EB003417BC523D30`, `485B276B3D2BFAA26DF348E1E5C84DF3648981E09B88531A6A53006F0705C24B`, and `AE48DF11A335DC1A615F4F938B69CBA73BCF4485C4F97AF49B38EFB0F216353B`. Existing pinned PDFsharp and .NET license texts cover the remaining explicit fallbacks.

## Check package coverage

The claim is that every NuGet runtime package declared by this published app has copied license documents with an exact inventory. An omitted package, mismatched hash, or successful publication with an unknown package rejects it.

An independent read of the published dependency list and inventory matches all 29 packages and all 44 copied notice files, with exact SHA-256 values and no duplicate paths. The payload now contains 620 files. The first assembly fails on a missing explicit ProtectedData fallback; adding its pinned .NET license source resolves that failure. A separate fictional unknown-package probe fails assembly and produces no successful inventory. The notice-bearing app also opens the retained TEST business as the second owner and reads its approved trip attribution.

This is a package-document inventory and packaging correction. It does not establish legal approval of every distribution condition, a signing decision, or clean-machine/device acceptance. Those release decisions remain open.
