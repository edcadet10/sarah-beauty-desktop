using SarahBeauty.Business.Resources;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Finance;

public sealed record LoanLenderSnapshot(string LoanId,string LenderName,string OwnerLenderId,string OwnerLenderName);

internal static class LoanLenderFacts
{
    internal static FinancialDraft Capture(DbWork db,BusinessStore store,OwnerSession owner,FinancialDraft draft)=>
        draft with{LoanLender=Applies(draft) && draft.LoanId.Length>0?Read(db,store,owner,draft.LoanId):null};

    internal static void Validate(DbWork db,BusinessStore store,OwnerSession owner,FinancialDraft draft)
    {
        if(!Applies(draft))return;
        if(draft.LoanLender is null || draft.LoanLender!=Read(db,store,owner,draft.LoanId))
            throw new ArgumentException("The loan lender facts are missing or changed. Edit, save, and review the loan draft again.");
    }

    private static bool Applies(FinancialDraft draft)=>draft.Kind is FinancialKind.LoanProceeds or FinancialKind.LoanPayment
        || draft.Kind==FinancialKind.Opening && draft.LoanId.Length>0;

    private static LoanLenderSnapshot Read(DbWork db,BusinessStore store,OwnerSession owner,string id)
    {
        var loan=new RecordStore<Loan>(store,"Loan").Get(db,owner,id);
        string name=loan.OwnerLenderId.Length==0?"":(string?)db.Scalar("SELECT Name FROM Owners WHERE BusinessId=$business AND Id=$id;",
            ("$business",owner.BusinessId),("$id",loan.OwnerLenderId))??throw new ArgumentException("The loan's recorded owner lender is unavailable.");
        return new(loan.Id,loan.Name,loan.OwnerLenderId,name);
    }
}
