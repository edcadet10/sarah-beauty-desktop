namespace SarahBeauty.Business.Documents;

internal static class InvoiceSchema
{
    internal const string Sql="""
        CREATE TABLE BillingState (
            BookingId TEXT PRIMARY KEY NOT NULL REFERENCES Records(Id), Generation INTEGER NOT NULL DEFAULT 0
        );
        CREATE TABLE InvoiceHeaders (
            VersionId TEXT PRIMARY KEY NOT NULL REFERENCES DocumentVersions(Id),
            CustomerId TEXT NOT NULL REFERENCES Customers(Id), SourceQuoteVersionId TEXT NOT NULL REFERENCES DocumentVersions(Id)
        );
        CREATE TABLE InvoiceCoverage (
            Id TEXT PRIMARY KEY NOT NULL, InvoiceVersionId TEXT NOT NULL REFERENCES InvoiceHeaders(VersionId),
            BookingId TEXT NOT NULL REFERENCES Records(Id), ChargeId TEXT NOT NULL, Quantity TEXT NOT NULL,
            NetCents INTEGER NOT NULL CHECK(NetCents>=0), TaxCents INTEGER NOT NULL CHECK(TaxCents>=0),
            DiscountCents INTEGER NOT NULL CHECK(DiscountCents>=0), SourceSignature TEXT NOT NULL,
            UNIQUE(InvoiceVersionId,ChargeId)
        );
        CREATE INDEX InvoiceCoverage_Source ON InvoiceCoverage(BookingId,ChargeId);
        CREATE TABLE InvoiceMilestones (
            Id TEXT PRIMARY KEY NOT NULL, InvoiceVersionId TEXT NOT NULL REFERENCES InvoiceHeaders(VersionId),
            Kind TEXT NOT NULL, Cents INTEGER NOT NULL CHECK(Cents>0), DueDate TEXT,
            UNIQUE(InvoiceVersionId,Kind)
        );
        """;
}
