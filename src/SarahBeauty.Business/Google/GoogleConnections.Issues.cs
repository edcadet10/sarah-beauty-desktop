namespace SarahBeauty.Business.Google;

public sealed partial class GoogleConnections
{
    internal bool ObserveFailure(GoogleAccessGrant grant,GoogleConnectionFailure failure)=>ObserveFailure(grant.Connection,failure);
    internal bool ObserveFailure(GoogleConnectionRecord expected,GoogleConnectionFailure failure)
    {
        if(expected.BusinessId!=_business || expected.InstanceId!=_store.InstanceId || !GoogleConnectionIssues.RequiresConsent(failure.Code))return false;
        try
        {
            return _store.Transaction(db=>
            {
                var current=GoogleConnectionRecords.Read(db,_business,_store.InstanceId);
                if(current?.Id!=expected.Id || current.Status!="Connected" || current.ConfigurationId!=expected.ConfigurationId || GoogleClientRecords.Read(db,_business,_store.InstanceId)?.Id!=expected.ConfigurationId)return false;
                GoogleConnectionIssues.Save(db,new(1,current.Id,_business,_store.InstanceId,failure.Code,DateTimeOffset.UtcNow.ToString("O")));return true;
            });
        }
        catch(Exception error) when(error is IOException or InvalidDataException or Microsoft.Data.Sqlite.SqliteException){throw new GoogleConnectionFailure("ConnectionStateUnconfirmed","Google authorization needs review, and its local failure state could not be saved. Review the connection before retrying.");}
    }
}
