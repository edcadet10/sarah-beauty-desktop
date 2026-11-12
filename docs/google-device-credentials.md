# Protect Google device credentials

Status: The internal Windows credential-file component is implemented. Google OAuth, connection controls, uploads, and readable exports follow separately. No actual Google authorization was performed in this change.

Credentials use Microsoft's Windows DPAPI through pinned `System.Security.Cryptography.ProtectedData` 10.0.15 with CurrentUser protection. The additional context binds version, logical store, business, writable instance, TEST/LIVE mode, and client/token purpose. The file contains protected bytes. Secret payloads do not enter SQLite, business directories, archive manifests, logs, or repository examples.

The default location is private local application data under `SarahBeautyDesktop/DeviceCredentials`, with a hashed context directory. The component rejects business-directory overlap, unknown purposes, reparse points at managed paths, oversized files/payloads, stale instances, and changed business/mode contexts. It is an internal storage primitive; owner-authorized Google connection services follow separately. It does not renew human sessions.

Writes protect before writing, flush a new file, recheck the active context under a short writer reservation, and atomically replace the earlier protected file under an exclusive filesystem gate. An interrupted replacement preserves the earlier value and removes only its own temporary file. Deleting a token does not delete client settings. Damaged/copied context files remain available for review and return a fixed reconnect message without an inner cryptographic error.

Protected credential files are deliberately absent from complete business recovery archives. After recovery, owners reconnect Google in the selected new instance. DPAPI protects against reading these files without the applicable Windows profile; it does not prevent other authorized processes in that profile from accessing them. A separate Windows-profile or second-device check remains acceptance work.

Seven fictional cases verify actual protected round trips/reopening, ciphertext without plaintext markers, exclusion from an actual captured database/manifest, business/instance/purpose mismatch, changed ciphertext, interrupted replacement, changed identity after flushing, payload/path bounds, deletion, and twelve concurrent replacements/readers. NuGet notices and the .NET runtime license copy to build/publish output.

References: Microsoft's [ProtectedData API](https://learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.protecteddata) and Google's [desktop OAuth guide](https://developers.google.com/identity/protocols/oauth2/native-app). This component does not use connected-app tokens for the desktop application.
