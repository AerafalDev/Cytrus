using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Cytrus.App.ViewModels;
using Cytrus.App.Views;
using Cytrus.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;

namespace Cytrus.App;

public sealed class App : Application
{
    private ServiceProvider? _services;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        var desktop = (IClassicDesktopStyleApplicationLifetime)ApplicationLifetime!;

        _services = new ServiceCollection()
            .AddLogging(static builder => builder.ClearProviders().AddSerilog())
            .AddCytrus()
            .AddSingleton<MainViewModel>()
            .BuildServiceProvider();

        var viewModel = _services.GetRequiredService<MainViewModel>();

        desktop.MainWindow = new MainView { DataContext = viewModel };
        desktop.ShutdownRequested += (_, _) => _services?.Dispose();

        Dispatcher.InvokeAsync(() => viewModel.RefreshIndexCommand.ExecuteAsync(null));
    }
}
