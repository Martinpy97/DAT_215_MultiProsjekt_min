using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class EnemyChaseAI : MonoBehaviour
{
    [Header("Spiller")]
    [Tooltip("Kan stå tomt. AI-en finner automatisk objektet med Player-tag.")]
    [SerializeField] private Transform player;

    [Header("Oppdagelse")]
    [SerializeField] private float detectionRange = 8f;
    [SerializeField] private float stoppingDistance = 1.5f;

    [Header("Bevegelse")]
    [SerializeField] private float moveSpeed = 2.5f;
    [SerializeField] private float rotationSpeed = 8f;

    private Rigidbody enemyRigidbody;
    private float movementPausedUntil;

    private void Awake()
    {
        enemyRigidbody = GetComponent<Rigidbody>();
    }

    private void Start()
    {
        FindPlayer();
    }

    private void FixedUpdate()
    {
        if (player == null)
        {
            FindPlayer();
            return;
        }

        if (Time.time < movementPausedUntil)
        {
            return;
        }

        Vector3 directionToPlayer =
            player.position - enemyRigidbody.position;

        directionToPlayer.y = 0f;

        float distanceToPlayer =
            directionToPlayer.magnitude;

        if (distanceToPlayer > detectionRange)
        {
            return;
        }

        if (distanceToPlayer < 0.01f)
        {
            return;
        }

        Vector3 direction =
            directionToPlayer.normalized;

        RotateTowardsPlayer(direction);

        if (distanceToPlayer > stoppingDistance)
        {
            MoveTowardsPlayer(direction);
        }
        else
        {
            StopHorizontalMovement();
        }
    }

    private void FindPlayer()
    {
        GameObject playerObject =
            GameObject.FindGameObjectWithTag("Player");

        if (playerObject != null)
        {
            player = playerObject.transform;
        }
    }

    private void RotateTowardsPlayer(Vector3 direction)
    {
        Quaternion targetRotation =
            Quaternion.LookRotation(
                direction,
                Vector3.up
            );

        Quaternion newRotation =
            Quaternion.Slerp(
                enemyRigidbody.rotation,
                targetRotation,
                rotationSpeed * Time.fixedDeltaTime
            );

        enemyRigidbody.MoveRotation(newRotation);
    }

    private void MoveTowardsPlayer(Vector3 direction)
    {
        StopHorizontalMovement();

        Vector3 movement =
            direction *
            moveSpeed *
            Time.fixedDeltaTime;

        enemyRigidbody.MovePosition(
            enemyRigidbody.position + movement
        );
    }

    private void StopHorizontalMovement()
    {
        Vector3 velocity =
            enemyRigidbody.linearVelocity;

        velocity.x = 0f;
        velocity.z = 0f;

        enemyRigidbody.linearVelocity = velocity;
    }

    public void PauseMovement(float duration)
    {
        movementPausedUntil = Mathf.Max(
            movementPausedUntil,
            Time.time + duration
        );
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(
            transform.position,
            detectionRange
        );

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(
            transform.position,
            stoppingDistance
        );
    }
}