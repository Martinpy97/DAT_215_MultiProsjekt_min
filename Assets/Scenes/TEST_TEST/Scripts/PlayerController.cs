using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("Referanser")]
    [Tooltip("Bruker Main Camera automatisk hvis feltet er tomt.")]
    [SerializeField] private Transform cameraTransform;

    [Header("Bevegelse")]
    [SerializeField] private float walkSpeed = 5f;
    [SerializeField] private float runSpeed = 8f;
    [SerializeField] private float rotationSpeed = 12f;

    [Header("Hopp og gravitasjon")]
    [SerializeField] private float jumpHeight = 1.5f;
    [SerializeField] private float gravity = -20f;
    [SerializeField] private float groundedForce = -2f;

    private CharacterController characterController;
    private float verticalVelocity;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();

        if (cameraTransform == null && Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
        }
    }

    private void Update()
    {
        MoveCharacter();
    }

    private void MoveCharacter()
    {
        Vector2 movementInput = ReadMovementInput();

        Vector3 inputDirection = new Vector3(
            movementInput.x,
            0f,
            movementInput.y
        ).normalized;

        Vector3 moveDirection =
            GetCameraRelativeDirection(inputDirection);

        // Roter karakteren i bevegelsesretningen
        if (moveDirection.sqrMagnitude > 0.01f)
        {
            Quaternion targetRotation =
                Quaternion.LookRotation(moveDirection);

            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                rotationSpeed * Time.deltaTime
            );
        }

        // Hopp og gravitasjon
        if (characterController.isGrounded)
        {
            verticalVelocity = groundedForce;

            if (JumpWasPressed())
            {
                verticalVelocity =
                    Mathf.Sqrt(jumpHeight * -2f * gravity);
            }
        }
        else
        {
            verticalVelocity += gravity * Time.deltaTime;
        }

        float currentSpeed =
            RunIsPressed() ? runSpeed : walkSpeed;

        Vector3 velocity = moveDirection * currentSpeed;
        velocity.y = verticalVelocity;

        characterController.Move(velocity * Time.deltaTime);
    }

    private Vector2 ReadMovementInput()
    {
        Vector2 input = Vector2.zero;

        // Tastatur
        if (Keyboard.current != null)
        {
            if (
                Keyboard.current.aKey.isPressed ||
                Keyboard.current.leftArrowKey.isPressed
            )
            {
                input.x -= 1f;
            }

            if (
                Keyboard.current.dKey.isPressed ||
                Keyboard.current.rightArrowKey.isPressed
            )
            {
                input.x += 1f;
            }

            if (
                Keyboard.current.sKey.isPressed ||
                Keyboard.current.downArrowKey.isPressed
            )
            {
                input.y -= 1f;
            }

            if (
                Keyboard.current.wKey.isPressed ||
                Keyboard.current.upArrowKey.isPressed
            )
            {
                input.y += 1f;
            }
        }

        // Gamepad
        if (Gamepad.current != null)
        {
            input += Gamepad.current.leftStick.ReadValue();
        }

        return Vector2.ClampMagnitude(input, 1f);
    }

    private bool JumpWasPressed()
    {
        bool keyboardJump =
            Keyboard.current != null &&
            Keyboard.current.spaceKey.wasPressedThisFrame;

        bool gamepadJump =
            Gamepad.current != null &&
            Gamepad.current.buttonSouth.wasPressedThisFrame;

        return keyboardJump || gamepadJump;
    }

    private bool RunIsPressed()
    {
        bool keyboardRun =
            Keyboard.current != null &&
            (
                Keyboard.current.leftShiftKey.isPressed ||
                Keyboard.current.rightShiftKey.isPressed
            );

        bool gamepadRun =
            Gamepad.current != null &&
            Gamepad.current.leftStickButton.isPressed;

        return keyboardRun || gamepadRun;
    }

    private Vector3 GetCameraRelativeDirection(
        Vector3 inputDirection
    )
    {
        if (cameraTransform == null)
        {
            return inputDirection;
        }

        Vector3 cameraForward = cameraTransform.forward;
        Vector3 cameraRight = cameraTransform.right;

        cameraForward.y = 0f;
        cameraRight.y = 0f;

        cameraForward.Normalize();
        cameraRight.Normalize();

        return (
            cameraForward * inputDirection.z +
            cameraRight * inputDirection.x
        ).normalized;
    }
}