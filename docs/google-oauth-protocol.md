# Authorize a Google desktop connection

Status: The internal consent protocol is implemented and tested with fictional responses. Owner-authorized configuration/connection storage, desktop controls, real Google consent, and delivery follow separately. This change does not create a real connection.

The protocol pins Google.Apis.Auth 1.78.0 and uses its PKCE authorization-code flow with S256. It requests only `drive.file`, offline access, explicit consent/account selection, and no incremental inclusion of other grants. Google's [Sheets create API](https://developers.google.com/workspace/sheets/api/reference/rest/v4/spreadsheets/create) supports that same per-file scope.

The app adds a fresh 256-bit callback state, exact loopback redirect binding, one-use receiver, bounded authorization-code length, and constant-time state comparison. The SDK listens at a random `127.0.0.1` port. System-browser launch accepts the official HTTPS Google account host and uses Windows URL handling without a command shell. The static callback page contains no returned code or state.

The consent attempt has a five-minute deadline and explicit cancellation. The protocol uses no disk token store. It requires complete offline refresh material, a bearer access token, bounded values/expiry, and the requested file scope. Additional returned scopes are rejected. Errors expose fixed codes/messages without private provider content or inner exceptions. The app leaves the Google library's default NullLogger in place and does not configure SDK debug logs.

Eleven protocol cases, plus eleven client-parser cases, pass. Tests independently hash the verifier to compare the transmitted S256 challenge, inspect the narrow offline request, reject incorrect state before exchange, sanitize denial/client/grant/malformed errors, reject broader scopes and missing refresh material, and distinguish deadline from cancellation. An actual SDK loopback test receives a fictional local callback without opening Google or using real credentials. This is not a real consent/account or second-device acceptance test.

The pinned library's Apache license, attribution, and restored transitive license/notices copy to build/publish output. The copied Google package license is identical for Google.Apis, Google.Apis.Auth, and Google.Apis.Core 1.78.0.

References: Google's [desktop OAuth guide](https://developers.google.com/identity/protocols/oauth2/native-app), [per-file Drive scope guide](https://developers.google.com/workspace/drive/api/guides/api-specific-auth), and [maintained .NET client source](https://github.com/googleapis/google-api-dotnet-client). No connected-app credentials are used.
