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

        AlignTo(targetCamera);
    }

    public void AlignTo(Camera camera)
    {
        if (targetCanvas != null && camera != null)
        {
            targetCanvas.worldCamera = camera;
            targetCanvas.transform.rotation = camera.transform.rotation;
        }
    }

    public bool IsReadableFrom(Camera camera)
    {
        if (camera == null || targetCanvas == null)
        {
            return false;
        }

        Transform label = targetCanvas.transform;
        return Vector3.Dot(label.right, camera.transform.right) > 0.99f &&
               Vector3.Dot(label.up, camera.transform.up) > 0.99f;
    }
}
