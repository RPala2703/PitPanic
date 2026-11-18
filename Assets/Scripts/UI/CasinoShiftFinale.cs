using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class CasinoShiftFinale : MonoBehaviour
{
    public const float HoldSeconds = 2f;
    public const float FadeSeconds = .45f;
    private CanvasGroup group;
    private Image shade;
    private Text label;
    private int lastNumber;
    private float numberAge;
    private float endAge;
    private bool timeUp;
    private bool revealed;
    private Action revealResults;
    private Camera sceneCamera;
    private Vector3 cameraPosition;
    private bool savedNavigationEvents;
    private bool navigationBlocked;

    public int DisplayedNumber => lastNumber;
    public bool IsTimeUp => timeUp;
    public string Message => label != null ? label.text : "";

    public void Build()
    {
        Canvas overlay = CasinoMinigameHud.Create("Shift finale overlay", 1000);
        overlay.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1600, 900);
        overlay.transform.SetParent(transform, false);
        group = overlay.gameObject.AddComponent<CanvasGroup>();
        group.interactable = false;
        group.blocksRaycasts = false;
        shade = CasinoMinigamePresentation.CreateImage("Finale shade", overlay.transform,
            Vector2.zero, Vector2.zero, new Color(.12f, .025f, .06f, 0), false).GetComponent<Image>();
        RectTransform rect = shade.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        label = CasinoMinigamePresentation.CreateText(overlay.transform, "Final countdown", "",
            Vector2.zero, new Vector2(1450, 240), 150, CasinoHudStyle.Gold);
        label.raycastTarget = false;
        label.resizeTextForBestFit = false;
        Shadow shadow = label.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(.12f, .015f, .045f, 1);
        shadow.effectDistance = new Vector2(5, -6);
        ResetPresentation();
    }

    public void ResetPresentation()
    {
        RestoreCamera();
        RestoreNavigation();
        lastNumber = 0;
        timeUp = revealed = false;
        revealResults = null;
        group.alpha = 0;
        group.blocksRaycasts = false;
        shade.raycastTarget = false;
        label.text = "";
    }

    public void Tick(float remaining)
    {
        if (timeUp || remaining <= 0 || remaining > 10) return;
        int number = Mathf.CeilToInt(remaining);
        if (number == lastNumber) return;
        lastNumber = number;
        numberAge = 0;
        label.text = number.ToString();
        label.fontSize = number <= 3 ? 174 : 150;
        label.color = number <= 3 ? new Color(1, .63f, .29f) : CasinoHudStyle.Gold;
        group.alpha = 1;
        CasinoSoundscape.PlayShiftFinale(false, number <= 3);
    }

    public void ShowTimeUp(Action showResults)
    {
        if (timeUp) return;
        timeUp = true;
        endAge = 0;
        revealResults = showResults ?? throw new ArgumentNullException(nameof(showResults));
        label.text = "TIME'S UP!";
        label.fontSize = 116;
        label.color = CasinoHudStyle.Gold;
        label.rectTransform.localScale = Vector3.one * 1.12f;
        group.alpha = 1;
        group.blocksRaycasts = true;
        shade.raycastTarget = true;
        if (EventSystem.current != null)
        {
            savedNavigationEvents = EventSystem.current.sendNavigationEvents;
            navigationBlocked = true;
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.sendNavigationEvents = false;
        }
        sceneCamera = Camera.main;
        if (sceneCamera != null) cameraPosition = sceneCamera.transform.localPosition;
        CasinoSoundscape.StopMusic();
        CasinoSoundscape.SetFocus(transform);
        CasinoSoundscape.PlayShiftFinale(true, false);
    }

    private void Update()
    {
        if (label == null) return;
        if (!timeUp)
        {
            if (lastNumber == 0 || Time.timeScale == 0) return;
            numberAge += Time.unscaledDeltaTime;
            float pop = Mathf.Clamp01(numberAge / .18f);
            float scale = pop < .55f ? Mathf.Lerp(.78f, 1.12f, pop / .55f) :
                Mathf.Lerp(1.12f, 1, (pop - .55f) / .45f);
            label.rectTransform.localScale = Vector3.one * scale;
            group.alpha = 1 - Mathf.Clamp01((numberAge - .62f) / .28f);
            return;
        }

        endAge += Time.unscaledDeltaTime;
        label.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.12f, 1, Mathf.Clamp01(endAge / .2f));
        if (sceneCamera != null && endAge < .3f)
        {
            float strength = .055f * (1 - endAge / .3f);
            sceneCamera.transform.localPosition = cameraPosition +
                new Vector3(Mathf.Sin(endAge * 93), Mathf.Sin(endAge * 121), 0) * strength;
        }
        else RestoreCamera();
        if (!revealed)
        {
            shade.color = new Color(.12f, .025f, .06f, .08f);
            if (endAge < HoldSeconds) return;
            revealed = true;
            revealResults();
            revealResults = null;
            RestoreNavigation();
        }
        group.alpha = 1 - Mathf.Clamp01((endAge - HoldSeconds) / FadeSeconds);
        if (group.alpha == 0)
        {
            group.blocksRaycasts = false;
            shade.raycastTarget = false;
        }
    }

    private void RestoreCamera()
    {
        if (sceneCamera == null) return;
        sceneCamera.transform.localPosition = cameraPosition;
        if (!timeUp || endAge >= .3f) sceneCamera = null;
    }

    private void RestoreNavigation()
    {
        if (!navigationBlocked) return;
        if (EventSystem.current != null)
            EventSystem.current.sendNavigationEvents = savedNavigationEvents;
        navigationBlocked = false;
    }

    private void OnDestroy()
    {
        RestoreCamera();
        RestoreNavigation();
    }
}
