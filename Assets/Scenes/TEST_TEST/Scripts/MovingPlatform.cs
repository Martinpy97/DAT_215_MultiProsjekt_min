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
    [Tooltip("Tiden fra aktivering til plattformen begynner å bevege seg.")]
    [SerializeField, Min(0f)] private float startDelay = 3f;
    [SerializeField] private bool startAutomatically = true;
    [SerializeField] private bool loop = true;

    [Header("Plattformsekvens")]
    [Tooltip("Plattformer som starter når denne plattformen når Point B.")]
    [SerializeField] private MovingPlatform[] nextPlatforms;

    private Rigidbody platformRigidbody;
    private Transform currentTarget;
    private float waitTimer;
    private bool isMoving;
    private bool hasCompletedJourney;
    private bool isWaitingToStart;
    private float startDelayTimer;

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
        isMoving = false;

        if (startAutomatically)
        {
            StartPlatform();
        }
    }

    private void FixedUpdate()
    {
        if (isWaitingToStart)
        {
            startDelayTimer -= Time.fixedDeltaTime;

            if (startDelayTimer <= 0f)
            {
                isWaitingToStart = false;
                isMoving = true;
            }

            return;
        }

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
            StartNextPlatforms();

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

    private void StartNextPlatforms()
    {
        if (nextPlatforms == null)
        {
            return;
        }

        foreach (MovingPlatform nextPlatform in nextPlatforms)
        {
            if (nextPlatform != null)
            {
                nextPlatform.StartPlatform();
            }
        }
    }

    public void StartPlatform()
    {
        if (isMoving || isWaitingToStart)
        {
            return;
        }

        if (hasCompletedJourney && !loop)
        {
            return;
        }

        if (startDelay <= 0f)
        {
            isMoving = true;
        }
        else
        {
            startDelayTimer = startDelay;
            isWaitingToStart = true;
        }
    }

    public void StopPlatform()
    {
        isMoving = false;
        isWaitingToStart = false;
        startDelayTimer = 0f;
    }

    public void TogglePlatform()
    {
        if (isMoving || isWaitingToStart)
        {
            StopPlatform();
        }
        else
        {
            StartPlatform();
        }
    }
}
