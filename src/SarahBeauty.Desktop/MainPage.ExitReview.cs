using SarahBeauty_Desktop.Views;

namespace SarahBeauty_Desktop;

public sealed partial class MainPage
{
    private WorkspaceExitReview? _exitReview;
    private WorkspaceExitReview ExitReview=>_exitReview??=new(Host);

    internal Task<bool> ConfirmWindowCloseAsync()=>_session is null?Task.FromResult(true):ExitReview.ConfirmAsync("Close Sarah Beauty?","Close app");

    private async Task RequestOwnerLockAsync()
    {
        int generation=_workspaceGeneration;
        if(await ExitReview.ConfirmAsync("Lock Sarah Beauty?","Lock now")&&generation==_workspaceGeneration&&_session is not null)
            LockOwner();
    }
}
