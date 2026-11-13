using SarahBeauty.Business.Documents;

namespace SarahBeauty.Business.Exports;

internal static class ReadableDocumentDecisionTables
{
    internal static ReadableExportTable[] Build(ReadableExportSnapshot snapshot,ReadableExportBudget budget)=>snapshot.Read(db=>
    {
        var source=snapshot.Source;var history=DocumentDecisionQueries.Read(db,source.BusinessId);
        var decisions=new ReadableTableBuilder(source,budget,"Document decisions","DocumentDecisionId","BusinessId","DocumentId","VersionId","RequestId","Action","DecisionDate","PriorStatus","ResultStatus","RecordedByOwnerId","RecordedAtUtc");
        foreach(var value in history.OrderBy(v=>v.RecordedAtUtc,StringComparer.Ordinal).ThenBy(v=>v.Id,StringComparer.Ordinal))
            decisions.Add(value.Id,source.BusinessId,value.DocumentId,value.VersionId,value.RequestId,value.Action,value.DecisionDate,value.PriorStatus,value.ResultStatus,value.OwnerId,value.RecordedAtUtc);
        var coverage=new ReadableTableBuilder(source,budget,"Document decision coverage","BusinessId","RetainedDecisionCount","DecisionHistoryScope","PrivateContentPolicy","FinancialEffectPolicy");
        coverage.Add(source.BusinessId,history.Count,"All retained quote and invoice decisions known at capture; not decisions frozen in earlier retained financial reports.","Private reasons and supporting-file descriptors, paths, identifiers and hashes are excluded from these decision tables.","A document decision does not create cash or a journal posting. Dated invoice voids reduce receivables and tax charges in their effective period.");
        return new[]{decisions.Freeze(),coverage.Freeze()};
    });
}
