using System;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class CasinoTitleScreen : MonoBehaviour
{
    private RectTransform logo;
    private Image[] lights;
    private RectTransform[] actors;
    private Vector2[] homes;
    private Image[] actorImages;
    private PixelCharacterVisual[] cast;
    private GameObject settings;
    private Camera gameCamera;
    private int savedCullingMask;
    private float elapsed;

    public void Build(Action newGame, Action continueGame, bool canContinue)
    {
        CasinoSoundscape.SetIntensity(0);
        CasinoSoundscape.SetFocus(transform);
        RectTransform rect = (RectTransform)transform;
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        GetComponent<Image>().color = new Color(.17f, .08f, .22f);
        gameCamera = Camera.main;
        if (gameCamera != null) { savedCullingMask = gameCamera.cullingMask; gameCamera.cullingMask = 0; }

        SpriteRenderer carpet = GameObject.Find("Pixel Carpet")?.GetComponent<SpriteRenderer>();
        Art("Carpet backdrop", carpet != null ? carpet.sprite : null, Vector2.zero, new Vector2(1600, 1000));
        foreach (string n in new[] { "Pixel BlackjackV2", "Pixel Roulette", "Pixel CashDesk", "Pixel PitDesk", "Pixel BreakRoom" })
        {
            GameObject source = GameObject.Find(n);
            Vector2 p = n == "Pixel Roulette" ? new Vector2(0, 155) :
                n == "Pixel CashDesk" ? new Vector2(-535, -265) :
                n == "Pixel PitDesk" ? new Vector2(535, -265) :
                n == "Pixel BreakRoom" ? new Vector2(-555, 225) : new Vector2(-475, -40);
            Sprite s = source != null ? source.GetComponent<SpriteRenderer>().sprite : null;
            Art("Title " + n, s, p, new Vector2(405, 205));
            if (n == "Pixel BlackjackV2") Art("BJ2", s, new Vector2(475, -40), new Vector2(405, 205));
        }
        cast = FindObjectsByType<PixelCharacterVisual>(FindObjectsSortMode.None);
        Array.Sort(cast, (left, right) => string.CompareOrdinal(left.name, right.name));
        PixelCharacterVisual boss = FindFirstObjectByType<CasinoPlayerController>()?.GetComponentInChildren<PixelCharacterVisual>();
        int bossIndex = Array.IndexOf(cast, boss);
        if (bossIndex > 0)
        {
            PixelCharacterVisual first = cast[0];
            cast[0] = boss;
            cast[bossIndex] = first;
        }
        actors = new RectTransform[5]; homes = new Vector2[5];
        actorImages = new Image[actors.Length];
        for (int i = 0; i < actors.Length; i++)
        {
            homes[i] = i == 0 ? new Vector2(0, -335) : new Vector2(i % 2 == 0 ? 485 : -485, i < 3 ? 32 : -130);
            Sprite character = cast.Length > 0 ? cast[i % cast.Length].TitleFrame(i) : null;
            actors[i] = Art("Title character " + i, character, homes[i], i == 0 ? new Vector2(140, 175) : new Vector2(65, 90));
            actorImages[i] = actors[i].GetComponent<Image>();
        }
        Image trim = CasinoMinigamePresentation.CreateImage("Marquee", transform, new Vector2(0, 325),
            new Vector2(930, 168), new Color(.41f, .07f, .24f)).GetComponent<Image>();
        CasinoHudStyle.Panel(trim, trim.color);
        logo = CasinoMinigamePresentation.CreateText(transform, "PIT PANIC logo", "PIT PANIC",
            new Vector2(0, 342), new Vector2(880, 100), 80, CasinoHudStyle.Gold).rectTransform;
        CasinoMinigamePresentation.CreateText(transform, "Tagline", "RUN THE FLOOR!",
            new Vector2(0, 275), new Vector2(650, 44), 25, new Color(1, .92f, .71f));
        lights = new Image[24];
        for (int i = 0; i < lights.Length; i++)
        {
            Vector2 p = new Vector2(-430 + i % 12 * 78, i < 12 ? 400 : 248);
            lights[i] = CasinoMinigamePresentation.CreateImage("Marquee bulb", transform, p,
                new Vector2(12, 12), CasinoHudStyle.Gold).GetComponent<Image>();
            CasinoPixelArt.SetSprite(lights[i], CasinoPixelArt.Ball, CasinoHudStyle.Gold);
        }
        Button("Start", "NEW GAME", new Vector2(0, 25), newGame);
        Button cont = Button("Continue", "CONTINUE", new Vector2(0, -45), continueGame);
        cont.interactable = canContinue;
        Button("Settings", "SETTINGS", new Vector2(0, -115), () => settings.SetActive(true));
        Button("Quit", "QUIT GAME", new Vector2(0, -185), () =>
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        });
        settings = CasinoMinigamePresentation.CreateImage("Settings panel", transform, Vector2.zero,
            new Vector2(650, 600), CasinoHudStyle.Ink, true);
        CasinoHudStyle.Panel(settings.GetComponent<Image>(), CasinoHudStyle.Ink);
        CasinoMinigamePresentation.CreateText(settings.transform, "Settings title", "MAKE YOURSELF AT HOME",
            new Vector2(0, 245), new Vector2(580, 50), 26, CasinoHudStyle.Gold);
        Button volume = Button("Volume", "", new Vector2(0, 180), () =>
        {
            float value = AudioListener.volume > 0 ? 0 : 1;
            AudioListener.volume = value;
            PlayerPrefs.SetInt("PitPanic.Muted", value == 0 ? 1 : 0); PlayerPrefs.Save();
        }, settings.transform);
        volume.onClick.AddListener(() => volume.GetComponentInChildren<Text>().text = AudioListener.volume > 0 ? "SOUND: ON" : "SOUND: OFF");
        AudioListener.volume = PlayerPrefs.GetInt("PitPanic.Muted", 0) == 1 ? 0 : 1;
        volume.GetComponentInChildren<Text>().text = AudioListener.volume > 0 ? "SOUND: ON" : "SOUND: OFF";
        AudioSlider("Music", 90, CasinoSoundscape.MusicVolume);
        AudioSlider("Ambience", 10, CasinoSoundscape.AmbienceVolume);
        AudioSlider("Effects", -70, CasinoSoundscape.EffectsVolume);
        Button("Fullscreen", "TOGGLE FULLSCREEN", new Vector2(0, -160),
            () => Screen.fullScreen = !Screen.fullScreen, settings.transform);
        Button("Back", "BACK", new Vector2(0, -235), () => settings.SetActive(false), settings.transform);
        settings.SetActive(false);
    }

    private void AudioSlider(string channel, float y, float volume)
    {
        CasinoMinigamePresentation.CreateText(settings.transform, channel + " label", channel.ToUpperInvariant(),
            new Vector2(0, y + 22), new Vector2(480, 30), 20, CasinoHudStyle.Gold);
        GameObject root = CasinoMinigamePresentation.CreateImage(channel + " volume", settings.transform,
            new Vector2(0, y - 10), new Vector2(420, 22), new Color(.25f, .2f, .3f), true);
        Slider slider = root.AddComponent<Slider>();
        GameObject fill = CasinoMinigamePresentation.CreateImage("Fill", root.transform, Vector2.zero, new Vector2(420, 22), CasinoHudStyle.Mint);
        RectTransform fillRect = (RectTransform)fill.transform;
        fillRect.anchorMin = Vector2.zero; fillRect.anchorMax = Vector2.one; fillRect.offsetMin = fillRect.offsetMax = Vector2.zero;
        GameObject handle = CasinoMinigamePresentation.CreateImage("Handle", root.transform, Vector2.zero, new Vector2(22, 30), CasinoHudStyle.Gold, true);
        slider.fillRect = fillRect;
        slider.handleRect = (RectTransform)handle.transform;
        slider.targetGraphic = handle.GetComponent<Image>();
        slider.value = volume;
        slider.onValueChanged.AddListener(value => CasinoSoundscape.SetVolume(channel, value));
    }

    private RectTransform Art(string name, Sprite sprite, Vector2 p, Vector2 size)
    {
        var root = CasinoMinigamePresentation.CreateImage(name, transform, p, size, Color.white);
        Image image = root.GetComponent<Image>();
        image.raycastTarget = false;
        if (sprite != null) { image.sprite = sprite; image.preserveAspect = true; }
        else image.enabled = false;
        return (RectTransform)root.transform;
    }

    private Button Button(string name, string text, Vector2 p, Action action, Transform parent = null)
    {
        var root = CasinoMinigamePresentation.CreateImage(name, parent != null ? parent : transform,
            p, new Vector2(360, 58), new Color(.45f, .10f, .28f), true);
        Button button = root.AddComponent<Button>();
        button.targetGraphic = root.GetComponent<Image>();
        CasinoHudStyle.Button(button);
        CasinoMinigamePresentation.CreateText(root.transform, "Label", text, Vector2.zero,
            new Vector2(340, 50), 26, CasinoHudStyle.Gold);
        button.onClick.AddListener(() => action());
        return button;
    }

    private void Update()
    {
        elapsed += Time.unscaledDeltaTime;
        if (logo == null) return;
        logo.localScale = Vector3.one * (1 + .012f * Mathf.Sin(elapsed * 2));
        for (int i = 0; i < lights.Length; i++)
            lights[i].color = Mathf.Sin(elapsed * 5 + i) > -.3f ? CasinoHudStyle.Gold : new Color(.56f, .26f, .18f);
        for (int i = 0; i < actors.Length; i++)
        {
            actors[i].anchoredPosition = homes[i] + Vector2.up * Mathf.Round(3 * Mathf.Sin(elapsed * 3 + i));
            actors[i].localRotation = Quaternion.Euler(0, 0, 2 * Mathf.Sin(elapsed * 2 + i));
            if (cast.Length > 0 && cast[i % cast.Length] != null)
                actorImages[i].sprite = cast[i % cast.Length].TitleFrame(elapsed + i);
        }
    }

    private void OnDisable()
    {
        CasinoSoundscape.ReleaseFocus(transform);
        if (gameCamera != null) gameCamera.cullingMask = savedCullingMask;
    }
}
