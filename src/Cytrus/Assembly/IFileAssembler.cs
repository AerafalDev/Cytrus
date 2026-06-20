using Cytrus.Planning;
using Cytrus.Storage;

namespace Cytrus.Assembly;

public interface IFileAssembler
{
    Task<FileAssemblyResult> AssembleAsync(
        FilePlan plan,
        IBundleStore store,
        string outputRoot,
        AssemblyOptions options,
        CancellationToken cancellationToken = default);
}
