using Cytrus.Models;

namespace Cytrus.Manifest;

public interface IManifestReader
{
    GameManifest Read(byte[] manifestBytes);
}
