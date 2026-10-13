using UnityEngine;

[DisallowMultipleComponent]
public sealed class CameraFacingCanvas : MonoBehaviour
{
    [SerializeField] private Canvas targetCanvas;
    private Camera targetCamera;

    public void Configure(Canvas canvas)
    {
        targetCanvas = canvas;
    }

    private void LateUpdate()
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        if (targetCanvas == null || targetCamera == null)
        {
            return;
        }

        Vector3 towardCamera = targetCamera.transform.position - targetCanvas.transform.position;
        if (towardCamera.sqrMagnitude > 0.001f)
        {
            targetCanvas.transform.rotation = Quaternion.LookRotation(towardCamera, targetCamera.transform.up);
        }
    }
}
