using UnityEngine;

public static class CasinoFloorLayout
{
    public const float PixelsPerUnit = 32f;
    public const int BlackjackLeftBoxX = 42;
    public const int BlackjackMiddleBoxX = 96;
    public const int BlackjackRightBoxX = 150;
    public const int BlackjackBoxBottom = 42;
    public const int BlackjackBoxTop = 52;
    public const int RouletteGridX = 100;
    public const int RouletteGridY = 47;
    public const int RouletteCellWidth = 4;
    public const int RouletteCellHeight = 6;
    public const int RouletteOutsideY = 40;
    public const int RouletteOutsideWidth = 12;

    public static Vector3 FromPixel(float x, float y, float height) =>
        new Vector3((x - 96f) / PixelsPerUnit, height, (y - 48f) / PixelsPerUnit);

    public static Vector3 BlackjackBet(int seat, float height)
    {
        int x = seat == 0 ? BlackjackLeftBoxX : seat == 1 ? BlackjackMiddleBoxX : BlackjackRightBoxX;
        return FromPixel(x, (BlackjackBoxBottom + BlackjackBoxTop) * .5f, height);
    }

    public static Vector3 RouletteNumber(int number, float height)
    {
        if (number == 0)
            return FromPixel(RouletteGridX - 3, RouletteGridY + RouletteCellHeight * 1.5f, height);
        int column = (number - 1) / 3;
        int row = (number - 1) % 3;
        return FromPixel(RouletteGridX + (column + .5f) * RouletteCellWidth,
            RouletteGridY + (row + .5f) * RouletteCellHeight, height);
    }

    public static Vector3 RouletteBet(RouletteBet bet, float height)
    {
        int outside = bet.Type == RouletteBetType.Red ? 0 : bet.Type == RouletteBetType.Black ? 1 :
            bet.Type == RouletteBetType.Odd ? 2 : bet.Type == RouletteBetType.Even ? 3 : -1;
        if (outside >= 0)
            return FromPixel(RouletteGridX + (outside + .5f) * RouletteOutsideWidth, RouletteOutsideY + 2.5f, height);
        if (bet.Type == RouletteBetType.StraightUp) return RouletteNumber(bet.Number, height);
        Vector3 position = Vector3.zero;
        foreach (int number in bet.Numbers) position += RouletteNumber(number, height);
        return position / bet.Numbers.Length;
    }
}
