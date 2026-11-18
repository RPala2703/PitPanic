using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Optional Employee Handbook overlay opened from the main menu and level select.
/// Content comes from <see cref="CasinoHandbookContent"/>; unlock/read state from <see cref="CasinoHandbookProgress"/>.
/// </summary>
[DisallowMultipleComponent]
public sealed class CasinoEmployeeHandbook : MonoBehaviour
{
    private static readonly HandbookCategory[] Categories =
        { HandbookCategory.Staff, HandbookCategory.Guests, HandbookCategory.Equipment, HandbookCategory.HouseRules,
          HandbookCategory.Archives };
    private static readonly Color TabIdle = new Color(.30f, .09f, .22f);
    private static readonly Color TabSelected = new Color(.62f, .18f, .36f);
    private const float ImageBox = 128f;

    private readonly Dictionary<HandbookCategory, List<HandbookEntry>> byCategory =
        new Dictionary<HandbookCategory, List<HandbookEntry>>();
    private readonly Dictionary<HandbookCategory, int> selectedIndex = new Dictionary<HandbookCategory, int>();
    private readonly List<GameObject> launcherBadges = new List<GameObject>();
    private Button[] tabs;
    private GameObject[] tabBadges;
    private Image entryImage;
    private Text entryTitle;
    private Text entryDescription;
    private Text entryCounter;
    private GameObject entryBadge;
    private Button previous;
    private Button next;
    private Button close;
    private Action onClosed;

    public bool IsOpen => gameObject.activeSelf;
    public HandbookCategory CurrentCategory { get; private set; }
    public int CurrentIndex => selectedIndex[CurrentCategory];
    public HandbookEntry CurrentEntry => byCategory[CurrentCategory].Count > 0 ? byCategory[CurrentCategory][CurrentIndex] : null;
    public bool CurrentEntryShowsNew => entryBadge.activeSelf;
    public bool CurrentEntryLocked { get; private set; }
    public string CurrentTitleText => entryTitle.text;
    public string CurrentDescriptionText => entryDescription.text;
    public Sprite CurrentImage => entryImage.sprite;
    public bool CategoryShowsNew(HandbookCategory category) => tabBadges[Array.IndexOf(Categories, category)].activeSelf;
    public int EntryCount(HandbookCategory category) => byCategory[category].Count;

    public static CasinoEmployeeHandbook Create(Transform canvas)
    {
        CasinoHandbookProgress.Sync();
        GameObject shade = CasinoMinigamePresentation.CreateImage("Employee Handbook", canvas, Vector2.zero,
            new Vector2(1600, 900), new Color(0, 0, 0, .72f), true);
        RectTransform rect = (RectTransform)shade.transform;
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        CasinoEmployeeHandbook handbook = shade.AddComponent<CasinoEmployeeHandbook>();
        handbook.Build();
        shade.SetActive(false);
        return handbook;
    }

    private void Build()
    {
        foreach (HandbookCategory category in Categories)
        {
            byCategory[category] = new List<HandbookEntry>();
            selectedIndex[category] = 0;
        }
        foreach (HandbookEntry entry in CasinoHandbookContent.Load().entries)
            if (entry != null && !string.IsNullOrEmpty(entry.id)) byCategory[entry.category].Add(entry);

        GameObject card = CasinoMinigamePresentation.CreateImage("Handbook card", transform, Vector2.zero,
            new Vector2(1240, 760), CasinoHudStyle.Ink, true);
        CasinoHudStyle.Panel(card.GetComponent<Image>(), CasinoHudStyle.Ink);
        Transform root = card.transform;
        CasinoMinigamePresentation.CreateText(root, "Handbook title", "EMPLOYEE HANDBOOK", new Vector2(0, 325),
            new Vector2(900, 50), 34, CasinoHudStyle.Gold);

        tabs = new Button[Categories.Length];
        tabBadges = new GameObject[Categories.Length];
        for (int i = 0; i < Categories.Length; i++)
        {
            HandbookCategory category = Categories[i];
            tabs[i] = MakeButton(root, "Tab " + category, CasinoHandbookContent.CategoryLabel(category),
                new Vector2((i - (Categories.Length - 1) * .5f) * 234, 250),
                new Vector2(218, 52), () => SelectCategory(category));
            tabBadges[i] = Badge(tabs[i].transform, new Vector2(80, 26));
        }

        GameObject frame = CasinoMinigamePresentation.CreateImage("Entry picture frame", root, new Vector2(515, 115),
            new Vector2(ImageBox + 20, ImageBox + 20), new Color(.05f, .03f, .08f, 1));
        CasinoHudStyle.Panel(frame.GetComponent<Image>(), new Color(.05f, .03f, .08f, 1));
        entryImage = CasinoMinigamePresentation.CreateImage("Entry image", frame.transform, Vector2.zero,
            new Vector2(ImageBox, ImageBox), Color.white).GetComponent<Image>();
        entryBadge = Badge(frame.transform, new Vector2(0, ImageBox * .5f + 10));

        entryTitle = CasinoMinigamePresentation.CreateText(root, "Entry title", "", new Vector2(0, 150),
            new Vector2(820, 60), 38, CasinoHudStyle.Gold, TextAnchor.MiddleCenter);
        entryDescription = CasinoMinigamePresentation.CreateText(root, "Entry description", "", new Vector2(0, -20),
            new Vector2(820, 260), 26, Color.white, TextAnchor.UpperCenter);
        entryCounter = CasinoMinigamePresentation.CreateText(root, "Entry counter", "", new Vector2(0, -215),
            new Vector2(820, 34), 19, new Color(.85f, .78f, .9f), TextAnchor.MiddleCenter);

        previous = MakeButton(root, "Previous entry", "PREV", new Vector2(-330, -315), new Vector2(240, 52), Previous);
        close = MakeButton(root, "Close handbook", "CLOSE", new Vector2(0, -315), new Vector2(240, 52), Close);
        next = MakeButton(root, "Next entry", "NEXT", new Vector2(330, -315), new Vector2(240, 52), Next);
    }

    private void ConfigureNavigation(bool canPage)
    {
        Button[] bottom = { previous, close, next };
        for (int i = 0; i < tabs.Length; i++)
        {
            Navigation nav = new Navigation { mode = Navigation.Mode.Explicit };
            nav.selectOnLeft = tabs[(i + tabs.Length - 1) % tabs.Length];
            nav.selectOnRight = tabs[(i + 1) % tabs.Length];
            nav.selectOnDown = !canPage || i == 2 ? close : i < 2 ? previous : next;
            tabs[i].navigation = nav;
        }
        for (int i = 0; i < bottom.Length; i++)
        {
            Navigation nav = new Navigation { mode = Navigation.Mode.Explicit };
            nav.selectOnLeft = bottom[(i + bottom.Length - 1) % bottom.Length];
            nav.selectOnRight = bottom[(i + 1) % bottom.Length];
            nav.selectOnUp = tabs[Array.IndexOf(Categories, CurrentCategory)];
            if (!canPage) nav.selectOnLeft = nav.selectOnRight = null;
            bottom[i].navigation = nav;
        }
    }

    /// <summary>Adds a NEW marker to a menu button that opens the handbook.</summary>
    public void RegisterLauncher(Button launcher)
    {
        RectTransform rect = (RectTransform)launcher.transform;
        launcherBadges.Add(Badge(launcher.transform, new Vector2(rect.sizeDelta.x * .5f - 22, rect.sizeDelta.y * .5f)));
        RefreshLaunchers();
    }

    public void RefreshLaunchers()
    {
        CasinoHandbookProgress.Sync();
        bool unread = CasinoHandbookProgress.AnyUnread();
        launcherBadges.RemoveAll(badge => badge == null);
        foreach (GameObject badge in launcherBadges) badge.SetActive(unread);
    }

    public void Open(Action closed)
    {
        CasinoHandbookProgress.Sync();
        onClosed = closed;
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        HandbookCategory first = HandbookCategory.Staff;
        foreach (HandbookCategory category in Categories)
            if (CasinoHandbookProgress.AnyUnread(category)) { first = category; break; }
        CasinoSoundscape.Play(CasinoSoundCue.Button, Vector3.zero, transform, .25f);
        ShowCategory(first);
        Select(tabs[Array.IndexOf(Categories, first)]);
    }

    public void Close()
    {
        if (!IsOpen) return;
        CasinoSoundscape.Play(CasinoSoundCue.Button, Vector3.zero, transform, .25f);
        gameObject.SetActive(false);
        RefreshLaunchers();
        Action callback = onClosed;
        onClosed = null;
        callback?.Invoke();
    }

    public void SelectCategory(HandbookCategory category)
    {
        CasinoSoundscape.Play(CasinoSoundCue.Button, Vector3.zero, transform, .2f);
        ShowCategory(category);
    }

    public void Next() => Step(1);
    public void Previous() => Step(-1);

    private void Step(int direction)
    {
        int count = byCategory[CurrentCategory].Count;
        if (count == 0) return;
        selectedIndex[CurrentCategory] = (CurrentIndex + direction + count) % count;
        CasinoSoundscape.Play(CasinoSoundCue.Button, Vector3.zero, transform, .2f);
        ShowEntry();
    }

    private void ShowCategory(HandbookCategory category)
    {
        CurrentCategory = category;
        for (int i = 0; i < tabs.Length; i++)
        {
            Image image = (Image)tabs[i].targetGraphic;
            image.color = Categories[i] == category ? TabSelected : TabIdle;
        }
        ShowEntry();
    }

    private void ShowEntry()
    {
        HandbookEntry entry = CurrentEntry;
        int count = byCategory[CurrentCategory].Count;
        previous.interactable = next.interactable = count > 1;
        ConfigureNavigation(count > 1);
        if (entry == null)
        {
            CurrentEntryLocked = true;
            entryImage.enabled = false;
            entryBadge.SetActive(false);
            entryTitle.text = "NO ENTRIES YET";
            entryDescription.text = "";
            entryCounter.text = "";
            RefreshBadges();
            return;
        }
        bool unlocked = CasinoHandbookProgress.IsUnlocked(entry);
        bool unread = CasinoHandbookProgress.IsUnread(entry);
        CurrentEntryLocked = !unlocked;
        SetImage(entry.DisplayImage, unlocked);
        entryTitle.text = unlocked ? entry.title : "LOCKED";
        entryDescription.text = unlocked ? entry.description : CasinoHandbookProgress.LockedHint(entry);
        entryCounter.text = $"{CasinoHandbookContent.CategoryLabel(CurrentCategory)}  •  ENTRY {CurrentIndex + 1} / {count}";
        entryBadge.SetActive(unread);
        if (unread) CasinoHandbookProgress.MarkRead(entry);
        RefreshBadges();
    }

    private void SetImage(Sprite sprite, bool unlocked)
    {
        entryImage.enabled = sprite != null;
        entryImage.sprite = sprite;
        if (sprite == null) return;
        // Whole-number scaling keeps the existing pixel art crisp; larger concept art is fitted down.
        Vector2 size = sprite.rect.size;
        float fit = Mathf.Min(ImageBox / size.x, ImageBox / size.y);
        float scale = fit >= 1 ? Mathf.Floor(fit) : fit;
        entryImage.rectTransform.sizeDelta = size * scale;
        entryImage.color = unlocked ? Color.white : new Color(0, 0, 0, .85f);
    }

    private void RefreshBadges()
    {
        for (int i = 0; i < Categories.Length; i++)
            tabBadges[i].SetActive(CasinoHandbookProgress.AnyUnread(Categories[i]));
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) Close();
        else if (Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame) Close();
    }

    private static void Select(Selectable selectable)
    {
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(selectable.gameObject);
    }

    private static GameObject Badge(Transform parent, Vector2 position)
    {
        GameObject badge = CasinoMinigamePresentation.CreateImage("NEW badge", parent, position, new Vector2(62, 26),
            CasinoHudStyle.Coral);
        CasinoHudStyle.Panel(badge.GetComponent<Image>(), CasinoHudStyle.Coral);
        CasinoMinigamePresentation.CreateText(badge.transform, "Label", "NEW", Vector2.zero, new Vector2(58, 24), 15,
            Color.white);
        return badge;
    }

    private static Button MakeButton(Transform parent, string name, string label, Vector2 position, Vector2 size,
        Action action)
    {
        GameObject root = CasinoMinigamePresentation.CreateImage(name, parent, position, size, new Color(.45f, .10f, .28f), true);
        Button button = root.AddComponent<Button>();
        button.targetGraphic = root.GetComponent<Image>();
        CasinoHudStyle.Button(button);
        CasinoMinigamePresentation.CreateText(root.transform, "Label", label, Vector2.zero, size - new Vector2(10, 6), 22,
            CasinoHudStyle.Gold);
        button.onClick.AddListener(() => action());
        return button;
    }
}
