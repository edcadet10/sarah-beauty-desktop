# Reject malformed Unicode before readable serialization

The readable text budget now counts bytes with strict UTF-8 encoding. Invalid UTF-16 surrogate sequences raise a fixed error before a table can be frozen or published. Valid text is not normalized or replaced. All existing cell/count/byte limits remain in effect.

Two failing-before cases showed that lone high and low surrogates entered the builder without error. A temporary experiment also confirmed that the runtime's JSON round-trip changed those strings. The committed regression cases require rejection at the readable boundary. A third case round-trips valid emoji, composed and decomposed accents, and an actual replacement-character value through the staged table codec without changing the text or digest.

All three new cases and 45 related file/Sheets cases pass in Release (48 targeted). The change uses the explicit error-detection behavior documented by [UTF8Encoding](https://learn.microsoft.com/en-us/dotnet/api/system.text.utf8encoding.-ctor?view=net-10.0).

This guards readable export construction. It does not rewrite existing records or claim that every business entry/import boundary has been changed. Existing text that was already replaced before capture cannot be reconstructed by this fix. No HTTP request occurred.
