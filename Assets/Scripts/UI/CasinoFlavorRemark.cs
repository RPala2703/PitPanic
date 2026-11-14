using UnityEngine;
using UnityEngine.UI;

public sealed class CasinoFlavorRemark : MonoBehaviour
{
    private Image portrait;
    private Text label;
    private float expires;
    private float nextAllowed;
    private CanvasGroup group;

    public void Configure(Transform parent)
    {
        GameObject panel = CasinoMinigamePresentation.CreateImage("Staff remark", parent,
            new Vector2(0, -135), new Vector2(620, 86), CasinoHudStyle.Ink);
        group = panel.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;
        portrait = CasinoMinigamePresentation.CreateImage("Remark portrait", panel.transform,
            new Vector2(-264, 0), new Vector2(80, 72), Color.white).GetComponent<Image>();
        portrait.preserveAspect = true;
        label = CasinoMinigamePresentation.CreateText(panel.transform, "Remark text", "",
            new Vector2(42, 0), new Vector2(505, 72), 18, Color.white);
        label.raycastTarget = false;
        group.alpha = 0;
    }

    public bool TryShow(DialogueSpeaker speaker, DialogueExpression expression, string text)
    {
        if (group == null || Time.unscaledTime < nextAllowed) return false;
        portrait.sprite = CasinoDialogue.Portrait(speaker, expression);
        label.text = speaker.ToString().ToUpperInvariant() + ": " + text;
        expires = Time.unscaledTime + 4f;
        nextAllowed = Time.unscaledTime + 25f;
        return true;
    }

    private void Update()
    {
        if (group == null) return;
        bool show = Time.timeScale > 0 && !AudioListener.pause && Time.unscaledTime < expires &&
                    !CasinoMinigamePresentation.IsOpen && !CasinoPlayerInteractionRouter.IsAnyInspectionOpen();
        group.alpha = Mathf.MoveTowards(group.alpha, show ? 1 : 0, Time.unscaledDeltaTime * 5);
    }
}
