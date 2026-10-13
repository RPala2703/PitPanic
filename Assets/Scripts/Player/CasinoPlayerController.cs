using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public sealed class CasinoPlayerController : MonoBehaviour
{
    [SerializeField, Min(0f)] private float moveSpeed = 5f;

    private CharacterController characterController;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return;
        }

        Vector2 input = Vector2.zero;
        if (keyboard.aKey.isPressed) input.x -= 1f;
        if (keyboard.dKey.isPressed) input.x += 1f;
        if (keyboard.sKey.isPressed) input.y -= 1f;
        if (keyboard.wKey.isPressed) input.y += 1f;

        Vector3 movement = new Vector3(input.x, 0f, input.y).normalized;
        characterController.SimpleMove(movement * moveSpeed);
    }
}
