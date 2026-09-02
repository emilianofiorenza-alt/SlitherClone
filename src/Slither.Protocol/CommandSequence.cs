namespace Slither.Protocol;

public sealed class CommandSequence
{
    private uint _current;

    public uint Next() => _current = unchecked(_current + 1);
}
