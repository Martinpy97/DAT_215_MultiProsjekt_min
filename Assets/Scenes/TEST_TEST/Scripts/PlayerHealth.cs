using System;
using System.Collections;
using UnityEngine;

public class PlayerHealth : MonoBehaviour
{
    public event Action<float, float> HealthChanged;
    public event Action<float> Damaged;
    public event Action Died;
    public event Action Respawned;

    [Header("Liv")]
    [SerializeField] private float maxHealth = 5f;
    [SerializeField] private float currentHealth;

    [Header("Treff")]
    [SerializeField] private float invulnerabilityDuration = 0.7f;

    [Header("Knockback")]
    [SerializeField] private float upwardForce = 1f;
    [SerializeField] private float knockbackDecay = 12f;

    [Header("Respawn")]
    [Tooltip("Hvis feltet er tomt brukes spillerens startposisjon.")]
    [SerializeField] private Transform respawnPoint;

    [SerializeField] private float respawnDelay = 1.5f;

    private CharacterController characterController;
    private PlayerController playerController;

    private Vector3 startingPosition;
    private Quaternion startingRotation;
    private Vector3 knockbackVelocity;

    private float invulnerableUntil;
    private bool isDead;

    public bool IsDead => isDead;
    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;

    private void Awake()
    {
        characterController =
            GetComponent<CharacterController>();

        playerController =
            GetComponent<PlayerController>();

        startingPosition = transform.position;
        startingRotation = transform.rotation;

        maxHealth = Mathf.Max(1f, maxHealth);
        currentHealth = maxHealth;
    }

    private void Update()
    {
        if (isDead)
        {
            return;
        }

        ApplyKnockbackMovement();
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

        if (Time.time < invulnerableUntil)
        {
            return;
        }

        float previousHealth = currentHealth;

        currentHealth = Mathf.Max(
            currentHealth - Mathf.Max(0f, damage),
            0f
        );

        float damageTaken = previousHealth - currentHealth;

        if (damageTaken <= 0f)
        {
            return;
        }

        invulnerableUntil =
            Time.time + invulnerabilityDuration;

        ApplyKnockback(
            knockbackDirection,
            knockbackForce
        );

        HealthChanged?.Invoke(currentHealth, maxHealth);
        Damaged?.Invoke(damageTaken);

        Debug.Log(
            "Spilleren ble truffet. Liv: " +
            currentHealth +
            " / " +
            maxHealth,
            this
        );

        if (currentHealth <= 0f)
        {
            StartCoroutine(DieAndRespawn());
        }
    }

    public void Heal(float amount)
    {
        if (isDead || amount <= 0f)
        {
            return;
        }

        float previousHealth = currentHealth;

        currentHealth = Mathf.Min(
            currentHealth + amount,
            maxHealth
        );

        if (!Mathf.Approximately(previousHealth, currentHealth))
        {
            HealthChanged?.Invoke(currentHealth, maxHealth);
        }
    }

    private void ApplyKnockback(
        Vector3 direction,
        float force
    )
    {
        direction.y = 0f;

        if (direction.sqrMagnitude > 0.01f)
        {
            direction.Normalize();
        }

        knockbackVelocity =
            direction * force +
            Vector3.up * upwardForce;
    }

    private void ApplyKnockbackMovement()
    {
        if (characterController == null)
        {
            return;
        }

        if (knockbackVelocity.sqrMagnitude < 0.01f)
        {
            knockbackVelocity = Vector3.zero;
            return;
        }

        characterController.Move(
            knockbackVelocity * Time.deltaTime
        );

        knockbackVelocity = Vector3.MoveTowards(
            knockbackVelocity,
            Vector3.zero,
            knockbackDecay * Time.deltaTime
        );
    }

    private IEnumerator DieAndRespawn()
    {
        isDead = true;
        knockbackVelocity = Vector3.zero;

        Died?.Invoke();

        Debug.Log(
            "Spilleren døde. Respawner...",
            this
        );

        if (playerController != null)
        {
            playerController.enabled = false;
        }

        yield return new WaitForSeconds(respawnDelay);

        RespawnPlayer();
    }

    private void RespawnPlayer()
    {
        Vector3 respawnPosition =
            respawnPoint != null
                ? respawnPoint.position
                : startingPosition;

        Quaternion respawnRotation =
            respawnPoint != null
                ? respawnPoint.rotation
                : startingRotation;

        if (characterController != null)
        {
            characterController.enabled = false;
        }

        transform.SetPositionAndRotation(
            respawnPosition,
            respawnRotation
        );

        if (characterController != null)
        {
            characterController.enabled = true;
        }

        currentHealth = maxHealth;
        knockbackVelocity = Vector3.zero;

        HealthChanged?.Invoke(currentHealth, maxHealth);

        invulnerableUntil =
            Time.time + invulnerabilityDuration;

        isDead = false;

        if (playerController != null)
        {
            playerController.enabled = true;
        }

        Respawned?.Invoke();

        Debug.Log(
            "Spilleren har respawnet.",
            this
        );
    }

    private void OnValidate()
    {
        maxHealth = Mathf.Max(1f, maxHealth);
        invulnerabilityDuration = Mathf.Max(0f, invulnerabilityDuration);
        respawnDelay = Mathf.Max(0f, respawnDelay);
    }
}
