using UnityEngine;

/// <summary>
/// The shared tip jar on the Pit Desk. It fills with chips as delivered tips arrive and bounces with a
/// sparkle each time, so the tip total reads in the world as well as on the HUD.
/// </summary>
[DisallowMultipleComponent]
public sealed class CasinoTipJarProp : MonoBehaviour
{
    private const int Width = 16;
    private const int Height = 20;
    private const int FullJarPence = 5000;
    private SpriteRenderer jar;
    private Texture2D texture;
    private int shownPence = -1;
    private float bounce;
    private Camera targetCamera;
    private Vector3 baseScale;

    public int ShownPence => shownPence;

    private void Awake()
    {
        texture = new Texture2D(Width, Height, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            hideFlags = HideFlags.HideAndDontSave
        };
        GameObject prop = new GameObject("Pit Desk Tip Jar");
        prop.transform.SetParent(transform, false);
        prop.transform.localPosition = new Vector3(1.25f, 1.3f, -.25f);
        jar = prop.AddComponent<SpriteRenderer>();
        jar.sprite = Sprite.Create(texture, new Rect(0, 0, Width, Height), new Vector2(.5f, 0), 32);
        jar.sprite.hideFlags = HideFlags.HideAndDontSave;
        jar.sortingOrder = 20;
        baseScale = prop.transform.localScale = Vector3.one * 1.1f;
        Redraw(0);
    }

    private void OnEnable() => CasinoPerformanceDirector.TipDelivered += OnTipDelivered;
    private void OnDisable() => CasinoPerformanceDirector.TipDelivered -= OnTipDelivered;

    private void OnTipDelivered(CasinoTipRecord tip)
    {
        bounce = 1;
        CasinoPerformanceDirector.Sparkles(jar.transform.position + Vector3.up * .45f, tip.Vip ? 8 : 4, .45f);
        CasinoSoundscape.Play(CasinoSoundCue.Chip, jar.transform.position);
    }

    private void LateUpdate()
    {
        if (CasinoTipJar.TotalPence != shownPence) Redraw(CasinoTipJar.TotalPence);
        if (targetCamera == null) targetCamera = Camera.main;
        if (targetCamera != null) jar.transform.rotation = targetCamera.transform.rotation;
        bounce = Mathf.Max(0, bounce - Time.deltaTime * 2.5f);
        float squash = Mathf.Sin(bounce * Mathf.PI * 2) * bounce * .18f;
        jar.transform.localScale = new Vector3(baseScale.x * (1 + squash), baseScale.y * (1 - squash), 1);
    }

    private void Redraw(int pence)
    {
        shownPence = pence;
        Color32 clear = new Color32(0, 0, 0, 0);
        Color32 ink = new Color32(29, 24, 43, 255);
        Color32 glass = new Color32(190, 232, 255, 110);
        Color32 shine = new Color32(255, 255, 255, 190);
        Color32 lid = new Color32(201, 140, 62, 255);
        Color32[] chipColours =
        {
            new Color32(220, 64, 60, 255), new Color32(70, 140, 230, 255),
            new Color32(52, 48, 60, 255), new Color32(240, 120, 200, 255)
        };
        Color32[] pixels = new Color32[Width * Height];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = clear;
        int fill = pence <= 0 ? 0 : Mathf.Clamp(1 + Mathf.RoundToInt(12f * pence / FullJarPence), 1, 13);
        for (int y = 1; y < 16; y++)
        for (int x = 2; x < 14; x++)
        {
            bool edge = x == 2 || x == 13 || y == 1;
            pixels[y * Width + x] = edge ? ink : y - 1 <= fill && y > 1 ? chipColours[(y + x / 4) % chipColours.Length] : glass;
        }
        for (int y = 4; y < 13; y++) pixels[y * Width + 4] = shine;
        for (int y = 16; y < 19; y++)
        for (int x = 3; x < 13; x++)
            pixels[y * Width + x] = y == 18 || x == 3 || x == 12 ? ink : lid;
        texture.SetPixels32(pixels);
        texture.Apply(false);
    }

    private void OnDestroy()
    {
        if (texture != null) Destroy(texture);
    }
}
