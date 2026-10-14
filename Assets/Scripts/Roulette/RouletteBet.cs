using System;

public enum RouletteBetType
{
    StraightUp,
    Red,
    Black,
    Odd,
    Even
}

[Serializable]
public readonly struct RouletteBet
{
    public RouletteBetType Type { get; }
    public int Number { get; }
    public int StakePence { get; }

    public RouletteBet(RouletteBetType type, int number, int stakePence)
    {
        Type = type;
        Number = number;
        StakePence = stakePence;
    }
}

public static class EuropeanRouletteRules
{
    public static bool IsValidBet(RouletteBet bet) =>
        bet.StakePence > 0 &&
        (bet.Type != RouletteBetType.StraightUp || IsValidNumber(bet.Number)) &&
        Enum.IsDefined(typeof(RouletteBetType), bet.Type);

    public static bool IsValidNumber(int number) => number >= 0 && number <= 36;

    public static bool IsRed(int number)
    {
        switch (number)
        {
            case 1: case 3: case 5: case 7: case 9: case 12:
            case 14: case 16: case 18: case 19: case 21: case 23:
            case 25: case 27: case 30: case 32: case 34: case 36:
                return true;
            default:
                return false;
        }
    }

    public static bool Wins(RouletteBet bet, int winningNumber)
    {
        if (!IsValidBet(bet) || !IsValidNumber(winningNumber))
        {
            return false;
        }

        switch (bet.Type)
        {
            case RouletteBetType.StraightUp:
                return bet.Number == winningNumber;
            case RouletteBetType.Red:
                return winningNumber != 0 && IsRed(winningNumber);
            case RouletteBetType.Black:
                return winningNumber != 0 && !IsRed(winningNumber);
            case RouletteBetType.Odd:
                return winningNumber != 0 && winningNumber % 2 == 1;
            case RouletteBetType.Even:
                return winningNumber != 0 && winningNumber % 2 == 0;
            default:
                return false;
        }
    }

    public static bool TryGetGrossReturnPence(RouletteBet bet, int winningNumber, out long grossReturnPence)
    {
        if (!IsValidBet(bet) || !IsValidNumber(winningNumber))
        {
            grossReturnPence = 0;
            return false;
        }

        if (!Wins(bet, winningNumber))
        {
            grossReturnPence = 0;
            return true;
        }

        long multiplier = bet.Type == RouletteBetType.StraightUp ? 36L : 2L;
        grossReturnPence = bet.StakePence * multiplier;
        return true;
    }
}
