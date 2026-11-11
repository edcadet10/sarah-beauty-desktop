using System.Text.Json;
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
            SELECT J.AccountId,J.DebitCents,J.CreditCents FROM JournalLines J JOIN Movements M ON M.Id=J.MovementId
            WHERE M.Id=$id AND M.BusinessId=$business;
            """, r => new JournalLine(r.GetString(0),r.GetInt64(1),r.GetInt64(2)), ("$id",movementId),("$business",owner.BusinessId));
    });

    public List<JournalLine> Preview(OwnerSession owner, FinancialDraft draft) => store.Read(db =>
    {
        store.RequireSession(db, owner);
        var accounts = ChartOfAccounts.List(db, owner);
        draft = CorrectionRules.Prepare(db, owner, draft);
        PostingValidation.Validate(db, owner, store, draft, accounts);
        return draft.Kind is FinancialKind.Reversal or FinancialKind.AssetDisposal
            ? CorrectionRules.Lines(db, owner, draft, accounts) : PostingRules.Build(draft, accounts);
    });

    public FinancialDraft SaveDraft(OwnerSession owner, FinancialDraft value)
    {
        value = value with { Id = value.Id.Length == 0 ? Guid.NewGuid().ToString() : value.Id, Name = value.Name.Trim() };
        return store.Write(owner, "Save financial draft", value.Id, db =>
        {
            if (value.Status != "Draft" || value.PostedBatchId.Length != 0 || (value.Revision > 0
                && _drafts.Get(db, owner, value.Id).Status != "Draft"))
                throw new ArgumentException("Posted financial records cannot be edited. Prepare a linked correction.");
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
        PostingValidation.Validate(db, owner, store, draft, accounts);
        CheckReference(db, owner, draft);
        var lines = draft.Kind is FinancialKind.Reversal or FinancialKind.AssetDisposal
            ? CorrectionRules.Lines(db, owner, draft, accounts) : PostingRules.Build(draft, accounts);
        PostingRules.ValidateBalance(lines);
        var posted = PostingWriter.Save(db, owner, draft, lines, FailurePoint);
        BalanceGuards.Validate(db, owner, draft);
        _drafts.Save(db, owner, draft with { Status = "Posted", PostedBatchId = posted.Id });
        BusinessStore.Audit(db, owner, "Approve financial draft", draft.Id);
        FailurePoint?.Invoke("audit");
        return posted;
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
