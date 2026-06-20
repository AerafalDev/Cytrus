using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Cytrus.App.ViewModels;

namespace Cytrus.App.Views;

public partial class MainView : ShadUI.Window
{
    public MainView()
    {
        InitializeComponent();
    }

    private async void OnBrowseClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (DataContext is not MainViewModel vm)
                return;

            var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = "Choose output directory", AllowMultiple = false });

            if (folders.Count > 0 && folders[0].TryGetLocalPath() is { } path)
                vm.OutputDirectory = path;
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
        }
    }
}

