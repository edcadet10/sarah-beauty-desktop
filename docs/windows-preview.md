# Download a Windows developer preview

The developer preview is a self-contained Windows x64 build for review. It is unsigned. Physical device, real Google authorization/delivery, signing, and complete release acceptance remain open.

## Download and open the app

1. Sign in to GitHub with read access to this repository. Open **Actions > Windows developer preview**.
2. Choose a successful run. Download its `SarahBeauty-Windows-preview-<source revision>` artifact before it expires. This workflow retains artifacts for 14 days. See [GitHub's artifact download guide](https://docs.github.com/en/actions/how-tos/manage-workflow-runs/download-workflow-artifacts).
3. Extract the entire archive. Keep its contents together. Open `SarahBeautyDesktop/SarahBeauty.Desktop.exe`.

The folder includes the Windows and .NET runtimes, the recovery helper, and third-party notices. No customer database, owner passphrase, Google credential, or preconfigured test business is supplied. The preview does not install or sign itself.

## Build another revision

After this workflow is on the default branch, choose **Run workflow**, enter a repository branch, tag, or commit in **revision**, and start the run. The selected source must contain `tools/publish-desktop.ps1`. Building in GitHub does not run the app or create a business database.

`source-manifest.json` records the exact checked-out source commit, target, unsigned preview status, and each published file's path, byte count, and SHA-256 hash. The artifact name uses the source commit, which can differ from the workflow branch's commit. A failed publish or empty output fails the job. Packaging rejects database, recovery archive, and key-file extensions.

## Verified downloads

The workflow was exercised through a temporary push trigger before enabling manual use; that trigger is removed from the final workflow.

| Source commit | GitHub run | Download check |
| --- | --- | --- |
| `a886ca9e1fc5fc9b18f7530a31aa16ac0dffbcc7` | [43350179467](https://github.com/edcadet10/sarah-beauty-desktop/actions/runs/43350179467) | All 574 paths, byte counts, and SHA-256 hashes match; second owner reads all six complete customer fields |
| `8dda1a7cf5e2e4c809fb9e3ce22526c1da7379d5` | [43354309923](https://github.com/edcadet10/sarah-beauty-desktop/actions/runs/43354309923) | All 574 paths, byte counts, and SHA-256 hashes match; second owner reads deliberately edited loan terms and complete unchanged evidence |

Both jobs succeeded. Neither download contains the rejected business-data file types. These checks establish packaging and the stated native probes; they do not certify production readiness or every app workflow.
