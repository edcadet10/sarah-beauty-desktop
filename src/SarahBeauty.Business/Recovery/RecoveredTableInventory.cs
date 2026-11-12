using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Recovery;

internal static class RecoveredTableInventory
{
    private static readonly (int Introduced,string Table)[] AddedTables=[(33,"OwnerInterestVersions"),(33,"OwnerInterestShares"),(36,"DocumentDecisions"),(39,"BusinessProfileVersions"),(40,"ClientDraftDiscards")];
    internal static void Verify(ArchiveDatabaseFacts current,ArchiveManifest source)
    {
        var expected=source.Facts.Tables.ToDictionary(t=>t.Table,t=>t.Rows,StringComparer.Ordinal);
        var added=AddedTables.Where(t=>t.Introduced>source.SchemaVersion&&t.Introduced<=BusinessSchema.Version&&!expected.ContainsKey(t.Table)).Select(t=>t.Table).ToHashSet(StringComparer.Ordinal);
        if(!current.Tables.Select(t=>t.Table).ToHashSet(StringComparer.Ordinal).SetEquals(expected.Keys.Concat(added)))throw Invalid();
        foreach(var table in current.Tables)
        {
            if(added.Contains(table.Table)){if(table.Rows!=0)throw Invalid();continue;}
            long increase=table.Table=="Audit"?1:table.Table=="AppMetadata"?1+(source.SchemaVersion<32?2:0):0;
            if(table.Rows!=checked(expected[table.Table]+increase))throw Invalid();
        }
    }
    private static InvalidDataException Invalid()=>new("The prepared record inventory changed before activation. Only known empty migration tables may be added.");
}
