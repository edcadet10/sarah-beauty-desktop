namespace SarahBeauty.Business.Banking;

internal static class StatementEvidenceSchema
{
    internal const string Sql="ALTER TABLE StatementImports ADD COLUMN EvidenceId TEXT NOT NULL DEFAULT '';";
}
