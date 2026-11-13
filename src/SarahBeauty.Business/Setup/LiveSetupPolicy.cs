namespace SarahBeauty.Business.Setup;

public static class LiveSetupPolicy
{
    public static BusinessProfile ForCreation(BusinessProfile draft)
    {
        var value=BusinessProfileRules.Normalize(draft);
        if(value.Currency!="USD"||value.TimeZoneId is null||value.BooksStartDate is null||!value.NewBusinessOnlyConfirmed)
            throw new ArgumentException("Review the USD currency, business time zone, books start date and new-business-only scope before creating a LIVE workspace.");
        return value;
    }
    public static IReadOnlyList<string> PendingOperationalReviews(BusinessProfile draft)
    {
        var value=ForCreation(draft);var pending=new List<string>();if(value.LegalName.Length==0)pending.Add("Legal business name");
        foreach(var item in new[]{("Formation",value.Formation),("EIN",value.EinReview),("Banking",value.Banking),("Tax",value.Tax)})
            if(item.Item2.State is not(BusinessSetupState.Confirmed or BusinessSetupState.NotApplicable))pending.Add(item.Item1+" review");
        return pending.AsReadOnly();
    }
    public static void RequireOperation(BusinessProfile draft,DateOnly date,DateTimeOffset now)
    {
        var value=ForCreation(draft);var pending=PendingOperationalReviews(value);
        if(pending.Count>0)throw new ArgumentException("Complete the explicit LIVE setup reviews first: "+string.Join(", ",pending)+". Unconfirmed facts remain unconfirmed.");
        var today=DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now,TimeZoneInfo.FindSystemTimeZoneById(value.TimeZoneId!)).DateTime);
        if(date<value.BooksStartDate||date>today)throw new ArgumentException("Use a LIVE operation date from the reviewed books start through today in the business time zone. Review earlier-business amounts through the explicit opening workflow.");
    }
}
