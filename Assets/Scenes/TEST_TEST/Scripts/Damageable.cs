using UnityEngine;

public class Damageable : MonoBehaviour
{
    [Header("Liv")]
    [SerializeField] private float maxHealth = 3f;

    [Header("Knockback")]
    [SerializeField] private float upwardForce = 1f;
    [SerializeField] private float knockbackPause = 0.3f;

    [Header("Når livet når null")]
    [SerializeField] private bool destroyOnDeath = true;
    [SerializeField] private float destroyDelay = 0f;

    private float currentHealth;
    private Rigidbody enemyRigidbody;
    private EnemyChaseAI enemyAI;
    private bool isDead;

    private void Awake()
    {
        currentHealth = maxHealth;

        enemyRigidbody = GetComponent<Rigidbody>();
        enemyAI = GetComponent<EnemyChaseAI>();

        if (enemyRigidbody == null)
        {
            Debug.LogWarning(
                name +
                " mangler Rigidbody. Knockback vil ikke fungere.",
                this
            );
        }
    }

    public void TakeDamage(
        float damage,
        Vector3 knockbackDirection,
        float knockbackForce
    )
    {
        if (isDead)
        {
            return;
        }

        currentHealth -= damage;

        ApplyKnockback(
            knockbackDirection,
            knockbackForce
        );

        Debug.Log(
            name +
            " ble truffet. Liv: " +
            currentHealth,
            this
        );

        if (currentHealth <= 0f)
        {
            Die();
        }
    }

    private void ApplyKnockback(
        Vector3 direction,
        float force
    )
    {
        if (enemyRigidbody == null)
        {
            return;
        }

        if (enemyAI != null)
        {
            enemyAI.PauseMovement(
                knockbackPause
            );
        }

        direction.y = 0f;

        if (direction.sqrMagnitude < 0.01f)
        {
            return;
        }

        direction.Normalize();

        Vector3 knockback =
            direction * force +
            Vector3.up * upwardForce;

        enemyRigidbody.AddForce(
            knockback,
            ForceMode.Impulse
        );
    }

    private void Die()
    {
        if (isDead)
        {
            return;
        }

        isDead = true;

        if (enemyAI != null)
        {
            enemyAI.enabled = false;
        }

        Debug.Log(
            name + " ble beseiret.",
            this
        );

        if (destroyOnDeath)
        {
            Destroy(
                gameObject,
                destroyDelay
            );
        }
    }
}