using UnityEngine;

// URP's sprite shader needs an explicit per-renderer texture in the 3D renderer.
[ExecuteAlways]
[DefaultExecutionOrder(1000)]
[RequireComponent(typeof(SpriteRenderer))]
public sealed class PixelSpriteTextureBinding : MonoBehaviour
{
    private SpriteRenderer spriteRenderer;
    private Texture boundTexture;
    private MaterialPropertyBlock block;

    private void OnEnable()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        boundTexture = null;
        UpdateTexture();
    }

    private void LateUpdate()
    {
        UpdateTexture();
    }

    private void UpdateTexture()
    {
        if (spriteRenderer == null || spriteRenderer.sprite == null)
            return;

        Texture texture = spriteRenderer.sprite.texture;
        if (texture == boundTexture)
            return;

        if (block == null)
            block = new MaterialPropertyBlock();
        spriteRenderer.GetPropertyBlock(block);
        block.SetTexture("_MainTex", texture);
        spriteRenderer.SetPropertyBlock(block);
        boundTexture = texture;
    }
}
