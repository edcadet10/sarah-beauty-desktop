using Microsoft.UI.Xaml;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace SarahBeauty_Desktop;

/// <summary>
/// The application window. This hosts a Frame that displays pages. Add your
/// UI and logic to MainPage.xaml / MainPage.xaml.cs instead of here so you
/// can use Page features such as navigation events and the Loaded lifecycle.
/// </summary>
public sealed partial class MainWindow : Window
{
    private bool _closing,_closeReady;
    public MainWindow()
    {
        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        AppWindow.SetIcon(System.IO.Path.Combine(System.AppContext.BaseDirectory,"Assets","AppIcon.ico"));

        // Navigate the root frame to the main page on startup.
        RootFrame.Navigate(typeof(MainPage));
        AppWindow.Closing+=CloseAfterBackups;
    }
    private async void CloseAfterBackups(Microsoft.UI.Windowing.AppWindow sender,Microsoft.UI.Windowing.AppWindowClosingEventArgs args)
    {
        if(_closeReady)return;args.Cancel=true;if(_closing)return;_closing=true;
        try{if(RootFrame.Content is MainPage page)await page.StopBackupsAsync();}
        finally{_closeReady=true;Close();}
    }
}
