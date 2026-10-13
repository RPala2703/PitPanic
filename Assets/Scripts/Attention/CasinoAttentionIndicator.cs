using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class CasinoAttentionIndicator : MonoBehaviour
{
    [SerializeField] private CasinoTableAttention attention;
    [SerializeField] private Canvas indicatorCanvas;

    private Camera targetCamera;
    private Text indicatorText;

    public void Configure(CasinoTableAttention tableAttention, Canvas worldCanvas, Text text)
    {
        attention = tableAttention;
        indicatorCanvas = worldCanvas;
        indicatorText = text;
    }

    private void Start()
    {
        if (attention == null)
        {
            attention = GetComponent<CasinoTableAttention>();
        }

        if (indicatorCanvas == null)
        {
            indicatorCanvas = GetComponentInChildren<Canvas>(true);
        }

        if (indicatorText == null && indicatorCanvas != null)
        {
            indicatorText = indicatorCanvas.GetComponentInChildren<Text>(true);
        }

        if (attention == null || indicatorCanvas == null || indicatorText == null)
        {
            Debug.LogError($"{nameof(CasinoAttentionIndicator)} on {name} is missing its attention, canvas, or text reference.", this);
            enabled = false;
            return;
        }

        targetCamera = Camera.main;
        attention.AttentionChanged += SetVisible;
        SetVisible(attention.HasPendingTasks);
    }

    private void OnDestroy()
    {
        if (attention != null)
        {
            attention.AttentionChanged -= SetVisible;
        }
    }

    private void LateUpdate()
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        if (targetCamera != null)
        {
            Vector3 towardCamera = indicatorCanvas.transform.position - targetCamera.transform.position;
            if (towardCamera.sqrMagnitude > 0.001f)
            {
                indicatorCanvas.transform.rotation = Quaternion.LookRotation(towardCamera, Vector3.up);
            }
        }
    }

    private void SetVisible(bool visible)
    {
        if (indicatorCanvas != null)
        {
            indicatorCanvas.enabled = visible;
        }
    }
}
