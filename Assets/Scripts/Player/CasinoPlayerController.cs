using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public sealed class CasinoPlayerController : MonoBehaviour
{
    [SerializeField, Min(0f)] private float moveSpeed = 5f;
    [SerializeField] private Vector2 floorHalfExtents = new Vector2(15f, 10f);

    private CharacterController characterController;
    private bool movementEnabled = true;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
    }

    public void SetMovementEnabled(bool enabled)
    {
        movementEnabled = enabled;
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (!movementEnabled || keyboard == null)
        {
            characterController.SimpleMove(Vector3.zero);
            return;
        }

        Vector2 input = Vector2.zero;
        if (keyboard.aKey.isPressed) input.x -= 1f;
        if (keyboard.dKey.isPressed) input.x += 1f;
        if (keyboard.sKey.isPressed) input.y -= 1f;
        if (keyboard.wKey.isPressed) input.y += 1f;

        Vector3 movement = new Vector3(input.x, 0f, input.y).normalized;
        Vector3 nextPosition = transform.position + movement * moveSpeed * Time.deltaTime;
        nextPosition.x = Mathf.Clamp(nextPosition.x, -floorHalfExtents.x, floorHalfExtents.x);
        nextPosition.z = Mathf.Clamp(nextPosition.z, -floorHalfExtents.y, floorHalfExtents.y);
        Vector3 boundedMovement = Time.deltaTime > 0f ? (nextPosition - transform.position) / Time.deltaTime : Vector3.zero;
        characterController.SimpleMove(boundedMovement);
    }
}
