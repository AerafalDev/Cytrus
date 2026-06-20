namespace Cytrus.Planning;

public readonly record struct ByteRange(long Start, long EndExclusive)
{
    public long Length =>
        EndExclusive - Start;

    public long InclusiveEnd =>
        EndExclusive - 1;
}
