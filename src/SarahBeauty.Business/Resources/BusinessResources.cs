using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Resources;

public sealed class BusinessResources(BusinessStore store)
{
    public List<Loan> Loans(OwnerSession owner) => new RecordStore<Loan>(store, "Loan").List(owner);
    public List<Asset> Assets(OwnerSession owner) => new RecordStore<Asset>(store, "Asset").List(owner);
    public List<Supplier> Suppliers(OwnerSession owner) => new RecordStore<Supplier>(store, "Supplier").List(owner);

    public Loan SaveLoan(OwnerSession owner, Loan value) => Save(owner, "Loan", value, (db, previous) =>
    {
        if (value.OpeningPrincipalCents is < 0) throw new ArgumentException("Opening principal cannot be negative; leave it blank when unknown.");
        if(value.OpeningPrincipalCents>0 && value.OpeningDate is null)throw new ArgumentException("A known nonzero opening principal needs its opening date.");
        if(value.DueDate<value.OpeningDate)throw new ArgumentException("The loan due date cannot precede its opening date.");
        if(value.OwnerLenderId.Length>0 && db.Scalar("SELECT Id FROM Owners WHERE BusinessId=$business AND Id=$id;",("$business",owner.BusinessId),("$id",value.OwnerLenderId)) is null)
            throw new ArgumentException("Choose the actual owner lender from this business.");
        if (previous is not null && (previous.OpeningPrincipalCents != value.OpeningPrincipalCents || previous.OpeningDate != value.OpeningDate)
            && db.Scalar("SELECT Id FROM Movements WHERE LoanId=$id LIMIT 1;", ("$id", value.Id)) is not null)
            throw new ArgumentException("A loan with posted movements needs an approved opening correction.");
    });

    public Asset SaveAsset(OwnerSession owner, Asset value) => Save(owner, "Asset", value, (db, previous) =>
    {
        if (value.CostCents is < 0 || value.TaxBasisCents is < 0 || value.BusinessUseFraction is < 0 or > 1)
            throw new ArgumentException("Check the asset's values and business-use fraction. Unknown values may stay blank.");
        if (value.DisposedDate != previous?.DisposedDate || value.DisposalMovementId!=(previous?.DisposalMovementId??""))
            throw new ArgumentException("Use an approved disposal or its reversal to change disposal details.");
        if(!new[]{"Undecided","Purchased","Contributed","Borrowed"}.Contains(value.Funding) || value.InServiceDate<value.AcquisitionDate)
            throw new ArgumentException("Choose the asset funding and an in-service date on or after acquisition.");
        if(previous is not null && (value.CostCents!=previous.CostCents || value.TaxBasisCents!=previous.TaxBasisCents
            || value.BusinessUseFraction!=previous.BusinessUseFraction || value.AcquisitionDate!=previous.AcquisitionDate || value.InServiceDate!=previous.InServiceDate)
            && db.Scalar("SELECT Id FROM Movements WHERE AssetId=$id LIMIT 1;",("$id",value.Id)) is not null)
            throw new ArgumentException("An asset with posted movements needs a reviewed value or business-use correction. Its original basis cannot be overwritten.");
    });

    public Supplier SaveSupplier(OwnerSession owner, Supplier value) => Save(owner, "Supplier", value, (_, _) =>
    {
        if(value.Email.Length>0 && (!System.Net.Mail.MailAddress.TryCreate(value.Email,out var address) || address.Address!=value.Email))
            throw new ArgumentException("Enter a supplier email address or leave it blank.");
    });

    private T Save<T>(OwnerSession owner, string kind, T value, Action<DbWork,T?> validate) where T : BusinessRecord
    {
        if (string.IsNullOrWhiteSpace(value.Name)) throw new ArgumentException("Enter a name.");
        value = (T)(value with { Id = value.Id.Length == 0 ? Guid.NewGuid().ToString() : value.Id, Name = value.Name.Trim() });
        var records = new RecordStore<T>(store, kind);
        return store.Write(owner, $"Save {kind.ToLowerInvariant()}", value.Id, db =>
        {
            var previous = value.Revision == 0 ? null : records.Get(db, owner, value.Id);
            validate(db, previous);
            return records.Save(db, owner, value);
        });
    }
}
