using System;
using Microsoft.UI.Xaml.Controls;
using SarahBeauty_Desktop.Storage;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace SarahBeauty_Desktop;

/// <summary>
/// The main content page displayed inside the application window.
/// Add your UI logic, event handlers, and data binding here.
/// </summary>
public sealed partial class MainPage : Page
{
    public MainPage()
    {
        InitializeComponent();

        try
        {
            var database = LocalDatabase.InitializeTestDatabase();

            StoragePathText.Text =
                $"{database.DatabasePath}\nStore ID: {database.StoreId}";
        }
        catch (Exception error)
        {
            StoragePathText.Text = $"Could not prepare TEST storage:{error.Message}";
        }

    }
}
