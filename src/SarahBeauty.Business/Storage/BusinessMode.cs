namespace SarahBeauty.Business.Storage;

public enum BusinessMode {Test,Live}
public static class BusinessModes
{
    public static string StoredName(BusinessMode mode)=>mode switch{BusinessMode.Test=>"TEST",BusinessMode.Live=>"LIVE",_=>throw new ArgumentOutOfRangeException(nameof(mode))};
    public static BusinessMode FromStoredName(string value)=>value switch{"TEST"=>BusinessMode.Test,"LIVE"=>BusinessMode.Live,_=>throw new ArgumentException("Choose an explicit supported business mode.",nameof(value))};
}
