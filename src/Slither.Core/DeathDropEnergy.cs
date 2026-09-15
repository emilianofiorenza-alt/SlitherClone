namespace Slither.Core;

public static class DeathDropEnergy
{
    public static int ForIndex(int totalEnergy, int dotCount, int index)
    {
        if (totalEnergy < 0) throw new ArgumentOutOfRangeException(nameof(totalEnergy));
        if (dotCount < 1) throw new ArgumentOutOfRangeException(nameof(dotCount));
        if (index < 0 || index >= dotCount) throw new ArgumentOutOfRangeException(nameof(index));
        return (totalEnergy / dotCount) + (index < totalEnergy % dotCount ? 1 : 0);
    }
}
