using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class CasinoTitleScreen : MonoBehaviour
{
    public static readonly Vector2 ArtworkSize = new Vector2(1672, 941);
    public const float TransitionSeconds = 2.4f;
    private RectTransform artwork;
    private Image marquee;
    private Image[] windows;
    private Image glint;
    private Image warmLight;
    private RectTransform leftDoor;
    private RectTransform rightDoor;
    private Image penguinImage;
    private PixelCharacterVisual penguin;
    private CanvasGroup fade;
    private CanvasGroup remarkGroup;
    private CasinoDialogue remark;
    private GameObject settings;
    private Button[] menuButtons;
    private Camera gameCamera;
    private int savedCullingMask;
    private float elapsed;
    private float departure;
    private float nextStep;
    private float idleSeconds;
    private float nextRemark = 24;
    private float lineCompleteAt = -1;
    private int remarkIndex;
    private bool transitioning;
    private bool entering;
    private Action destination;
    private Vector2 departureStart;
    private Vector2 lastPointer;
    private float lastUpdateTime;
    public bool IsTransitioning => transitioning;

    public void Build(Action newGame, Action continueGame, bool canContinue)
    {
        CasinoSoundscape.SetMusicTrack(CasinoMusicTrack.Title);
        CasinoSoundscape.SetIntensity(0);
        CasinoSoundscape.SetFocus(transform);
        RectTransform rect = (RectTransform)transform;
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        GetComponent<Image>().color = new Color(.09f, .055f, .15f);
        gameCamera = Camera.main;
        if (gameCamera != null) { savedCullingMask = gameCamera.cullingMask; gameCamera.cullingMask = 0; }

        artwork = new GameObject("Casino exterior", typeof(RectTransform)).GetComponent<RectTransform>();
        artwork.SetParent(transform, false);
        artwork.sizeDelta = ArtworkSize;
        Art("Supplied background", Load("Background/Title_Casino_Exterior_1672x941"),
            ArtworkSize * .5f, ArtworkSize);
        marquee = Art("Marquee light overlay", Load("FX/Marquee_Light_Pulse_FullCanvas"),
            ArtworkSize * .5f, ArtworkSize);
        Sprite background = Load("Background/Title_Casino_Exterior_1672x941");
        windows = new Image[2];
        for (int i = 0; i < windows.Length; i++)
        {
            Rect crop = new Rect(i == 0 ? 234 : 1300, 941 - 635, 103, 97);
            Sprite section = Sprite.Create(background.texture, crop, new Vector2(.5f, .5f), 100);
            windows[i] = Art("Window light " + i, section,
                new Vector2(crop.center.x, 941 - crop.center.y), crop.size);
            windows[i].color = new Color(.72f, .58f, .49f, 0);
        }
        glint = Art("Occasional lamp glint", Load("FX/Sparkle_01_48px"),
            new Vector2(494, 183), new Vector2(18, 18));
        glint.color = new Color(1, 1, 1, 0);
        warmLight = Art("Warm doorway", Load("Door/Doorway_Warm_Reveal_FullCanvas"),
            ArtworkSize * .5f, ArtworkSize);
        warmLight.color = new Color(1, 1, 1, 0);
        leftDoor = Art("Left entrance door", Load("Door/Door_Left_Closed_Tight"),
            new Vector2(745.5f, 634), new Vector2(171, 304)).rectTransform;
        rightDoor = Art("Right entrance door", Load("Door/Door_Right_Closed_Tight"),
            new Vector2(918.5f, 634), new Vector2(173, 304)).rectTransform;
        leftDoor.pivot = new Vector2(0, .5f);
        leftDoor.anchoredPosition = ImagePoint(new Vector2(660, 634));
        rightDoor.pivot = new Vector2(1, .5f);
        rightDoor.anchoredPosition = ImagePoint(new Vector2(1005, 634));
        leftDoor.gameObject.SetActive(false);
        rightDoor.gameObject.SetActive(false);
        penguin = FindFirstObjectByType<CasinoPlayerController>()?.GetComponentInChildren<PixelCharacterVisual>();
        if (penguin == null) throw new InvalidOperationException("The title requires the existing playable Penguin.");
        penguinImage = Art("Title Penguin", penguin.TitleFrame(0), new Vector2(834, 800), new Vector2(120, 160));
        penguinImage.rectTransform.pivot = new Vector2(.5f, 0);
        penguinImage.rectTransform.anchoredPosition = ImagePoint(new Vector2(834, 800));

        // Keep the established object paths used by onboarding and accessibility tools.
        menuButtons = new[]
        {
            Button("Start", "NEW GAME", new Vector2(710, 853), () => BeginEntrance(newGame)),
            Button("Continue", "CONTINUE", new Vector2(962, 853), () => BeginEntrance(continueGame)),
            Button("Settings", "SETTINGS", new Vector2(710, 913), OpenSettings),
            Button("Quit", "QUIT GAME", new Vector2(962, 913), Quit)
        };
        menuButtons[1].interactable = canContinue;
        foreach (Button button in menuButtons)
        {
            // Buttons remain direct children of the menu; their bounds follow the artwork in FitArtwork.
            button.gameObject.AddComponent<CanvasGroup>();
        }
        SetNavigation(menuButtons);
        BuildRemark();
        BuildSettings();
        fade = Art("Entrance fade", null, ArtworkSize * .5f, ArtworkSize).gameObject.AddComponent<CanvasGroup>();
        fade.GetComponent<Image>().color = new Color(.09f, .055f, .15f);
        fade.alpha = 0;
        fade.blocksRaycasts = false;
        ResetArrival();
        FitArtwork();
    }

    private static Sprite Load(string path)
    {
        Sprite sprite = Resources.Load<Sprite>("TitleScreen/" + path);
        if (sprite == null) throw new InvalidOperationException("Missing title screen asset: " + path);
        return sprite;
    }

    private static Vector2 ImagePoint(Vector2 point) => new Vector2(point.x - ArtworkSize.x * .5f, ArtworkSize.y * .5f - point.y);
    public static float FitScale(Vector2 available) => Mathf.Min(available.x / ArtworkSize.x, available.y / ArtworkSize.y);

    private Image Art(string name, Sprite sprite, Vector2 point, Vector2 size)
    {
        GameObject root = CasinoMinigamePresentation.CreateImage(name, artwork, ImagePoint(point), size, Color.white);
        Image image = root.GetComponent<Image>();
        image.sprite = sprite;
        image.raycastTarget = false;
        return image;
    }

    private void FitArtwork()
    {
        float scale = FitScale(((RectTransform)transform).rect.size);
        artwork.localScale = Vector3.one * scale;
        Vector2[] positions = { new Vector2(710, 853), new Vector2(962, 853), new Vector2(710, 913), new Vector2(962, 913) };
        for (int i = 0; i < menuButtons.Length; i++)
        {
            RectTransform rect = (RectTransform)menuButtons[i].transform;
            rect.anchoredPosition = ImagePoint(positions[i]) * scale;
            rect.localScale = Vector3.one * scale;
        }
    }

    private static void SetNavigation(Button[] buttons)
    {
        for (int i = 0; i < buttons.Length; i++)
        {
            Navigation nav = buttons[i].navigation;
            nav.mode = Navigation.Mode.Explicit;
            nav.selectOnLeft = nav.selectOnRight = buttons[i ^ 1];
            nav.selectOnUp = nav.selectOnDown = buttons[(i + 2) % 4];
            buttons[i].navigation = nav;
        }
    }

    private void BuildRemark()
    {
        Image panel = Art("Idle staff dialogue", null, new Vector2(305, 838), new Vector2(490, 132));
        CasinoHudStyle.Panel(panel, CasinoHudStyle.Ink);
        remarkGroup = panel.gameObject.AddComponent<CanvasGroup>();
        remarkGroup.alpha = 0;
        remarkGroup.blocksRaycasts = false;
        remarkGroup.interactable = false;
        Image portrait = CasinoMinigamePresentation.CreateImage("Speaker portrait", panel.transform,
            new Vector2(-181, 0), new Vector2(112, 112), Color.white).GetComponent<Image>();
        portrait.preserveAspect = true;
        Text name = CasinoMinigamePresentation.CreateText(panel.transform, "Speaker", "", new Vector2(56, 40),
            new Vector2(334, 30), 19, CasinoHudStyle.Gold);
        Text text = CasinoMinigamePresentation.CreateText(panel.transform, "Remark", "", new Vector2(56, -12),
            new Vector2(334, 77), 20, Color.white);
        Text indicator = CasinoMinigamePresentation.CreateText(panel.transform, "Automatic advance", "",
            Vector2.zero, Vector2.zero, 1, Color.white);
        foreach (Graphic graphic in panel.GetComponentsInChildren<Graphic>()) graphic.raycastTarget = false;
        remark = panel.gameObject.AddComponent<CasinoDialogue>();
        remark.Configure(panel.rectTransform, portrait, name, text, indicator);
        remark.VoiceGain = .12f;
    }

    public static DialogueLine[][] IdleConversations() => new[]
    {
        new[] { new DialogueLine(DialogueSpeaker.Bear, DialogueExpression.Neutral, "The building is insured. Allegedly.") },
        new[] { new DialogueLine(DialogueSpeaker.Pig, DialogueExpression.Shocked, "Those lights aren't free.") },
        new[] { new DialogueLine(DialogueSpeaker.Fox, DialogueExpression.Smug, "The building looks better from outside.") },
        new[] {
            new DialogueLine(DialogueSpeaker.Tiger, DialogueExpression.Happy, "Welcome. Everything is almost under control."),
            new DialogueLine(DialogueSpeaker.Penguin, DialogueExpression.Angry, "Almost?", .25f),
            new DialogueLine(DialogueSpeaker.Tiger, DialogueExpression.Smug, "It's a useful word.", .3f) }
    };

    private void StopRemark()
    {
        if (remark == null) return;
        remark.Stop();
        remarkGroup.alpha = 0;
        lineCompleteAt = -1;
    }

    private void UpdateRemarks(bool input)
    {
        idleSeconds = input ? 0 : idleSeconds + Time.unscaledDeltaTime;
        if (remarkGroup.alpha > 0)
        {
            if (!remark.IsComplete) lineCompleteAt = -1;
            else if (lineCompleteAt < 0) lineCompleteAt = Time.unscaledTime;
            else if (Time.unscaledTime - lineCompleteAt > 3.2f)
            {
                remark.Advance();
                lineCompleteAt = -1;
            }
        }
        else if (idleSeconds >= 18 && elapsed >= nextRemark)
        {
            DialogueLine[][] conversations = IdleConversations();
            remarkGroup.alpha = 1;
            remark.Begin(conversations[remarkIndex++ % conversations.Length], StopRemark);
            nextRemark = elapsed + 42;
        }
    }

    private void OpenSettings()
    {
        if (transitioning) return;
        StopRemark();
        idleSeconds = 0;
        CasinoSoundscape.Play(CasinoSoundCue.Button, Vector3.zero, transform, .25f);
        settings.SetActive(true);
        foreach (Button button in menuButtons) button.interactable = false;
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(settings.transform.Find("Volume").gameObject);
    }

    private void CloseSettings()
    {
        settings.SetActive(false);
        RestoreButtons();
        idleSeconds = 0;
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(menuButtons[2].gameObject);
    }

    private void RestoreButtons()
    {
        foreach (Button button in menuButtons) button.interactable = true;
        menuButtons[1].interactable = canResume;
    }
    private bool canResume;

    private void Quit()
    {
        if (transitioning) return;
        StopRemark();
        CasinoSoundscape.Play(CasinoSoundCue.Button, Vector3.zero, transform, .25f);
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void BeginEntrance(Action next)
    {
        if (transitioning || settings.activeSelf) return;
        CasinoSoundscape.StopMusic();
        StopRemark();
        transitioning = true;
        destination = next;
        departure = 0;
        entering = false;
        departureStart = penguinImage.rectTransform.anchoredPosition;
        foreach (Button button in menuButtons) button.interactable = false;
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        CasinoSoundscape.Play(CasinoSoundCue.Button, Vector3.zero, transform, .25f);
    }

    private void UpdateEntrance(float delta)
    {
        departure += delta;
        SetButtonAlpha(1 - Mathf.Clamp01(departure / .3f));
        penguinImage.sprite = penguin.TitlePoseFrame(departure < .6f ? CasinoAnimationPose.Idle : CasinoAnimationPose.Walk, 1, departure);
        if (departure >= .4f && !entering)
        {
            entering = true;
            warmLight.color = Color.white;
            leftDoor.gameObject.SetActive(true);
            rightDoor.gameObject.SetActive(true);
            CasinoSoundscape.Play(CasinoSoundCue.Door, Vector3.zero, transform);
        }
        float opening = Mathf.SmoothStep(0, 1, Mathf.Clamp01((departure - .4f) / .65f));
        leftDoor.localScale = rightDoor.localScale = new Vector3(1 - .88f * opening, 1, 1);
        float walk = Mathf.SmoothStep(0, 1, Mathf.Clamp01((departure - .85f) / .95f));
        penguinImage.rectTransform.anchoredPosition = Vector2.Lerp(departureStart, ImagePoint(new Vector2(834, 686)), walk);
        penguinImage.color = new Color(1, 1, 1, 1 - Mathf.Clamp01((departure - 1.45f) / .45f));
        if (departure > .85f && departure < 1.7f) Footstep();
        float opacity = Mathf.Clamp01((departure - 1.75f) / .65f);
        if (fade.alpha == 0 && opacity > 0)
            CasinoSoundscape.Play(CasinoSoundCue.Entrance, Vector3.zero, transform, .3f);
        fade.alpha = opacity;
        if (departure >= TransitionSeconds)
        {
            Action callback = destination;
            destination = null;
            callback?.Invoke();
        }
    }

    private void Footstep()
    {
        if (Time.unscaledTime < nextStep) return;
        nextStep = Time.unscaledTime + .45f;
        CasinoSoundscape.Play(CasinoSoundCue.Step, Vector3.zero, transform, .65f);
    }

    private void SetButtonAlpha(float value)
    {
        foreach (Button button in menuButtons) button.GetComponent<CanvasGroup>().alpha = value;
    }

    private void ResetArrival()
    {
        elapsed = 0;
        lastUpdateTime = Time.realtimeSinceStartup;
        departure = 0;
        idleSeconds = 0;
        nextRemark = 24;
        nextStep = 0;
        transitioning = false;
        entering = false;
        destination = null;
        canResume = menuButtons[1].interactable || canResume;
        RestoreButtons();
        SetButtonAlpha(1);
        fade.alpha = 0;
        warmLight.color = new Color(1, 1, 1, 0);
        leftDoor.localScale = rightDoor.localScale = Vector3.one;
        leftDoor.gameObject.SetActive(false); rightDoor.gameObject.SetActive(false);
        penguinImage.color = Color.white;
        penguinImage.rectTransform.anchoredPosition = ImagePoint(new Vector2(834, 800));
        penguinImage.sprite = penguin.TitleFrame(0);
        StopRemark();
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(menuButtons[0].gameObject);
    }

    private void Update()
    {
        if (artwork == null) return;
        float now = Time.realtimeSinceStartup;
        float delta = now - lastUpdateTime;
        lastUpdateTime = now;
        elapsed += delta;
        FitArtwork();
        marquee.color = new Color(1, 1, 1, .035f + .025f * (.5f + .5f * Mathf.Sin(elapsed * 1.1f)));
        for (int i = 0; i < windows.Length; i++)
            windows[i].color = new Color(.72f, .58f, .49f, .018f + .018f * (.5f + .5f * Mathf.Sin(elapsed * .42f + i * 2)));
        float glintPhase = elapsed % 47f;
        glint.color = new Color(1, 1, 1, glintPhase > 34 && glintPhase < 34.9f ? Mathf.Sin((glintPhase - 34) / .9f * Mathf.PI) * .2f : 0);
        if (transitioning) { UpdateEntrance(delta); return; }
        penguinImage.sprite = penguin.TitleFrame(elapsed);
        bool input = Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame;
        if (Mouse.current != null)
        {
            Vector2 pointer = Mouse.current.position.ReadValue();
            input |= (pointer - lastPointer).sqrMagnitude > 4 || Mouse.current.leftButton.wasPressedThisFrame;
            lastPointer = pointer;
        }
        if (Gamepad.current != null)
            input |= Gamepad.current.buttonSouth.wasPressedThisFrame || Gamepad.current.dpad.ReadValue().sqrMagnitude > 0;
        if (settings.activeSelf)
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) CloseSettings();
            return;
        }
        UpdateRemarks(input);
    }

    private void BuildSettings()
    {
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
        Button("Back", "BACK", new Vector2(0, -235), CloseSettings, settings.transform);
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

    private Button Button(string name, string text, Vector2 point, Action action, Transform parent = null)
    {
        var root = CasinoMinigamePresentation.CreateImage(name, parent != null ? parent : transform,
            parent != null ? point : ImagePoint(point), parent != null ? new Vector2(360, 50) : new Vector2(230, 48),
            new Color(.45f, .10f, .28f), true);
        Button button = root.AddComponent<Button>();
        button.targetGraphic = root.GetComponent<Image>();
        CasinoHudStyle.Button(button);
        CasinoMinigamePresentation.CreateText(root.transform, "Label", text, Vector2.zero,
            parent != null ? new Vector2(340, 44) : new Vector2(220, 42), 23, CasinoHudStyle.Gold);
        button.onClick.AddListener(() => action());
        return button;
    }

    private void OnEnable()
    {
        if (gameCamera == null) return;
        gameCamera.cullingMask = 0;
        CasinoSoundscape.SetMusicTrack(CasinoMusicTrack.Title);
        CasinoSoundscape.SetFocus(transform);
        CasinoSoundscape.SetIntensity(0);
        if (artwork != null) ResetArrival();
    }

    private void OnDisable()
    {
        StopRemark();
        destination = null;
        CasinoSoundscape.ReleaseFocus(transform);
        if (gameCamera != null) gameCamera.cullingMask = savedCullingMask;
    }

    private void OnDestroy()
    {
        if (windows == null) return;
        foreach (Image window in windows) if (window != null && window.sprite != null) Destroy(window.sprite);
    }
}
