using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PlatformPassengerTrigger : MonoBehaviour
{
    [Header("Referanser")]
    [Tooltip("Objektet som faktisk beveger seg.")]
    [SerializeField] private Transform platform;

    [Tooltip("Trigger-collideren på PassengerTrigger-barnet.")]
    [SerializeField] private Collider passengerZone;

    [Header("Feilsøking")]
    [SerializeField] private bool showDebugMessages = true;

    private CharacterController passenger;
    private Vector3 previousPlatformPosition;

    private void Awake()
    {
        if (platform == null)
        {
            platform = transform;
        }

        if (passengerZone == null)
        {
            FindPassengerZone();
        }

        if (passengerZone == null)
        {
            Debug.LogError(
                "Finner ingen trigger-collider på plattformen.",
                this
            );
        }
        else
        {
            passengerZone.isTrigger = true;
        }

        previousPlatformPosition = platform.position;
    }

    private void FindPassengerZone()
    {
        Collider[] childColliders =
            GetComponentsInChildren<Collider>();

        foreach (Collider childCollider in childColliders)
        {
            if (childCollider.isTrigger)
            {
                passengerZone = childCollider;
                return;
            }
        }
    }

    private void LateUpdate()
    {
        if (platform == null)
        {
            return;
        }

        Vector3 currentPlatformPosition =
            platform.position;

        Vector3 platformMovement =
            currentPlatformPosition -
            previousPlatformPosition;

        if (
            passenger != null &&
            platformMovement.sqrMagnitude > 0f
        )
        {
            passenger.Move(platformMovement);
        }

        previousPlatformPosition =
            currentPlatformPosition;
    }

    private void OnTriggerEnter(Collider other)
    {
        DetectPassenger(other);
    }

    private void OnTriggerStay(Collider other)
    {
        DetectPassenger(other);
    }

    private void DetectPassenger(Collider other)
    {
        CharacterController controller =
            other.GetComponentInParent<CharacterController>();

        if (controller == null)
        {
            return;
        }

        if (passenger == controller)
        {
            return;
        }

        passenger = controller;
        previousPlatformPosition = platform.position;

        if (showDebugMessages)
        {
            Debug.Log(
                "MovingPlatform: Spilleren er registrert.",
                this
            );
        }
    }

    private void OnTriggerExit(Collider other)
    {
        CharacterController controller =
            other.GetComponentInParent<CharacterController>();

        if (
            controller == null ||
            controller != passenger
        )
        {
            return;
        }

        passenger = null;

        if (showDebugMessages)
        {
            Debug.Log(
                "MovingPlatform: Spilleren forlot plattformen.",
                this
            );
        }
    }

    private void OnDisable()
    {
        passenger = null;
    }
}