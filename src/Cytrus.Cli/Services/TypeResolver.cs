using Spectre.Console.Cli;

namespace Cytrus.Cli.Services;

public sealed class TypeResolver(IServiceProvider provider) : ITypeResolver, IDisposable
{
    public object? Resolve(Type? type)
    {
        return type is null ? null : provider.GetService(type);
    }

    public void Dispose()
    {
        if (provider is IDisposable disposable)
            disposable.Dispose();
    }
}
