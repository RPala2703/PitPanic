using UnityEngine;
using UnityEngine.UI;

public static class CasinoPixelFont
{
    private static Font cached;

    public static Font Default
    {
        get
        {
            if (cached == null)
                cached = Resources.Load<Font>("Fonts/Silkscreen-Regular");
            if (cached == null)
                throw new System.InvalidOperationException("The Silkscreen pixel font is missing from Resources/Fonts.");
            return cached;
        }
    }

    public static Font Symbols
    {
        get
        {
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null)
                throw new System.InvalidOperationException("The built-in font required for playing-card suits is unavailable.");
            return font;
        }
    }

    public static void Fit(Text text, int fontSize)
    {
        text.resizeTextForBestFit = true;
        text.resizeTextMinSize = Mathf.Max(8, Mathf.RoundToInt(fontSize * 0.6f));
        text.resizeTextMaxSize = fontSize;
    }
}
