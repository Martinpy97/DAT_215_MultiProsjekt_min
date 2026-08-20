using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class EnemyChaseAI : MonoBehaviour
{
    [Header("Spiller")]
    [Tooltip("Kan stå tomt. AI-en finner Player-tag automatisk.")]
    [SerializeField] private Transform player;

    [Header("Oppdagelse")]
    [SerializeField] private float detectionRange = 8f;

    [Header("Bevegelse")]
    [SerializeField] private float moveSpeed = 2.5f;
    [SerializeField] private float rotationSpeed = 8f;

    [Header("Angrep")]
    [SerializeField] private float attackRange = 1.7f;
    [SerializeField] private float attackDamage = 1f;
    [SerializeField] private float attackCooldown = 1.2f;
    [SerializeField] private float attackWindup = 0.35f;

    [Header("Knockback på spilleren")]
    [SerializeField] private float playerKnockbackForce = 4f;

    private Rigidbody enemyRigidbody;
    private PlayerHealth playerHealth;

    private float nextAttackTime;
    private float movementPausedUntil;

    private bool isAttacking;
    private Coroutine attackRoutine;

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

        if (playerHealth != null && playerHealth.IsDead)
        {
            StopHorizontalMovement();
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

        if (distanceToPlayer <= attackRange)
        {
            StopHorizontalMovement();
            TryAttack();
        }
        else if (!isAttacking)
        {
            MoveTowardsPlayer(direction);
        }
    }

    private void FindPlayer()
    {
        GameObject playerObject =
            GameObject.FindGameObjectWithTag("Player");

        if (playerObject == null)
        {
            return;
        }

        player = playerObject.transform;
        playerHealth =
            playerObject.GetComponent<PlayerHealth>();
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

    private void TryAttack()
    {
        if (isAttacking)
        {
            return;
        }

        if (Time.time < nextAttackTime)
        {
            return;
        }

        attackRoutine =
            StartCoroutine(PerformAttack());
    }

    private IEnumerator PerformAttack()
    {
        isAttacking = true;
        StopHorizontalMovement();

        // Kort ventetid før slaget treffer.
        yield return new WaitForSeconds(attackWindup);

        if (player == null || playerHealth == null)
        {
            FinishAttack();
            yield break;
        }

        Vector3 directionToPlayer =
            player.position - transform.position;

        directionToPlayer.y = 0f;

        float distanceToPlayer =
            directionToPlayer.magnitude;

        // Spilleren kan rekke å gå unna under windup.
        if (distanceToPlayer <= attackRange + 0.25f)
        {
            Vector3 knockbackDirection =
                directionToPlayer.normalized;

            playerHealth.TakeDamage(
                attackDamage,
                knockbackDirection,
                playerKnockbackForce
            );
        }

        FinishAttack();
    }

    private void FinishAttack()
    {
        nextAttackTime =
            Time.time + attackCooldown;

        isAttacking = false;
        attackRoutine = null;
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

        // Avbryt angrepet hvis fienden treffes.
        if (attackRoutine != null)
        {
            StopCoroutine(attackRoutine);
            attackRoutine = null;
            isAttacking = false;

            nextAttackTime = Mathf.Max(
                nextAttackTime,
                movementPausedUntil
            );
        }
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
            attackRange
        );
    }
}