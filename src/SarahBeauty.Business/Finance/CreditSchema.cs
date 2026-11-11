namespace SarahBeauty.Business.Finance;

internal static class CreditSchema
{
    internal const string Sql="""
        ALTER TABLE CreditFunding ADD COLUMN SourceDraftId TEXT REFERENCES Records(Id);
        CREATE UNIQUE INDEX CreditFunding_Draft ON CreditFunding(SourceDraftId) WHERE SourceDraftId IS NOT NULL;
        CREATE TRIGGER CustomerCredits_Immutable BEFORE UPDATE ON CustomerCredits
        BEGIN SELECT RAISE(ABORT,'Issued credit contents are immutable.'); END;
        CREATE TRIGGER CustomerCredits_Retain BEFORE DELETE ON CustomerCredits
        BEGIN SELECT RAISE(ABORT,'Credits cannot be deleted.'); END;
        CREATE TRIGGER CreditFunding_Immutable BEFORE UPDATE ON CreditFunding
        BEGIN SELECT RAISE(ABORT,'Funding changes require linked movements.'); END;
        CREATE TRIGGER CreditFunding_Retain BEFORE DELETE ON CreditFunding
        BEGIN SELECT RAISE(ABORT,'Funding history cannot be deleted.'); END;
        """;
}
