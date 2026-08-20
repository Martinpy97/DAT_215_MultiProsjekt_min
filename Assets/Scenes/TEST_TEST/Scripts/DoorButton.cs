using UnityEngine;
using UnityEngine.InputSystem;

public class DoorButton : MonoBehaviour
{
    [Header("Kobling")]
    [Tooltip("Alle dørene som denne knappen skal kontrollere.")]
    [SerializeField] private DoorController[] doors;

    [Tooltip("Trapper som skal spawnes når knappen aktiveres.")]
    [SerializeField] private StairSpawner[] stairSpawners;

    [Tooltip("Plattformer som skal starte når knappen aktiveres.")]
    [SerializeField] private MovingPlatform[] movingPlatforms;

    [Header("Innstillinger")]
    [Tooltip("Når denne er på, kan knappen bare brukes én gang.")]
    [SerializeField] private bool oneUse = true;

    [Tooltip("Valgfri Renderer som skifter farge når knappen aktiveres.")]
    [SerializeField] private Renderer indicatorRenderer;
    [SerializeField] private Color inactiveColor = new Color(0.85f, 0.65f, 0.1f);
    [SerializeField] private Color activeColor = new Color(0.15f, 0.8f, 0.3f);

    private bool playerNearby;
    private bool hasBeenUsed;
    private Material indicatorMaterial;

    private void Awake()
    {
        Collider triggerCollider = GetComponent<Collider>();

        if (triggerCollider == null)
        {
            Debug.LogError("DoorButton trenger en Collider på samme objekt.", this);
        }
        else
        {
            triggerCollider.isTrigger = true;
        }

        Rigidbody buttonRigidbody = GetComponent<Rigidbody>();

        if (buttonRigidbody == null)
        {
            buttonRigidbody = gameObject.AddComponent<Rigidbody>();
        }

        buttonRigidbody.isKinematic = true;
        buttonRigidbody.useGravity = false;

        if (indicatorRenderer != null)
        {
            indicatorMaterial = indicatorRenderer.material;
            indicatorMaterial.color = inactiveColor;
        }
    }

    private void Update()
    {
        if (!playerNearby || (oneUse && hasBeenUsed))
        {
            return;
        }

        if (InteractWasPressed())
        {
            ActivateButton();
        }
    }

    private bool InteractWasPressed()
    {
        bool keyboardPressed = Keyboard.current != null &&
            Keyboard.current.eKey.wasPressedThisFrame;

        bool gamepadPressed = Gamepad.current != null &&
            Gamepad.current.buttonSouth.wasPressedThisFrame;

        return keyboardPressed || gamepadPressed;
    }

    private void ActivateButton()
    {
        bool hasDoors = doors != null && doors.Length > 0;
        bool hasStairs = stairSpawners != null && stairSpawners.Length > 0;
        bool hasPlatforms = movingPlatforms != null && movingPlatforms.Length > 0;

        if (!hasDoors && !hasStairs && !hasPlatforms)
        {
            Debug.LogError(
                "Ingen dører, trapper eller plattformer er koblet til DoorButton.",
                this
            );
            return;
        }

        if (hasDoors)
        {
            foreach (DoorController door in doors)
            {
                if (door == null)
                {
                    continue;
                }

                if (oneUse)
                {
                    door.OpenDoor();
                }
                else
                {
                    door.ToggleDoor();
                }
            }
        }

        if (hasStairs)
        {
            foreach (StairSpawner stairSpawner in stairSpawners)
            {
                if (stairSpawner != null)
                {
                    stairSpawner.SpawnStairs();
                }
            }
        }

        if (hasPlatforms)
        {
            foreach (MovingPlatform movingPlatform in movingPlatforms)
            {
                if (movingPlatform == null)
                {
                    continue;
                }

                if (oneUse)
                {
                    movingPlatform.StartPlatform();
                }
                else
                {
                    movingPlatform.TogglePlatform();
                }
            }
        }

        hasBeenUsed = oneUse;

        if (indicatorMaterial != null)
        {
            indicatorMaterial.color = activeColor;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (IsPlayer(other))
        {
            playerNearby = true;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (IsPlayer(other))
        {
            playerNearby = false;
        }
    }

    private bool IsPlayer(Collider other)
    {
        return other.CompareTag("Player") ||
            other.GetComponentInParent<CharacterController>() != null;
    }
}
