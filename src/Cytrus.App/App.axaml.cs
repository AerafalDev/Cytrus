using Avalonia;
using Avalonia.Markup.Xaml;

namespace Cytrus.App;

public sealed class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
    }
}
