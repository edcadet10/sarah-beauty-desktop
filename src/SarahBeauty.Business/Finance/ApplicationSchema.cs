namespace SarahBeauty.Business.Finance;

internal static class ApplicationSchema
{
    internal const string Sql="""
        CREATE TABLE ValueState (
            BusinessId TEXT NOT NULL REFERENCES Business(Id), Kind TEXT NOT NULL, SourceId TEXT NOT NULL,
            Generation INTEGER NOT NULL DEFAULT 0, PRIMARY KEY(BusinessId,Kind,SourceId)
        );
        CREATE TABLE CustomerCredits (
            Id TEXT PRIMARY KEY NOT NULL, BusinessId TEXT NOT NULL REFERENCES Business(Id),
            CustomerId TEXT NOT NULL REFERENCES Customers(Id), PaymentId TEXT NOT NULL REFERENCES Movements(Id),
            IssueDate TEXT NOT NULL, IssuedCents INTEGER NOT NULL CHECK(IssuedCents>0),
            IssuedTaxCents INTEGER NOT NULL CHECK(IssuedTaxCents>=0 AND IssuedTaxCents<=IssuedCents),
            Reason TEXT NOT NULL, OwnerId TEXT NOT NULL REFERENCES Owners(Id), AtUtc TEXT NOT NULL
        );
        CREATE TABLE CreditFunding (
            Id TEXT PRIMARY KEY NOT NULL, CreditId TEXT NOT NULL REFERENCES CustomerCredits(Id),
            PaymentId TEXT NOT NULL REFERENCES Movements(Id), Kind TEXT NOT NULL CHECK(Kind IN ('Reserve','Release')),
            Date TEXT NOT NULL, Cents INTEGER NOT NULL CHECK(Cents>0), TaxCents INTEGER NOT NULL CHECK(TaxCents>=0 AND TaxCents<=Cents),
            Reason TEXT NOT NULL, OwnerId TEXT NOT NULL REFERENCES Owners(Id),
            RefundMovementId TEXT REFERENCES Movements(Id), ReversesFundingId TEXT REFERENCES CreditFunding(Id)
        );
        CREATE TABLE ApplicationGroups (
            Id TEXT PRIMARY KEY NOT NULL, BusinessId TEXT NOT NULL REFERENCES Business(Id),
            DraftId TEXT NOT NULL UNIQUE REFERENCES Records(Id), SourceRevision INTEGER NOT NULL,
            PayloadHash TEXT NOT NULL, ApprovedBy TEXT NOT NULL REFERENCES Owners(Id), AtUtc TEXT NOT NULL
        );
        CREATE TABLE PaymentApplications (
            Id TEXT PRIMARY KEY NOT NULL, GroupId TEXT NOT NULL REFERENCES ApplicationGroups(Id),
            PaymentId TEXT REFERENCES Movements(Id), CreditId TEXT REFERENCES CustomerCredits(Id),
            InvoiceVersionId TEXT NOT NULL REFERENCES InvoiceHeaders(VersionId), MilestoneId TEXT NOT NULL REFERENCES InvoiceMilestones(Id),
            Cents INTEGER NOT NULL CHECK(Cents>0), TaxCents INTEGER NOT NULL CHECK(TaxCents>=0 AND TaxCents<=Cents),
            Date TEXT NOT NULL, ReversesId TEXT REFERENCES PaymentApplications(Id),
            CHECK((PaymentId IS NOT NULL AND CreditId IS NULL) OR (PaymentId IS NULL AND CreditId IS NOT NULL))
        );
        CREATE INDEX PaymentApplications_Payment ON PaymentApplications(PaymentId,Date);
        CREATE INDEX PaymentApplications_Target ON PaymentApplications(InvoiceVersionId,MilestoneId,Date);
        CREATE TRIGGER PaymentApplications_Immutable BEFORE UPDATE ON PaymentApplications
        BEGIN SELECT RAISE(ABORT,'Applications require a dated reversal.'); END;
        CREATE TRIGGER PaymentApplications_Retain BEFORE DELETE ON PaymentApplications
        BEGIN SELECT RAISE(ABORT,'Applications cannot be deleted.'); END;
        """;
}
