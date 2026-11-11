# Validate the app's Google desktop client

Status: The bounded desktop-client JSON parser is implemented. Import controls, saved configuration, OAuth, and delivery follow separately. No actual Google client was imported or authorized in this change.

The parser accepts downloaded `installed` configuration for a Google Desktop OAuth client. It requires client identity, secret, project, and supported official Google authorization/token endpoints. It rejects Web/service-account configurations, duplicate fields, incomplete values, untrusted endpoints, invalid UTF-8/JSON, and files larger than 64 KiB. Other downloaded fields are not used to choose network endpoints.

Parsing has no file, database, or network effects. The internal client object's display is redacted. Failures use one fixed message with no underlying private input or nested parser errors. Configuration will use [protected device files](google-device-credentials.md); connected-app credentials are not a source for the desktop app.

Desktop client secrets do not establish a confidential application. The connection flow still requires PKCE, callback state checking, and explicit consent in the system browser. Those are separate implementation and verification steps.

Eleven fictional cases cover accepted desktop configurations, redacted display, wrong credential types, changed endpoints, duplicates/missing fields, malformed/invalid UTF-8/oversized input, invalid identifiers, and unsupported whitespace. See Google's [desktop OAuth guide](https://developers.google.com/identity/protocols/oauth2/native-app).
