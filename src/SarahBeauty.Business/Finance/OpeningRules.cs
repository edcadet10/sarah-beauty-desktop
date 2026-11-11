using SarahBeauty.Business.Resources;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Finance;

internal static class OpeningRules
{
    internal static (string Kind,string Id) Dimension(FinancialDraft draft,Account account)=>account.Code switch
    {"2300"=>("Loan",draft.LoanId),"1500" or "1590"=>("Asset",draft.AssetId),"2100" or "3000" or "3100"=>("Owner",draft.EconomicOwnerId),_=>("Account","")};

    internal static void Validate(DbWork db,BusinessStore store,OwnerSession owner,FinancialDraft draft,List<Account> accounts)
    {
        if(draft.Kind!=FinancialKind.Opening)return;
        var account=accounts.Single(a=>a.Id==draft.CounterAccountId);
        if(account.Type is "Income" or "Expense" || account.Code is "3200" or "2200" or "2400" || string.IsNullOrWhiteSpace(draft.EvidenceId))
            throw new ArgumentException("Choose an evidenced balance-sheet opening. Customer deposits and unclassified value need their source workflow.");
        var key=Dimension(draft,account);
        if(key.Kind!="Account" && key.Id.Length==0)throw new ArgumentException("Link the individual loan, asset, or owner for this opening.");
        if((draft.LoanId.Length>0 && key.Kind!="Loan") || (draft.AssetId.Length>0 && key.Kind!="Asset")
            || (draft.EconomicOwnerId.Length>0 && key.Kind!="Owner") || draft.CustomerId.Length>0)
            throw new ArgumentException("Keep only the subledger identity belonging to this opening account.");
        if(key.Kind=="Loan")ValidateLoan(db,store,owner,draft);
        if(key.Kind=="Asset")
        {
            var asset=new RecordStore<Asset>(store,"Asset").Get(db,owner,key.Id);
            if(asset.Archived || asset.DisposedDate.HasValue || asset.BusinessUseFraction is null)
                throw new ArgumentException("Confirm asset business use before approving its opening book value.");
        }
        if(key.Kind=="Owner" && db.Scalar("SELECT Id FROM Owners WHERE Id=$id AND BusinessId=$business AND Active=1;",("$id",key.Id),("$business",owner.BusinessId)) is null)
            throw new ArgumentException("Choose the owner involved in the opening.");
        ValidateTarget(db,owner,account.Id,key.Kind,key.Id,draft.Date!.Value);
    }

    private static void ValidateLoan(DbWork db,BusinessStore store,OwnerSession owner,FinancialDraft draft)
    {
        var loan=new RecordStore<Loan>(store,"Loan").Get(db,owner,draft.LoanId);
        if(loan.Archived || loan.OpeningPrincipalCents!=draft.AmountCents || loan.OpeningDate!=draft.Date || !draft.OpeningCredit)
            throw new ArgumentException("The approved loan opening must match its dated principal and use a liability credit.");
    }

    internal static void ValidateTarget(DbWork db,OwnerSession owner,string accountId,string kind,string dimension,DateOnly date)
    {
        if(db.Scalar("SELECT Id FROM OpeningBalances WHERE BusinessId=$business AND AccountId=$account AND DimensionKind=$kind AND DimensionId=$dimension AND RevokedAt IS NULL;",
            ("$business",owner.BusinessId),("$account",accountId),("$kind",kind),("$dimension",dimension)) is not null)
            throw new ArgumentException("This balance already has an opening. Reverse or revoke its original decision before replacing it.");
        var dates=db.Read("""
            SELECT M.Date,M.LoanId,J.AssetId,M.OwnerId FROM Movements M JOIN JournalLines J ON J.MovementId=M.Id
            WHERE M.BusinessId=$business AND J.AccountId=$account AND M.Kind NOT IN('Opening','Reversal');
            """,r=>(Date:DateOnly.Parse(r.GetString(0)),Loan:r.IsDBNull(1)?"":r.GetString(1),Asset:r.IsDBNull(2)?"":r.GetString(2),Owner:r.IsDBNull(3)?"":r.GetString(3)),
            ("$business",owner.BusinessId),("$account",accountId));
        if(dates.Any(m=>m.Date<date && (kind=="Account" || (kind switch{"Loan"=>m.Loan,"Asset"=>m.Asset,_=>m.Owner})==dimension)))
            throw new ArgumentException("The opening date must be on or before this balance's first ordinary movement.");
    }

    internal static void AfterPosting(DbWork db,OwnerSession owner,FinancialDraft draft,string movementId,List<Account> accounts)
    {
        if(draft.Kind==FinancialKind.Opening)
        {
            var account=accounts.Single(a=>a.Id==draft.CounterAccountId);
            var key=Dimension(draft,account);
            OpeningBalances.Insert(db,owner,account.Id,key.Kind,key.Id,draft.Date!.Value,draft.AmountCents,draft.OpeningCredit,movementId,draft.Name,draft.EvidenceId);
        }
        else if(draft.Kind==FinancialKind.Reversal)
        {
            var original=db.Read("SELECT Id,AccountId,DimensionKind FROM OpeningBalances WHERE MovementId=$id AND RevokedAt IS NULL;",
                r=>(Id:r.GetString(0),Account:r.GetString(1),Kind:r.GetString(2)),("$id",draft.OriginalId)).SingleOrDefault();
            if(original.Id is not null)OpeningBalances.Revoke(db,owner,original.Id,original.Account,original.Kind,draft.Name,movementId);
        }
    }
}
