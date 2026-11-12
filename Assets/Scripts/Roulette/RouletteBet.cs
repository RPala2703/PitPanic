using System;

public enum RouletteBetType
{
    StraightUp,
    Red,
    Black,
    Odd,
    Even,
    Split,
    Street,
    Corner,
    SixLine
}

[Serializable]
public readonly struct RouletteBet
{
    public RouletteBetType Type { get; }
    public int Number { get; }
    public int StakePence { get; }
    public int[] Numbers { get; }

    public RouletteBet(RouletteBetType type, int number, int stakePence)
    {
        Type = type;
        Number = number;
        StakePence = stakePence;
        Numbers = type == RouletteBetType.StraightUp ? new[] { number } : Array.Empty<int>();
    }

    public RouletteBet(RouletteBetType type, int[] numbers, int stakePence)
    {
        Type = type;
        Number = numbers != null && numbers.Length > 0 ? numbers[0] : -1;
        StakePence = stakePence;
        Numbers = numbers != null ? (int[])numbers.Clone() : Array.Empty<int>();
    }
}

public static class EuropeanRouletteRules
{
    public static bool IsValidBet(RouletteBet bet) =>
        bet.StakePence > 0 && Enum.IsDefined(typeof(RouletteBetType), bet.Type) &&
        (bet.Type switch
        {
            RouletteBetType.StraightUp => IsValidNumber(bet.Number),
            RouletteBetType.Split => IsLegalSplit(bet.Numbers),
            RouletteBetType.Street => IsLegalStreet(bet.Numbers),
            RouletteBetType.Corner => IsLegalCorner(bet.Numbers),
            RouletteBetType.SixLine => IsLegalSixLine(bet.Numbers),
            _ => bet.Type == RouletteBetType.Red || bet.Type == RouletteBetType.Black ||
                 bet.Type == RouletteBetType.Odd || bet.Type == RouletteBetType.Even
        });

    public static bool IsLegalSplit(int[] numbers) => numbers != null && numbers.Length == 2 &&
        numbers[0] != numbers[1] && IsValidNumber(numbers[0]) && IsValidNumber(numbers[1]) &&
        (Math.Min(numbers[0], numbers[1]) == 0 && Math.Max(numbers[0], numbers[1]) <= 3 ||
         numbers[0] > 0 && numbers[1] > 0 && (Math.Abs(numbers[0] - numbers[1]) == 3 ||
         Math.Abs(numbers[0] - numbers[1]) == 1 && Math.Min(numbers[0], numbers[1]) % 3 != 0));
    public static bool IsLegalStreet(int[] numbers) => IsConsecutiveLine(numbers, 3) &&
        numbers[0] % 3 == 1 && numbers[0] <= 34;
    public static bool IsLegalCorner(int[] numbers) => numbers != null && numbers.Length == 4 &&
        numbers[0] > 0 && IsValidNumber(numbers[0]) && numbers[0] % 3 != 0 && numbers[0] <= 32 &&
        Contains(numbers, numbers[0] + 1) && Contains(numbers, numbers[0] + 3) && Contains(numbers, numbers[0] + 4);
    public static bool IsLegalSixLine(int[] numbers) => numbers != null && numbers.Length == 6 &&
        IsLegalStreet(new[] { numbers[0], numbers[1], numbers[2] }) &&
        IsLegalStreet(new[] { numbers[3], numbers[4], numbers[5] }) &&
        numbers[0] % 3 == numbers[3] % 3 && numbers[3] == numbers[0] + 3;
    private static bool IsConsecutiveLine(int[] numbers, int count) => numbers != null && numbers.Length == count &&
        IsValidNumber(numbers[0]) && Enumerable(numbers);
    private static bool Enumerable(int[] numbers)
    {
        for (int i = 1; i < numbers.Length; i++)
            if (numbers[i] != numbers[0] + i) return false;
        return true;
    }
    private static bool Contains(int[] numbers, int number)
    {
        foreach (int candidate in numbers) if (candidate == number) return true;
        return false;
    }
    private static bool InNumbers(RouletteBet bet, int winning) => Contains(bet.Numbers, winning);

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
            case RouletteBetType.Split:
            case RouletteBetType.Street:
            case RouletteBetType.Corner:
            case RouletteBetType.SixLine:
                return InNumbers(bet, winningNumber);
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

    public static bool TryGetWinningsPence(RouletteBet bet, int winningNumber, out long winningsPence)
    {
        if (!IsValidBet(bet) || !IsValidNumber(winningNumber))
        {
            winningsPence = 0;
            return false;
        }

        if (!Wins(bet, winningNumber))
        {
            winningsPence = 0;
            return true;
        }

        long profitOdds = bet.Type switch
        {
            RouletteBetType.StraightUp => 35,
            RouletteBetType.Split => 17,
            RouletteBetType.Street => 11,
            RouletteBetType.Corner => 8,
            RouletteBetType.SixLine => 5,
            _ => 1
        };
        winningsPence = bet.StakePence * profitOdds;
        return true;
    }
}
