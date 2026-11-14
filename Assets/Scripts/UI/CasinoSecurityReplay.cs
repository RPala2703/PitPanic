using UnityEngine;
using UnityEngine.UI;

// Close-up reconstruction from the immutable recorded wager, not the live table.
public sealed class CasinoSecurityReplay : MonoBehaviour
{
    public const float Duration = 7.5f;
    private readonly Image[] pockets = new Image[37];
    private Image customer;
    private Image chip;
    private Image hand;
    private Image wheel;
    private Image resultMarker;
    private Text result;
    private Sprite[] handFrames;
    private Texture2D handAtlas;
    private CasinoSecurityEvidence evidence;

    private Image Picture(string name, Vector2 position, Vector2 size, Color color) =>
        CasinoMinigamePresentation.CreateImage(name, transform, position, size, color).GetComponent<Image>();

    private Text Label(string name, string text, Vector2 position, Vector2 size, int fontSize) =>
        CasinoMinigamePresentation.CreateText(transform, name, text, position, size, fontSize, Color.white);

    public void Build()
    {
        Picture("Felt", Vector2.zero, new Vector2(1160, 430), new Color(.04f, .23f, .18f));
        Picture("Rail", new Vector2(0, 200), new Vector2(1160, 20), new Color(.37f, .20f, .13f));
        Picture("Rail highlight", new Vector2(0, 191), new Vector2(1160, 3), CasinoHudStyle.Gold);
        Picture("Camera divider", new Vector2(-265, 0), new Vector2(3, 370), new Color(.7f, .65f, .4f));
        for (int number = 0; number <= 36; number++)
        {
            Color color = number == 0 ? new Color(.08f, .40f, .25f) :
                EuropeanRouletteRules.Wins(new RouletteBet(RouletteBetType.Red, -1, 500), number)
                    ? new Color(.58f, .10f, .15f) : new Color(.08f, .09f, .12f);
            Vector2 position = NumberPosition(number);
            Picture($"Pocket {number} rim", position, new Vector2(51, number == 0 ? 151 : 49), CasinoHudStyle.Gold);
            pockets[number] = Picture($"Pocket {number}", position, new Vector2(48, number == 0 ? 148 : 46), color);
            Label($"Number {number}", number.ToString(), position, new Vector2(45, 44), 20);
        }
        string[] outside = { "RED", "BLACK", "ODD", "EVEN" };
        for (int i = 0; i < outside.Length; i++)
        {
            Vector2 position = OutsidePosition(i);
            Picture(outside[i] + " rim", position, new Vector2(146, 48), CasinoHudStyle.Gold);
            Picture(outside[i] + " felt", position, new Vector2(143, 45),
                i == 0 ? new Color(.58f, .10f, .15f) : i == 1 ? new Color(.08f, .09f, .12f) : new Color(.04f, .26f, .18f));
            Label(outside[i], outside[i], position, new Vector2(140, 38), 17);
        }
        customer = Picture("Recorded customer", new Vector2(-420, 96), new Vector2(140, 178), Color.white);
        customer.preserveAspect = true;
        wheel = Picture("Recorded wheel", new Vector2(-420, -108), new Vector2(142, 142), Color.white);
        CasinoPixelArt.SetSprite(wheel, CasinoPixelArt.Wheel, Color.white);
        Picture("Wheel pointer", new Vector2(-420, -31), new Vector2(12, 18), CasinoHudStyle.Gold);
        Picture("Result badge", new Vector2(-315, -107), new Vector2(68, 66), CasinoHudStyle.Ink);
        result = Label("Wheel result", "?", new Vector2(-315, -107), new Vector2(65, 58), 32);
        resultMarker = Picture("Winning pocket", Vector2.zero, new Vector2(26, 26), Color.white);
        CasinoPixelArt.SetSprite(resultMarker, CasinoPixelArt.Sparkle, Color.white);
        chip = Picture("Recorded wager", Vector2.zero, new Vector2(34, 34), Color.white);
        GenerateHandFrames();
        hand = Picture("Recorded hand", Vector2.zero, new Vector2(104, 156), Color.white);
        hand.preserveAspect = true;
        foreach (Graphic graphic in GetComponentsInChildren<Graphic>()) graphic.raycastTarget = false;
    }

    public void Load(CasinoSecurityEvidence recorded)
    {
        evidence = recorded;
        CasinoPixelArt.SetSprite(chip, CasinoPixelArt.ChipForValue(recorded.Bet.Bet.StakePence), Color.white);
        Render(0);
    }

    public static Vector2 NumberPosition(int number) => number == 0
        ? new Vector2(-215, -5)
        : new Vector2(-161 + (number - 1) / 3 * 52, -55 + (number - 1) % 3 * 50);

    private static Vector2 OutsidePosition(int index) => new Vector2(-91 + index * 151, -112);

    public static Vector2 BetPosition(RouletteBet bet)
    {
        if (bet.Type == RouletteBetType.Red) return OutsidePosition(0) + new Vector2(50, 0);
        if (bet.Type == RouletteBetType.Black) return OutsidePosition(1) + new Vector2(50, 0);
        if (bet.Type == RouletteBetType.Odd) return OutsidePosition(2) + new Vector2(50, 0);
        if (bet.Type == RouletteBetType.Even) return OutsidePosition(3) + new Vector2(50, 0);
        Vector2 position = Vector2.zero;
        if (bet.Numbers.Length == 0) return NumberPosition(bet.Number);
        foreach (int number in bet.Numbers) position += NumberPosition(number);
        return position / bet.Numbers.Length;
    }

    public static Vector2 ChipPosition(CasinoSecurityEvidence recorded, float elapsed)
    {
        Vector2 original = BetPosition(recorded.Bet.Bet);
        return recorded.IsCheating
            ? Vector2.Lerp(original, NumberPosition(recorded.AttemptedNumber),
                Mathf.SmoothStep(0, 1, Mathf.Clamp01((elapsed - 5.1f) / 1.1f)))
            : original;
    }

    public void Render(float elapsed)
    {
        if (evidence == null) return;
        float time = Mathf.Clamp(elapsed, 0, Duration);
        Vector2 original = BetPosition(evidence.Bet.Bet);
        Vector2 wager = ChipPosition(evidence, time);
        chip.rectTransform.anchoredPosition = wager;
        chip.enabled = time >= 1.1f;
        float stopAngle = System.Array.IndexOf(CasinoPixelArt.WheelOrder, evidence.WinningNumber) * (360f / 37f) - 1080;
        wheel.rectTransform.localRotation = Quaternion.Euler(0, 0, time < 3.6f
            ? Mathf.Lerp(0, stopAngle, Mathf.SmoothStep(0, 1, Mathf.Clamp01((time - 1.7f) / 1.9f))) : stopAngle);
        result.text = time < 3.6f ? "?" : evidence.WinningNumber.ToString();
        resultMarker.enabled = time >= 3.6f;
        resultMarker.rectTransform.anchoredPosition = NumberPosition(evidence.WinningNumber) + new Vector2(13, 12);
        int pose = time < 2 ? 0 : time < 4.2f ? 1 : 2;
        float poseStart = pose == 0 ? 0 : pose == 1 ? 2 : 4.2f;
        int frame = PixelCharacterVisual.PlaybackFrame(time - poseStart, pose == 2 ? 2.3f : 2, pose == 0);
        customer.sprite = evidence.CustomerFrames[pose * 60 + frame];
        customer.enabled = customer.sprite != null;
        bool placing = time < 1.7f;
        float reach = placing ? Mathf.Clamp01(time / .7f) : Mathf.Clamp01((time - 4.2f) / .8f);
        float withdraw = placing ? Mathf.Clamp01((time - 1.2f) / .5f) : Mathf.Clamp01((time - 6.2f) / .7f);
        float extension = Mathf.SmoothStep(0, 1, reach) * (1 - Mathf.SmoothStep(0, 1, withdraw));
        hand.enabled = placing || time >= 4.2f && time < 6.9f;
        hand.sprite = handFrames[Mathf.Clamp(Mathf.RoundToInt(extension * 59), 0, 59)];
        hand.rectTransform.anchoredPosition = Vector2.Lerp(original + new Vector2(0, 230),
            wager + new Vector2(0, 67), extension);
    }

    // Raster frames include cuff, palm, knuckles and curled fingers unfolding into a reach.
    private void GenerateHandFrames()
    {
        const int width = 128, height = 192, columns = 8, rows = 8;
        handAtlas = new Texture2D(width * columns, height * rows, TextureFormat.RGBA32, false);
        handAtlas.filterMode = FilterMode.Bilinear;
        handAtlas.SetPixels32(new Color32[handAtlas.width * handAtlas.height]);
        handFrames = new Sprite[60];
        for (int frame = 0; frame < 60; frame++)
        {
            Color32[] pixels = new Color32[width * height];
            float reach = Mathf.SmoothStep(0, 1, frame / 59f);
            void Box(int x, int y, int w, int h, Color32 color)
            {
                for (int yy = Mathf.Max(0, y); yy < Mathf.Min(height, y + h); yy++)
                    for (int xx = Mathf.Max(0, x); xx < Mathf.Min(width, x + w); xx++) pixels[yy * width + xx] = color;
            }
            void Oval(int cx, int cy, int rx, int ry, Color32 color)
            {
                for (int y = Mathf.Max(0, cy - ry); y < Mathf.Min(height, cy + ry); y++)
                    for (int x = Mathf.Max(0, cx - rx); x < Mathf.Min(width, cx + rx); x++)
                        if ((x - cx) * (x - cx) / (float)(rx * rx) + (y - cy) * (y - cy) / (float)(ry * ry) <= 1)
                            pixels[y * width + x] = color;
            }
            Color32 outline = new Color32(48, 31, 29, 255);
            Color32 skin = new Color32(198, 151, 103, 255);
            Color32 light = new Color32(233, 185, 132, 255);
            Box(39, 118, 51, 74, outline);
            Box(43, 122, 43, 70, new Color32(54, 70, 83, 255));
            Box(47, 122, 5, 70, new Color32(88, 110, 124, 255));
            Box(40, 111, 50, 13, new Color32(237, 229, 210, 255));
            Oval(65, 97, 27, 32, outline);
            Oval(65, 99, 23, 28, skin);
            Oval(60, 104, 13, 19, light);
            for (int finger = 0; finger < 4; finger++)
            {
                int x = 46 + finger * 12;
                int length = Mathf.RoundToInt(Mathf.Lerp(12, 40 + (finger == 1 ? 9 : finger == 3 ? -10 : 0), reach));
                Oval(x, 70 - length / 2, 7, length / 2 + 9, outline);
                Oval(x, 72 - length / 2, 5, length / 2 + 7, skin);
                Oval(x - 1, 69 - length, 3, 5, light);
                Box(x - 3, 62, 6, 2, new Color32(158, 109, 75, 255));
            }
            Oval(40, 89, 11, 20, outline);
            Oval(41, 91, 8, 17, skin);
            Box(79, 147, 4, 4, new Color32(223, 186, 83, 255));
            int atlasX = frame % columns * width, atlasY = frame / columns * height;
            handAtlas.SetPixels32(atlasX, atlasY, width, height, pixels);
            handFrames[frame] = Sprite.Create(handAtlas, new Rect(atlasX, atlasY, width, height), new Vector2(.5f, .5f), 100);
        }
        handAtlas.Apply(false, true);
    }

    private void OnDestroy()
    {
        if (handFrames != null) foreach (Sprite frame in handFrames) if (frame != null) Destroy(frame);
        if (handAtlas != null) Destroy(handAtlas);
    }
}
