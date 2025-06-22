using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerInput))]
public class PlayerMoveAround : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 5f;
    public float rotationSpeed = 720f;

    private Vector2 moveInput;

    void Update()
    {
        // Movement
        Vector3 moveDir = new Vector3(moveInput.x, moveInput.y, 0f);
        float inputMagnitude = Mathf.Clamp01(moveDir.magnitude);
        moveDir.Normalize();

        transform.position += moveDir * moveSpeed * inputMagnitude * Time.deltaTime;

        // Rotation
        if (moveDir.sqrMagnitude > 0.001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(Vector3.forward, moveDir);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }
    }

    // Called automatically via PlayerInput component
    /* public void OnMove(InputAction.CallbackContext context)
    {
        moveInput = context.ReadValue<Vector2>();
    } */
    public void OnMove(InputValue value)
    {
        moveInput = value.Get<Vector2>();
    }
}
