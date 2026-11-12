namespace SarahBeauty.Business.Exports;

internal static class ReadableColumnJoins
{
    internal static (string Table,string Column)? Target(string table,string column)
    {
        if(column is "OwnershipVersionId" or "SupersedesOwnershipVersionId")return("Ownership confirmations","OwnershipVersionId");
        if(column.EndsWith("OwnerId",StringComparison.Ordinal)||column is "OwnerLenderId" or "CapturedOwnerLenderId")return ("Owners","OwnerId");
        if(column.EndsWith("CustomerId",StringComparison.Ordinal)||column=="PayerId")return ("Customers","CustomerId");
        if(column.EndsWith("AccountId",StringComparison.Ordinal))return ("Accounts","AccountId");
        if(column.EndsWith("BookingId",StringComparison.Ordinal))return ("Bookings","BookingId");
        if(column.EndsWith("MovementId",StringComparison.Ordinal)||column is "PaymentId" or "SourcePaymentId" or "PostedBatchId")return ("Posted movements","MovementId");
        if(column is "InvoiceVersionId" or "QuoteVersionId" or "SourceQuoteVersionId" or "AcceptedQuoteVersionId" or "CreditNoteVersionId" or "NoteVersionId" or "ReceiptVersionId" or "TermsVersionId")return ("Document versions","VersionId");
        if(column=="VersionId")return table.StartsWith("Managed evidence",StringComparison.Ordinal)?("Managed evidence version references","VersionId"):("Document versions","VersionId");
        if(column=="PreviousVersionId")return ("Managed evidence version references","VersionId");
        if(column=="CreditId")return table.StartsWith("Supplier",StringComparison.Ordinal)?("Supplier bill credits","CreditId"):("Customer credits","CreditId");
        if(column=="GroupId")return table.StartsWith("Bank match",StringComparison.Ordinal)?("Bank match approvals","GroupId"):("Application approvals","GroupId");
        if(column=="PeriodId")return table.Contains("tax filing",StringComparison.OrdinalIgnoreCase)?("Tax filing periods","PeriodId"):("Book period close history","PeriodId");
        return column switch
        {
            "BusinessId"=>("Business","BusinessId"),"ServiceId"=>("Service catalog","ServiceId"),"ChargeId"=>("Booking charges","ChargeId"),
            "PlaceId"=>("Booking places","PlaceId"),"AppointmentId"=>("Appointments","AppointmentId"),
            "ArtistId" or "DriverArtistId"=>("Artists","ArtistId"),"SupplierId"=>("Suppliers","SupplierId"),
            "LoanId" or "CapturedLoanId"=>("Loan register","LoanId"),"AssetId"=>("Asset register","AssetId"),
            "PurchaseId"=>("Supplier bills","PurchaseId"),"PurchaseLineId"=>("Supplier bill lines","LineId"),
            "ReversesCreditId"=>("Supplier bill credits","CreditId"),"MilestoneId"=>("Invoice payment milestones","MilestoneId"),
            "StatementId"=>("Bank statements","StatementId"),"BankLineId"=>("Bank transactions","BankLineId"),
            "JournalLineId"=>("Journal lines","JournalLineId"),"RetainedPeriodId"=>("Book period close history","PeriodId"),
            "ReconciliationId" or "PriorReconciliationId"=>("Reconciliation approvals","ReconciliationId"),
            "MatchId" or "ReversesMatchId"=>("Bank match portions","MatchId"),
            "ApplicationId" or "ReversesApplicationId"=>("Payment applications","ApplicationId"),
            "FundingId" or "ReversesFundingId"=>("Credit funding","FundingId"),"LinkId"=>("Managed evidence links","LinkId"),
            "SupplyId"=>("Supplies","SupplyId"),"TripId"=>("Actual mileage trips","TripId"),
            "PermissionId"=>("Customer permission status","PermissionId"),
            _=>null
        };
    }
}
