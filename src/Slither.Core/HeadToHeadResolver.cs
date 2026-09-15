namespace Slither.Core;

public readonly record struct HeadToHeadContestant(
    SnakeId Id,
    WorldVector Position,
    WorldVector Heading,
    int MatchScore);

public readonly record struct HeadToHeadResult(bool FirstDies, bool SecondDies);

public static class HeadToHeadResolver
{
    public static HeadToHeadResult Resolve(
        in HeadToHeadContestant first,
        in HeadToHeadContestant second,
        double approachTieTolerance)
    {
        if (!double.IsFinite(approachTieTolerance) || approachTieTolerance is < 0 or > 2)
            throw new ArgumentOutOfRangeException(nameof(approachTieTolerance));

        var separation = second.Position - first.Position;
        if (separation.Length > 1e-12)
        {
            var towardSecond = separation.NormalizedOr(new WorldVector(1, 0));
            var towardFirst = towardSecond * -1;
            var firstApproach = Dot(first.Heading.NormalizedOr(towardSecond), towardSecond);
            var secondApproach = Dot(second.Heading.NormalizedOr(towardFirst), towardFirst);

            if (Math.Abs(firstApproach - secondApproach) > approachTieTolerance)
            {
                return firstApproach > secondApproach
                    ? new HeadToHeadResult(true, false)
                    : new HeadToHeadResult(false, true);
            }
        }

        if (first.MatchScore != second.MatchScore)
        {
            return first.MatchScore < second.MatchScore
                ? new HeadToHeadResult(true, false)
                : new HeadToHeadResult(false, true);
        }

        return new HeadToHeadResult(true, true);
    }

    private static double Dot(WorldVector left, WorldVector right) =>
        (left.X * right.X) + (left.Y * right.Y);
}
