using UnityEngine;

[RequireComponent(typeof(Camera))]
public class IsometricCameraFollow : MonoBehaviour
{
    [Header("Karakter")]
    [Tooltip("Objektet kameraet skal følge, for eksempel Hero.")]
    [SerializeField] private Transform target;
    [SerializeField] private Vector3 targetOffset = new Vector3(0f, 1f, 0f);

    [Header("Kameravinkel")]
    [SerializeField, Range(20f, 80f)] private float downwardAngle = 45f;
    [SerializeField, Range(-180f, 180f)] private float horizontalAngle = 45f;
    [SerializeField, Min(1f)] private float distance = 12f;

    [Header("Følging")]
    [SerializeField, Min(0.01f)] private float followSmoothTime = 0.15f;

    [Header("Isometrisk visning")]
    [Tooltip("Slå på for et flatere, mer isometrisk uttrykk.")]
    [SerializeField] private bool useOrthographic = true;
    [SerializeField, Min(1f)] private float orthographicSize = 6f;

    private Camera cameraComponent;
    private Vector3 followVelocity;

    private void Awake()
    {
        cameraComponent = GetComponent<Camera>();
        UpdateCameraProjection();
    }

    private void OnValidate()
    {
        Camera currentCamera = GetComponent<Camera>();

        if (currentCamera != null)
        {
            currentCamera.orthographic = useOrthographic;
            currentCamera.orthographicSize = orthographicSize;
        }
    }

    private void Start()
    {
        if (target == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");

            if (player != null)
            {
                target = player.transform;
            }
        }

        if (target != null)
        {
            MoveCameraImmediately();
        }
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            return;
        }

        Vector3 focusPoint = target.position + targetOffset;
        Quaternion cameraRotation = Quaternion.Euler(
            downwardAngle,
            horizontalAngle,
            0f
        );

        Vector3 desiredPosition =
            focusPoint - cameraRotation * Vector3.forward * distance;

        transform.position = Vector3.SmoothDamp(
            transform.position,
            desiredPosition,
            ref followVelocity,
            followSmoothTime
        );

        transform.rotation = cameraRotation;
    }

    private void MoveCameraImmediately()
    {
        Vector3 focusPoint = target.position + targetOffset;
        Quaternion cameraRotation = Quaternion.Euler(
            downwardAngle,
            horizontalAngle,
            0f
        );

        transform.position =
            focusPoint - cameraRotation * Vector3.forward * distance;
        transform.rotation = cameraRotation;
    }

    private void UpdateCameraProjection()
    {
        cameraComponent.orthographic = useOrthographic;
        cameraComponent.orthographicSize = orthographicSize;
    }
}
