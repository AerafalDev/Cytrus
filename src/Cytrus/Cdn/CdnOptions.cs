namespace Cytrus.Cdn;

public sealed record CdnOptions
{
    public Uri BaseAddress { get; init; } = new("https://cytrus.cdn.ankama.com/");

    public string IndexPath { get; init; } = "cytrus.json";

    public string UserAgent { get; init; } = "Cytrus/1.0 (+https://github.com/)";

    public int MaxAttempts { get; init; } = 5;

    public TimeSpan RetryBaseDelay { get; init; } = TimeSpan.FromMilliseconds(300);

    public TimeSpan RetryMaxDelay { get; init; } = TimeSpan.FromSeconds(10);

    public TimeSpan ConnectTimeout { get; init; } = TimeSpan.FromSeconds(15);

    public TimeSpan ResponseHeadersTimeout { get; init; } = TimeSpan.FromSeconds(60);

    public void Validate()
    {
        if (!BaseAddress.IsAbsoluteUri)
            throw new InvalidOperationException("CdnOptions.BaseAddress must be an absolute URI.");

        if (BaseAddress.Scheme != Uri.UriSchemeHttps)
            throw new InvalidOperationException("CdnOptions.BaseAddress must use HTTPS.");

        if (MaxAttempts < 1)
            throw new InvalidOperationException("CdnOptions.MaxAttempts must be >= 1.");
    }
}
