using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Recovery;

internal static class ArchiveDatabaseChecks
{
    internal static ArchiveDatabaseFacts Read(DbWork db)
    {
        if(db.Scalar("PRAGMA integrity_check;") as string!="ok" || db.Read("PRAGMA foreign_key_check;",r=>r.GetValue(0)).Count>0)throw new InvalidDataException("The recovery database failed integrity or relationship checks.");
        CheckHashes(db);CheckOwnership(db);CheckDocumentDecisions(db);CheckBusinessProfiles(db);CheckClientDraftDiscards(db);var tables=db.Read("SELECT name FROM sqlite_schema WHERE type='table' AND name NOT GLOB 'sqlite_*' ORDER BY name;",r=>r.GetString(0));
        var counts=tables.Select(table=>new ArchiveTableCount(table,(long)db.Scalar("SELECT COUNT(*) FROM "+Quote(table)+";")!)).ToArray();
        BigInteger debit=0,credit=0,groupDebit=0,groupCredit=0;string? movement=null;long lines=0;
        using(var command=db.Command("SELECT MovementId,DebitCents,CreditCents FROM JournalLines ORDER BY MovementId,Id;"))
        using(var reader=command.ExecuteReader())while(reader.Read())
        {
            string next=reader.GetString(0);if(movement is not null && next!=movement){Balanced(groupDebit,groupCredit);groupDebit=groupCredit=0;}movement=next;
            long d=reader.GetInt64(1),c=reader.GetInt64(2);if(d<0||c<0)throw new InvalidDataException("The recovery database contains a negative journal side.");
            groupDebit+=d;groupCredit+=c;debit+=d;credit+=c;lines++;
        }
        Balanced(groupDebit,groupCredit);Balanced(debit,credit);
        return new(counts,(long)db.Scalar("SELECT COUNT(*) FROM Movements;")!,lines,debit.ToString(System.Globalization.CultureInfo.InvariantCulture),credit.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }
    private static void Balanced(BigInteger debit,BigInteger credit){if(debit!=credit)throw new InvalidDataException("The recovery database contains an unbalanced posting batch.");}
    private static void CheckHashes(DbWork db)
    {
        Verify(db,"SELECT M.DataJson,B.PayloadHash FROM Movements M JOIN PostingBatches B ON B.Id=M.Id;");
        Verify(db,"SELECT DataJson,SnapshotHash FROM DocumentVersions;");
        foreach(string table in new[]{"Reconciliations","BookPeriodCloses","CancellationDecisions","TripPaymentLinks","TripPaymentReleases","AttachmentVersions","AttachmentLinks","AttachmentLinkReleases"})
            Verify(db,"SELECT SnapshotJson,SnapshotHash FROM "+Quote(table)+";");
    }
    private static void Verify(DbWork db,string sql)
    {
        using var command=db.Command(sql);using var reader=command.ExecuteReader();while(reader.Read())
            if(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(reader.GetString(0))))!=reader.GetString(1))throw new InvalidDataException("A retained source failed its recovery hash check.");
    }
    private static string Quote(string identifier)=>"\""+identifier.Replace("\"","\"\"")+"\"";
    private static void CheckOwnership(DbWork db)
    {
        if(Convert.ToInt64(db.Scalar("PRAGMA user_version;"))<33)return;
        foreach(string business in db.Read("SELECT Id FROM Business ORDER BY Id;",r=>r.GetString(0)))
            SarahBeauty.Business.Ownership.OwnerInterestQueries.Read(db,business);
    }
    private static void CheckDocumentDecisions(DbWork db)
    {
        if(Convert.ToInt64(db.Scalar("PRAGMA user_version;"))<36)return;
        foreach(string business in db.Read("SELECT Id FROM Business ORDER BY Id;",r=>r.GetString(0)))
            SarahBeauty.Business.Documents.DocumentDecisionQueries.Read(db,business);
    }
    private static void CheckBusinessProfiles(DbWork db)
    {
        if(Convert.ToInt64(db.Scalar("PRAGMA user_version;"))<39)return;
        foreach(string business in db.Read("SELECT Id FROM Business ORDER BY Id;",r=>r.GetString(0)))
            SarahBeauty.Business.Setup.BusinessProfileQueries.Read(db,business);
    }
    private static void CheckClientDraftDiscards(DbWork db)
    {
        if(Convert.ToInt64(db.Scalar("PRAGMA user_version;"))<40)return;
        foreach(string business in db.Read("SELECT Id FROM Business ORDER BY Id;",r=>r.GetString(0)))
            SarahBeauty.Business.Documents.ClientDraftDiscardQueries.Read(db,business);
    }
}
