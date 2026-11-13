# Download a Windows developer preview

The developer preview is a self-contained Windows x64 build for review. It is unsigned. Physical-device, clean-machine, signing, and complete release acceptance remain open. Google authorization and delivery are optional and do not gate the offline app.

## Download and open the app

1. Download the [verified Windows preview ZIP](https://github.com/edcadet10/sarah-beauty-desktop/releases/download/windows-preview-2026-11-13-simple-setup/SarahBeauty-Windows-preview.zip). A GitHub sign-in is not required. The [download website](https://edcadet10.github.io/sarah-beauty-desktop/) links to this same file.
2. Extract the entire archive. Keep its contents together.
3. Open `SarahBeautyDesktop/SarahBeauty.Desktop.exe`. Enter **Business name**, **Username**, and **Password**, then select **Create my workspace**. The dashboard opens immediately. Add the remaining details, another user, or optional [PIN unlock](owner-pin-unlock.md) later in settings. No Google account or cloud project is required.

The [three-field setup preview release](https://github.com/edcadet10/sarah-beauty-desktop/releases/tag/windows-preview-2026-11-13-simple-setup) packages source `0d4ac64df8ed68c6a7781433a03f27cb1db6535d` from [GitHub build 43483947275](https://github.com/edcadet10/sarah-beauty-desktop/actions/runs/43483947275). Its ZIP has SHA-256 `a39d47922beceb32cad73d88f2a65d3bd0ca3d1a2dbda756fbc3d9c7261e29ec`; the release also includes `SHA256SUMS.txt`. All 620 files match the manifest. This release download remains separate from the expiring Actions artifact.

For a different source revision, use **Actions > Windows developer preview** and choose a successful run. GitHub sign-in is required to download those workflow artifacts, which are retained for 14 days. See [GitHub's artifact download guide](https://docs.github.com/en/actions/how-tos/manage-workflow-runs/download-workflow-artifacts).

The folder includes the Windows and .NET runtimes, the recovery helper, and third-party notices. No customer database, owner passphrase, Google credential, or preconfigured test business is supplied. The preview does not install or sign itself.

The [earlier keyboard preview](https://github.com/edcadet10/sarah-beauty-desktop/actions/runs/43444824612/artifacts/14047403190) expires November 27, 2026. It includes [password or PIN unlock](owner-pin-unlock.md), [voluntary exit review](workspace-exit-review.md), and [Enter from either credential field](keyboard-unlock.md). With its exact executable's outbound access blocked, Enter/repeat/rejection/retry and both owner headers pass with every saved table retained. PIN setup/removal, restart and 1280/500 protected-input layouts passed on the preceding `818ec4f` download; those settings and authentication implementations are unchanged. Blocked-network archive review passed on `6cd916a`; recovery sources are unchanged. No Google account or Cloud project is required.

## Build another revision

After this workflow is on the default branch, choose **Run workflow**, enter a repository branch, tag, or commit in **revision**, and start the run. The selected source must contain `tools/publish-desktop.ps1`. Building in GitHub does not run the app or create a business database.

The default is the verified three-field setup commit `0d4ac64df8ed68c6a7781433a03f27cb1db6535d`. All 1,709 local regression cases pass with the real recovery helper. Debug and self-contained publishing are clean. The GitHub-downloaded app passes fresh three-field setup, dashboard, later profile/user settings, optional PIN/password unlock, and restart with its executable's outbound access blocked; the owned firewall rule is removed afterward. See [first-run evidence](simple-first-run.md). Choose another revision explicitly when reviewing a newer build.

`source-manifest.json` records the exact checked-out source commit, target, unsigned preview status, and each published file's path, byte count, and SHA-256 hash. The artifact name uses the source commit, which can differ from the workflow branch's commit. A failed publish or empty output fails the job. Packaging rejects database, recovery archive, and key-file extensions.

## Verified downloads

The workflow was exercised through a temporary push trigger before enabling manual use; that trigger is removed from the final workflow.

| Source commit | GitHub run | Download check |
| --- | --- | --- |
| `a886ca9e1fc5fc9b18f7530a31aa16ac0dffbcc7` | [43350179467](https://github.com/edcadet10/sarah-beauty-desktop/actions/runs/43350179467) | All 574 paths, byte counts, and SHA-256 hashes match; second owner reads all six complete customer fields |
| `8dda1a7cf5e2e4c809fb9e3ce22526c1da7379d5` | [43354309923](https://github.com/edcadet10/sarah-beauty-desktop/actions/runs/43354309923) | All 574 paths, byte counts, and SHA-256 hashes match; second owner reads deliberately edited loan terms and complete unchanged evidence |
| `0a6185b069d622700cf5ff081458e3eb881704b0` | [43357524913](https://github.com/edcadet10/sarah-beauty-desktop/actions/runs/43357524913) | All 574 paths, byte counts, and SHA-256 hashes match; installed app retains the offline backup layout and verifies/decrypts its local archive under an outbound network block |
| `96da46d780ccac01f369a5786e07186251ea9844` | [43368056716](https://github.com/edcadet10/sarah-beauty-desktop/actions/runs/43368056716) | All 575 paths, byte counts, and SHA-256 hashes match; extracted executable icon matches the expected 32-pixel frame; second owner reads edited multiline terms and complete unchanged evidence |
| `2912a34be0e9889723877164aef3a9eaf70371a3` | [43371951319](https://github.com/edcadet10/sarah-beauty-desktop/actions/runs/43371951319) | All 575 paths, byte counts, and SHA-256 hashes match; second owner repeats New/reload/Keep/Discard/navigation in all five resource registers; every saved database table remains exact |
| `a1a4e2a65e1c8e86e7588cac98aea9341f8396a5` | [43375431442](https://github.com/edcadet10/sarah-beauty-desktop/actions/runs/43375431442) | All 575 paths, byte counts, and SHA-256 hashes match; nested asset New/Keep, parent-save Keep, and New/Discard pass with unchanged tables; second-owner asset reads and all five main register probes pass |

| `d1fbb426dcf96244f647ad3cc496afc762d703f8` | [43387168315](https://github.com/edcadet10/sarah-beauty-desktop/actions/runs/43387168315) | All 575 paths, byte counts and SHA-256 hashes match; resource paragraphs/edit guards and blocked-network archive review pass |
| `6cd916a4127ce17a2032e101bbcf78bf694f7c9c` | [43392094460](https://github.com/edcadet10/sarah-beauty-desktop/actions/runs/43392094460) | All 620 paths, byte counts and SHA-256 hashes match; 29 runtime packages/44 notices, trip draft guards, second-owner paragraphs and blocked-network archive review pass with retained saved tables |
| `81afd6d0f0f062cc462bc563f1ef5cf385325fd5` | [43402725739](https://github.com/edcadet10/sarah-beauty-desktop/actions/runs/43402725739) | All 620 paths, sizes and hashes match; Money Keep/Discard/reversion/filter probes, second-owner posting read and rejected-approval control restoration pass |
| `6b753ca7ed6dc8d73ae0d1436981c3f0b7471d9c` | [43408828743](https://github.com/edcadet10/sarah-beauty-desktop/actions/runs/43408828743) | All 620 paths, sizes and hashes match; 29 packages/44 notice hashes, exact matching-receipt context, second-owner reads, Money draft guards and 1280/500 row layouts pass; saved tables remain exact |
| `97dffa1c0e6f5ba76fb5340457ac896a477a1019` | [43432004423](https://github.com/edcadet10/sarah-beauty-desktop/actions/runs/43432004423) | All 620 paths/sizes/hashes match; cached customer/Money drafts survive cancelled lock/close, repeated close and another modal; explicit exits and second-owner restart retain saved tables |
| `818ec4f243ddf5966fcae8d325b30b9c3deea401` | [43438361774](https://github.com/edcadet10/sarah-beauty-desktop/actions/runs/43438361774) | All 620 paths/sizes/hashes and 44 notice hashes match; outbound-blocked PIN setup/unlock/removal and password fallback, both-owner restart, status-race recovery and 1280/500 protected-input/action layouts pass; all saved tables remain exact |
| `a6f3b9f450199a585bdca38796ea3b8660f63a7f` | [43444824612](https://github.com/edcadet10/sarah-beauty-desktop/actions/runs/43444824612) | All 620 paths/sizes/hashes match; outbound-blocked PIN/password Enter, held-key suppression, wrong-PIN retry and actual owner headers pass; all saved business tables remain exact |
| `0d4ac64df8ed68c6a7781433a03f27cb1db6535d` | [43483947275](https://github.com/edcadet10/sarah-beauty-desktop/actions/runs/43483947275) | All 620 paths/sizes/hashes match; outbound-blocked three-field setup, immediate dashboard, later details/user settings, optional PIN/password unlock and restart pass |

All listed jobs succeeded. None of the downloads contains the rejected business-data file types. These checks establish packaging and the stated native probes; they do not certify production readiness or every app workflow. The [complete feature regression](https://github.com/edcadet10/sarah-beauty-desktop/actions/runs/43389582998) passes all 1,684 cases, recovery interoperability and the Windows Debug build with zero warnings/errors.

## Observed runtime independence

The downloaded `0a6185b` app records .NET 10.0.15 as an included framework, with no shared-framework requirement in its runtime configuration. Its publish profile enables both .NET and Windows App SDK self-contained deployment and disables trimming. See [Microsoft's self-contained deployment guide](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/self-contained-deploy/deploy-self-contained-apps).

An actual startup module inventory resolves `coreclr.dll`, `Microsoft.WindowsAppRuntime.dll`, `Microsoft.UI.Xaml.dll`, `Microsoft.UI.Xaml.Controls.dll`, and `e_sqlite3.dll` inside the extracted app folder. Every observed loaded module is either app-local or under Windows; none comes from a developer SDK/shared-runtime directory. The probe closes normally and retains the fictional business. This directly checks the observed loads; it is not a substitute for a clean-machine or physical Surface check.
