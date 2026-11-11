using System.Text.Json;
using SarahBeauty.Business.Banking;
using SarahBeauty.Business.Purchasing;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Finance;

public sealed class FinanceBook(BusinessStore store)
{
    private readonly RecordStore<FinancialDraft> _drafts = new(store, "FinancialDraft");
    internal Action<string>? FailurePoint { get; set; }
    public List<FinancialDraft> Drafts(OwnerSession owner) => _drafts.List(owner);

    public List<JournalLine> Journal(OwnerSession owner, string movementId) => store.Read(db =>
    {
        store.RequireSession(db, owner);
        return db.Read("""
            SELECT J.AccountId,J.DebitCents,J.CreditCents,COALESCE(J.AssetId,'') FROM JournalLines J JOIN Movements M ON M.Id=J.MovementId
            WHERE M.Id=$id AND M.BusinessId=$business;
            """, r => new JournalLine(r.GetString(0),r.GetInt64(1),r.GetInt64(2),r.GetString(3)), ("$id",movementId),("$business",owner.BusinessId));
    });

    public List<JournalLine> Preview(OwnerSession owner, FinancialDraft draft) => store.Read(db =>
    {
        store.RequireSession(db, owner);
        var accounts = ChartOfAccounts.List(db, owner);
        draft = CorrectionRules.Prepare(db, owner, draft);
        draft=PurchasePayments.Prepare(db,store,owner,draft);
        draft=ReceiptClassifications.Prepare(db,owner,draft);
        PostingValidation.Validate(db, owner, store, draft, accounts);
        OpeningRules.Validate(db,store,owner,draft,accounts);
        CreditRefundRules.Validate(db,owner,draft);
        var lines=BuildLines(db,owner,draft,accounts);
        ReconciliationLocks.Posting(db,owner,draft.Date!.Value,lines,accounts);
        return lines;
    });

    public FinancialDraft SaveDraft(OwnerSession owner, FinancialDraft value)
    {
        value = value with { Id = value.Id.Length == 0 ? Guid.NewGuid().ToString() : value.Id, Name = value.Name.Trim() };
        return store.Write(owner, "Save financial draft", value.Id, db =>
        {
            if (value.Status != "Draft" || value.PostedBatchId.Length != 0 || (value.Revision > 0
                && _drafts.Get(db, owner, value.Id).Status != "Draft"))
                throw new ArgumentException("Posted financial records cannot be edited. Prepare a linked correction.");
            if(value.Kind==FinancialKind.AssetDisposal)value=value with{AssetGeneration=ValueState.Generation(db,owner,"Asset",value.AssetId)};
            if(value.Kind is FinancialKind.PurchaseRefund or FinancialKind.SupplierCreditUse)value=PurchasePayments.Prepare(db,store,owner,value);
            if(value.Kind is FinancialKind.PurchasePayment or FinancialKind.PurchaseRefund or FinancialKind.SupplierCreditUse)
                value=value with{PurchaseGeneration=ValueState.Generation(db,owner,"Purchase",value.PurchaseId)};
            if(value.Kind==FinancialKind.SupplierCreditUse)
            {
                var source=CorrectionRules.Original(db,owner,value.OriginalId);
                value=value with{SourcePurchaseGeneration=ValueState.Generation(db,owner,"Purchase",source.PurchaseId)};
            }
            value=value with{AssetAdjustments=value.AssetAdjustments.Select(c=>c with{Generation=ValueState.Generation(db,owner,"Asset",c.AssetId)}).ToArray()};
            if(value.Kind==FinancialKind.TaxRecovery)value=value with{TaxRecoveryGeneration=ValueState.Generation(db,owner,"TaxRecovery",value.OriginalId)};
            if(value.Kind==FinancialKind.ReceiptClassification)value=ReceiptClassifications.Review(db,owner,value);
            return _drafts.Save(db, owner, value);
        });
    }

    public PostedMovement Approve(OwnerSession owner, string draftId, int expectedRevision, string phrase)
    {
        new OwnerAccess(store).Confirm(owner, phrase);
        return store.Transaction(db =>
        {
            store.RequireSession(db, owner);
            var existing = FindPosted(db, owner, draftId);
            if (existing is not null) return existing;
            var draft = _drafts.Get(db, owner, draftId);
            if (draft.Revision != expectedRevision || draft.Status != "Draft")
                throw new InvalidOperationException("The draft changed. Reload it before approving.");
            return Post(db, owner, draft);
        });
    }

    private PostedMovement Post(DbWork db, OwnerSession owner, FinancialDraft draft)
    {
        var accounts = ChartOfAccounts.List(db, owner);
        draft = CorrectionRules.Prepare(db, owner, draft);
        draft=PurchasePayments.Prepare(db,store,owner,draft);
        draft=ReceiptClassifications.Prepare(db,owner,draft);
        PostingValidation.Validate(db, owner, store, draft, accounts);
        OpeningRules.Validate(db,store,owner,draft,accounts);
        CreditRefundRules.Validate(db,owner,draft);
        CheckReference(db, owner, draft);
        var lines=BuildLines(db,owner,draft,accounts);
        PostingRules.ValidateBalance(lines,PurchasePayments.AllowsEmpty(db,owner,draft) || AssetDisposals.AllowsEmpty(db,owner,draft) || ReceiptClassifications.AllowsEmpty(db,owner,draft));
        ReconciliationLocks.Posting(db,owner,draft.Date!.Value,lines,accounts);
        var posted = PostingWriter.Save(db, owner, draft, lines, FailurePoint);
        OpeningRules.AfterPosting(db,owner,draft,posted.Id,accounts);
        AssetDisposals.AfterPosting(db,store,owner,draft,posted.Id);
        AssetRetirements.AfterPosting(db,store,owner,draft,posted.Id);
        if(draft.AssetAdjustments.Any(c=>c.Treatment==AssetValueChoice.Retire))FailurePoint?.Invoke("asset-refund-retirement");
        PurchasePayments.AfterPosting(db,store,owner,draft,posted.Id);
        if(draft.Kind is FinancialKind.PurchasePayment or FinancialKind.PurchaseRefund or FinancialKind.SupplierCreditUse)FailurePoint?.Invoke("purchase-settlement");
        var retired=draft.AssetAdjustments.Where(c=>c.Treatment==AssetValueChoice.Retire).Select(c=>c.AssetId);
        JournalAssets.AfterPosting(db,owner,lines,retired.Concat(draft.AssetId.Length>0?[draft.AssetId]:[]));
        if(draft.Kind==FinancialKind.AssetDisposal)FailurePoint?.Invoke("asset-disposal-record");
        FailurePoint?.Invoke("opening-record");
        if(CreditRefundRules.AfterPosting(db,owner,draft,posted.Id))FailurePoint?.Invoke("credit-refund-funding");
        ReceiptValueGuard.AfterPosting(db,owner,draft,posted.Id);
        ReceiptClassifications.AfterPosting(db,owner,draft);
        if(draft.Kind==FinancialKind.ReceiptClassification)FailurePoint?.Invoke("receipt-classification");
        TaxRecoveries.AfterPosting(db,owner,draft,posted.Id);
        if(draft.Kind==FinancialKind.TaxRecovery || draft.TaxRefundAllocation is not null)FailurePoint?.Invoke("tax-recovery");
        BalanceGuards.Validate(db, owner, draft);
        _drafts.Save(db, owner, draft with { Status = "Posted", PostedBatchId = posted.Id });
        BusinessStore.Audit(db, owner, "Approve financial draft", draft.Id);
        FailurePoint?.Invoke("audit");
        return posted;
    }

    private List<JournalLine> BuildLines(DbWork db,OwnerSession owner,FinancialDraft draft,List<Account> accounts)
    {
        AssetRefunds.ValidateContext(draft);
        var lines=draft.Kind switch
        {
            FinancialKind.SupplierRefund=>AssetRefunds.ClassicLines(db,owner,draft,accounts),
            FinancialKind.Refund=>TaxRefunds.Lines(draft,accounts),
            FinancialKind.TaxRecovery=>TaxRecoveries.Lines(db,owner,draft,accounts),
            FinancialKind.ReceiptClassification=>ReceiptClassifications.Lines(db,owner,draft,accounts),
            FinancialKind.PurchasePayment=>PurchasePayments.Lines(db,store,owner,draft,accounts),
            FinancialKind.PurchaseRefund=>PurchaseRefundPlan.Lines(db,store,owner,draft,accounts),
            FinancialKind.SupplierCreditUse=>SupplierCreditUsePlan.Lines(db,store,owner,draft,accounts),
            FinancialKind.Reversal or FinancialKind.AssetDisposal=>CorrectionRules.Lines(db,owner,draft,accounts),
            _=>PostingRules.Build(draft,accounts)
        };
        return JournalAssets.Assign(db,store,owner,draft,lines,accounts);
    }

    public List<PostedMovement> Posted(OwnerSession owner) => store.Read(db =>
    {
        store.RequireSession(db, owner);
        return db.Read("""
            SELECT M.Id,M.DataJson,B.ApprovedBy,B.AtUtc FROM Movements M JOIN PostingBatches B ON B.Id=M.Id
            WHERE M.BusinessId=$business ORDER BY M.Date,M.Id;
            """, r => new PostedMovement(r.GetString(0), JsonSerializer.Deserialize<FinancialDraft>(r.GetString(1))!,
                r.GetString(2), r.GetString(3)), ("$business", owner.BusinessId));
    });

    private static PostedMovement? FindPosted(DbWork db, OwnerSession owner, string draftId) => db.Read("""
        SELECT M.Id,M.DataJson,B.ApprovedBy,B.AtUtc FROM PostingBatches B JOIN Movements M ON M.Id=B.Id
        WHERE B.BusinessId=$business AND B.DraftId=$draft;
        """, r => new PostedMovement(r.GetString(0), JsonSerializer.Deserialize<FinancialDraft>(r.GetString(1))!, r.GetString(2), r.GetString(3)),
        ("$business", owner.BusinessId), ("$draft", draftId)).SingleOrDefault();

    private static void CheckReference(DbWork db, OwnerSession owner, FinancialDraft draft)
    {
        if (!draft.ReliableReference) return;
        if (string.IsNullOrWhiteSpace(draft.ExternalReference) || string.IsNullOrWhiteSpace(draft.Provider))
            throw new ArgumentException("A reliable reference needs its provider and reference value.");
        if (db.Scalar("SELECT Id FROM Movements WHERE BusinessId=$business AND ReferenceNamespace=$namespace AND ExternalReference=$reference;",
            ("$business", owner.BusinessId), ("$namespace", draft.CashAccountId + ":" + draft.Provider.Trim().ToUpperInvariant()),
            ("$reference", draft.ExternalReference.Trim())) is not null)
            throw new ArgumentException("This reliable payment reference was already posted. Review the existing movement.");
    }
}
