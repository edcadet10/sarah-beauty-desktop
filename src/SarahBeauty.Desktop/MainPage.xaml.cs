using System;
using Microsoft.UI.Xaml.Controls;
using SarahBeauty_Desktop.Storage;

namespace SarahBeauty_Desktop;

public sealed partial class MainPage : Page
{
    public MainPage()
    {
        InitializeComponent();

        try
        {
            var database = LocalDatabase.InitializeTestDatabase();

            StoragePathText.Text =
                $"{database.DatabasePath}\n" +
                $"Store ID: {database.StoreId}\n" +
                $"Schema version: {database.SchemaVersion}";
        }
        catch (Exception error)
        {
            StoragePathText.Text =
                $"Could not prepare TEST storage: {error.Message}";
        }
    }
}