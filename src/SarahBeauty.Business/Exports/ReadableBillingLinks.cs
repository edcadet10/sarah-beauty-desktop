namespace SarahBeauty.Business.Exports;

internal static class ReadableBillingLinks
{
    internal static ReadableExportTable[] Build(ReadableExportSnapshot snapshot,ReadableExportBudget budget)=>snapshot.Read<ReadableExportTable[]>(db=>
    [
        ReadableSqlTables.Read(db,snapshot.Source,budget,"Invoice payers",["InvoiceVersionId","BusinessId","CustomerId","SourceQuoteVersionId"],"""
            SELECT H.VersionId,D.BusinessId,H.CustomerId,H.SourceQuoteVersionId FROM InvoiceHeaders H
            JOIN DocumentVersions V ON V.Id=H.VersionId JOIN Documents D ON D.Id=V.DocumentId WHERE D.BusinessId=$business ORDER BY H.VersionId;
            """),
        ReadableSqlTables.Read(db,snapshot.Source,budget,"Invoice billed portions",["PortionId","BusinessId","InvoiceVersionId","BookingId","ChargeId","Quantity","NetCents","TaxCents","DiscountCents","SourceSignature"],"""
            SELECT C.Id,D.BusinessId,C.InvoiceVersionId,C.BookingId,C.ChargeId,C.Quantity,C.NetCents,C.TaxCents,C.DiscountCents,C.SourceSignature FROM InvoiceCoverage C
            JOIN DocumentVersions V ON V.Id=C.InvoiceVersionId JOIN Documents D ON D.Id=V.DocumentId WHERE D.BusinessId=$business ORDER BY C.Id;
            """),
        ReadableSqlTables.Read(db,snapshot.Source,budget,"Invoice payment milestones",["MilestoneId","BusinessId","InvoiceVersionId","Kind","Cents","DueDate"],"""
            SELECT M.Id,D.BusinessId,M.InvoiceVersionId,M.Kind,M.Cents,M.DueDate FROM InvoiceMilestones M
            JOIN DocumentVersions V ON V.Id=M.InvoiceVersionId JOIN Documents D ON D.Id=V.DocumentId WHERE D.BusinessId=$business ORDER BY M.Id;
            """),
        ReadableSqlTables.Read(db,snapshot.Source,budget,"Credit note invoice links",["NoteVersionId","BusinessId","InvoiceVersionId","Date"],"""
            SELECT H.VersionId,D.BusinessId,H.InvoiceVersionId,H.Date FROM CreditNoteHeaders H
            JOIN DocumentVersions V ON V.Id=H.VersionId JOIN Documents D ON D.Id=V.DocumentId WHERE D.BusinessId=$business ORDER BY H.VersionId;
            """),
        ReadableSqlTables.Read(db,snapshot.Source,budget,"Credit note portions",["NoteVersionId","BusinessId","ChargeId","Quantity","NetCents","TaxCents","DiscountCents"],"""
            SELECT L.NoteVersionId,D.BusinessId,L.ChargeId,L.Quantity,L.NetCents,L.TaxCents,L.DiscountCents FROM CreditNoteLines L
            JOIN DocumentVersions V ON V.Id=L.NoteVersionId JOIN Documents D ON D.Id=V.DocumentId WHERE D.BusinessId=$business ORDER BY L.NoteVersionId,L.ChargeId;
            """),
        ReadableSqlTables.Read(db,snapshot.Source,budget,"Credit note milestone reductions",["NoteVersionId","BusinessId","MilestoneId","Cents","TaxCents"],"""
            SELECT M.NoteVersionId,D.BusinessId,M.MilestoneId,M.Cents,M.TaxCents FROM CreditNoteMilestones M
            JOIN DocumentVersions V ON V.Id=M.NoteVersionId JOIN Documents D ON D.Id=V.DocumentId WHERE D.BusinessId=$business ORDER BY M.NoteVersionId,M.MilestoneId;
            """),
        ReadableSqlTables.Read(db,snapshot.Source,budget,"Receipt payment links",["ReceiptVersionId","BusinessId","PaymentMovementId"],"""
            SELECT H.VersionId,D.BusinessId,H.PaymentId FROM ReceiptHeaders H
            JOIN DocumentVersions V ON V.Id=H.VersionId JOIN Documents D ON D.Id=V.DocumentId WHERE D.BusinessId=$business ORDER BY H.VersionId;
            """)
    ]);
}
