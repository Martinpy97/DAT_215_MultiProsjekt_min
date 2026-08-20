using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class MovingPlatform : MonoBehaviour
{
    [Header("Punkter")]
    [SerializeField] private Transform pointA;
    [SerializeField] private Transform pointB;

    [Header("Bevegelse")]
    [SerializeField, Min(0.01f)] private float speed = 2f;
    [SerializeField, Min(0f)] private float waitTime = 1f;
    [SerializeField] private bool startAutomatically = true;
    [SerializeField] private bool loop = true;

    private Rigidbody platformRigidbody;
    private Transform currentTarget;
    private float waitTimer;
    private bool isMoving;
    private bool hasCompletedJourney;

    private void Awake()
    {
        platformRigidbody = GetComponent<Rigidbody>();
        platformRigidbody.isKinematic = true;
        platformRigidbody.useGravity = false;
        platformRigidbody.interpolation = RigidbodyInterpolation.Interpolate;
        platformRigidbody.collisionDetectionMode =
            CollisionDetectionMode.ContinuousSpeculative;
    }

    private void Start()
    {
        if (pointA == null || pointB == null)
        {
            Debug.LogError("MovingPlatform mangler Point A eller Point B.", this);
            enabled = false;
            return;
        }

        platformRigidbody.position = pointA.position;
        currentTarget = pointB;
        isMoving = startAutomatically;
    }

    private void FixedUpdate()
    {
        if (!isMoving || currentTarget == null)
        {
            return;
        }

        if (waitTimer > 0f)
        {
            waitTimer -= Time.fixedDeltaTime;
            return;
        }

        Vector3 nextPosition = Vector3.MoveTowards(
            platformRigidbody.position,
            currentTarget.position,
            speed * Time.fixedDeltaTime
        );

        platformRigidbody.MovePosition(nextPosition);

        if (Vector3.Distance(nextPosition, currentTarget.position) <= 0.001f)
        {
            ArriveAtTarget();
        }
    }

    private void ArriveAtTarget()
    {
        waitTimer = waitTime;

        if (currentTarget == pointB)
        {
            if (!loop)
            {
                isMoving = false;
                hasCompletedJourney = true;
                return;
            }

            currentTarget = pointA;
        }
        else
        {
            currentTarget = pointB;
        }
    }

    public void StartPlatform()
    {
        if (!hasCompletedJourney || loop)
        {
            isMoving = true;
        }
    }

    public void StopPlatform()
    {
        isMoving = false;
    }

    public void TogglePlatform()
    {
        if (isMoving)
        {
            StopPlatform();
        }
        else
        {
            StartPlatform();
        }
    }
}
