using System.Collections;
using UnityEngine;

public class PlayerHealth : MonoBehaviour
{
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

        currentHealth -= damage;
        currentHealth = Mathf.Max(currentHealth, 0f);

        invulnerableUntil =
            Time.time + invulnerabilityDuration;

        ApplyKnockback(
            knockbackDirection,
            knockbackForce
        );

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

        invulnerableUntil =
            Time.time + invulnerabilityDuration;

        isDead = false;

        if (playerController != null)
        {
            playerController.enabled = true;
        }

        Debug.Log(
            "Spilleren har respawnet.",
            this
        );
    }
}