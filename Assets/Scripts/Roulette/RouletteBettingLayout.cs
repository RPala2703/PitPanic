using System;
using UnityEngine;

public static class RouletteBettingLayout
{
    public const int ColumnCount = 3;
    public const int RowCount = 12;

    public static bool TryGetNumberCell(int number, out int column, out int row)
    {
        if (number < 1 || number > 36)
        {
            column = -1;
            row = -1;
            return false;
        }

        int zeroBased = number - 1;
        column = zeroBased % ColumnCount;
        row = zeroBased / ColumnCount;
        return true;
    }

    public static Vector2 GetNumberCellPosition(
        int number,
        Vector2 firstCell,
        float columnSpacing,
        float rowSpacing)
    {
        if (!TryGetNumberCell(number, out int column, out int row))
        {
            throw new ArgumentOutOfRangeException(nameof(number), "A numbered roulette cell must be from 1 to 36.");
        }

        return firstCell + new Vector2(column * columnSpacing, -row * rowSpacing);
    }

    public static Vector2 GetOutsideBetPosition(
        RouletteBetType betType,
        Vector2 firstCell,
        float spacing)
    {
        int index = betType switch
        {
            RouletteBetType.Red => 0,
            RouletteBetType.Black => 1,
            RouletteBetType.Odd => 2,
            RouletteBetType.Even => 3,
            _ => throw new ArgumentOutOfRangeException(nameof(betType), "A straight-up bet is not an outside bet.")
        };

        return firstCell + new Vector2(index * spacing, 0f);
    }

    public static bool IsOutsideBet(RouletteBetType betType) =>
        betType == RouletteBetType.Red ||
        betType == RouletteBetType.Black ||
        betType == RouletteBetType.Odd ||
        betType == RouletteBetType.Even;
}
