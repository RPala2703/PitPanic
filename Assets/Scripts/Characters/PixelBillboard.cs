using UnityEngine;

[ExecuteAlways]
public sealed class PixelBillboard : MonoBehaviour
{
    private void LateUpdate()
    {
        Camera camera = Camera.main;
        if (camera != null)
            transform.rotation = camera.transform.rotation;
    }
}
