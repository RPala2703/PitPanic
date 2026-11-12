using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class CasinoSpeechBubble : MonoBehaviour
{
    private Canvas canvas;
    private Text label;
    private AIDealer dealer;
    private CasinoTableAttention nearbyTask;
    private Camera targetCamera;

    public string Message => canvas != null && canvas.enabled ? label.text : "";

    private void Start()
    {
        dealer = GetComponent<AIDealer>();
        GameObject bubble = new GameObject("Speech Bubble", typeof(RectTransform), typeof(Canvas));
        bubble.transform.SetParent(transform, false);
        canvas = bubble.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 80;
        RectTransform rect = bubble.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(dealer != null ? 110 : 330, 76);
        rect.localScale = Vector3.one * .012f;
        GameObject body = CasinoMinigamePresentation.CreateImage("Bubble", rect, Vector2.zero,
            rect.sizeDelta, new Color(.98f, .96f, .86f));
        body.GetComponent<Image>().raycastTarget = false;
        Outline outline = body.AddComponent<Outline>();
        outline.effectColor = CasinoHudStyle.Ink;
        outline.effectDistance = new Vector2(4, -4);
        GameObject tail = CasinoMinigamePresentation.CreateImage("Bubble Tail", rect,
            new Vector2(-24, -42), new Vector2(18, 18), new Color(.98f, .96f, .86f));
        tail.transform.localRotation = Quaternion.Euler(0, 0, 45);
        tail.GetComponent<Image>().raycastTarget = false;
        label = CasinoMinigamePresentation.CreateText(rect, "Speech", "", Vector2.zero,
            rect.sizeDelta - new Vector2(24, 16), dealer != null ? 32 : 21, CasinoHudStyle.Ink);
        canvas.enabled = false;
    }

    private void LateUpdate()
    {
        if (canvas == null) return;
        bool floorVisible = Time.timeScale > 0 && !AudioListener.pause &&
            !CasinoMinigamePresentation.IsOpen && !CasinoPlayerInteractionRouter.IsAnyInspectionOpen();
        string message = "";
        if (floorVisible)
        {
            if (dealer != null)
                message = dealer.IsResting || dealer.IsAtStation && dealer.Energy <= 0 ? "Zzz" : "";
            else
            {
                nearbyTask = FindNearbyTask();
                message = CasinoTaskFeedback.Message(nearbyTask);
            }
        }
        canvas.enabled = message.Length > 0;
        label.text = message;
        if (!canvas.enabled) return;
        if (targetCamera == null) targetCamera = Camera.main;
        if (targetCamera == null) return;
        canvas.transform.SetPositionAndRotation(transform.position + Vector3.up * 2.7f, targetCamera.transform.rotation);
    }

    private CasinoTableAttention FindNearbyTask()
    {
        CasinoTableAttention closest = nearbyTask != null && nearbyTask.HasVisibleTasks ? nearbyTask : null;
        float distance = closest != null
            ? Vector3.ProjectOnPlane(closest.transform.position - transform.position, Vector3.up).sqrMagnitude
            : 25f;
        if (distance >= 25f) { closest = null; distance = 25f; }
        foreach (CasinoTableAttention task in FindObjectsByType<CasinoTableAttention>(FindObjectsSortMode.None))
        {
            if (!task.HasVisibleTasks) continue;
            float candidate = Vector3.ProjectOnPlane(task.transform.position - transform.position, Vector3.up).sqrMagnitude;
            if (candidate + .25f >= distance) continue;
            distance = candidate;
            closest = task;
        }
        return closest;
    }
}
