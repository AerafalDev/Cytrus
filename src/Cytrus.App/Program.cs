using Avalonia;

namespace Cytrus.App;

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        AppBuilder
            .Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace()
            .StartWithClassicDesktopLifetime(args);
    }
}
