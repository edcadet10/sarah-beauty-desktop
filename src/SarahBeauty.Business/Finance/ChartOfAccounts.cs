using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Finance;

public sealed class ChartOfAccounts(BusinessStore store)
{
    public List<Account> List(OwnerSession owner) => store.Read(db =>
    {
        store.RequireSession(db, owner);
        return List(db, owner);
    });

    internal static List<Account> List(DbWork db, OwnerSession owner) => db.Read(
        "SELECT Id,Code,Name,Type,CashKind,Active,OpeningConfirmed FROM Accounts WHERE BusinessId=$business ORDER BY Code;",
        r => new Account(r.GetString(0), r.GetString(1), r.GetString(2), r.GetString(3), r.GetString(4), r.GetInt32(5) == 1, r.GetInt32(6) == 1),
        ("$business", owner.BusinessId));

    public void Initialize(OwnerSession owner)
    {
        if (List(owner).Count > 0) return;
        store.Write(owner, "Initialize chart of accounts", owner.BusinessId, db =>
        {
            foreach (var (code, name, type, cash) in Defaults)
                db.Execute("""
                    INSERT OR IGNORE INTO Accounts (Id,BusinessId,Code,Name,Type,CashKind)
                    VALUES ($id,$business,$code,$name,$type,$cash);
                    """, ("$id", Guid.NewGuid().ToString()), ("$business", owner.BusinessId),
                    ("$code", code), ("$name", name), ("$type", type), ("$cash", cash));
            db.Execute("INSERT OR IGNORE INTO BookSettings (BusinessId) VALUES ($business);", ("$business", owner.BusinessId));
            return true;
        });
    }

    public Account Save(OwnerSession owner, Account value) => store.Write(owner, "Save account", value.Id, db =>
    {
        Validate(value);
        var existing = List(db, owner).SingleOrDefault(a => a.Id == value.Id);
        if (existing is not null && (existing.Type != value.Type || existing.CashKind != value.CashKind)
            && db.Scalar("SELECT Id FROM JournalLines WHERE AccountId=$id LIMIT 1;", ("$id", value.Id)) is not null)
            throw new ArgumentException("An account with posted movements must keep its accounting type and cash kind.");
        string id = existing?.Id ?? Guid.NewGuid().ToString();
        db.Execute("""
            INSERT INTO Accounts (Id,BusinessId,Code,Name,Type,CashKind,Active,OpeningConfirmed)
            VALUES ($id,$business,$code,$name,$type,$cash,$active,$opening)
            ON CONFLICT(Id) DO UPDATE SET Code=$code,Name=$name,Type=$type,CashKind=$cash,Active=$active,OpeningConfirmed=$opening;
            """, ("$id", id), ("$business", owner.BusinessId), ("$code", value.Code.Trim()), ("$name", value.Name.Trim()),
            ("$type", value.Type), ("$cash", value.CashKind), ("$active", value.Active ? 1 : 0), ("$opening", value.OpeningConfirmed ? 1 : 0));
        return value with { Id = id };
    });

    private static void Validate(Account value)
    {
        if (string.IsNullOrWhiteSpace(value.Code) || string.IsNullOrWhiteSpace(value.Name)
            || !new[] { "Asset", "Liability", "Equity", "Income", "Expense" }.Contains(value.Type)
            || !new[] { "None", "Cash", "Bank", "Processor" }.Contains(value.CashKind)
            || (value.CashKind != "None" && value.Type != "Asset")) throw new ArgumentException("Check the account code, name, type, and cash kind.");
    }

    private static readonly (string Code, string Name, string Type, string CashKind)[] Defaults =
    [
        ("1000","Cash","Asset","Cash"), ("1010","Bank","Asset","Bank"), ("1020","Processor clearing","Asset","Processor"),
        ("1200","Prepayments","Asset","None"), ("1500","Equipment","Asset","None"), ("1590","Accumulated depreciation","Asset","None"),
        ("2000","Sales tax payable","Liability","None"), ("2100","Owner reimbursements payable","Liability","None"),
        ("2200","Refundable customer deposits","Liability","None"), ("2300","Loan principal","Liability","None"),
        ("2400","Unclassified receipts","Liability","None"), ("3000","Owner contributions","Equity","None"),
        ("3100","Owner withdrawals","Equity","None"), ("3200","Opening equity","Equity","None"),
        ("4000","Service income","Income","None"), ("4010","Asset disposal gain","Income","None"),
        ("5000","Supplies expense","Expense","None"), ("5010","General expense","Expense","None"),
        ("5020","Processor fees","Expense","None"), ("5030","Interest expense","Expense","None"), ("5035","Loan fees","Expense","None"),
        ("5040","Depreciation expense","Expense","None"), ("5060","Asset disposal loss","Expense","None")
    ];
}
