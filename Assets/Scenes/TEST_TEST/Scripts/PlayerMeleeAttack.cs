using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMeleeAttack : MonoBehaviour
{
    [Header("Referanser")]
    [Tooltip("Objektet som roteres under angrepet.")]
    [SerializeField] private Transform weaponPivot;

    [Tooltip("Tomt objekt ytterst på våpenet.")]
    [SerializeField] private Transform attackPoint;

    [Header("Angrep")]
    [SerializeField] private float damage = 1f;
    [SerializeField] private float attackRadius = 0.6f;
    [SerializeField] private float swipeAngle = 120f;
    [SerializeField] private float swipeDuration = 0.25f;
    [SerializeField] private float cooldown = 0.35f;

    [Header("Knockback")]
    [SerializeField] private float knockbackForce = 6f;

    [Header("Treff")]
    [Tooltip("Lagene som kan treffes, for eksempel Enemy.")]
    [SerializeField] private LayerMask targetLayers;

    private readonly HashSet<Damageable> hitTargets = new();

    private Quaternion restingRotation;
    private float nextAttackTime;
    private bool isAttacking;

    private void Awake()
    {
        if (weaponPivot == null)
        {
            Debug.LogError(
                "PlayerMeleeAttack mangler Weapon Pivot.",
                this
            );

            enabled = false;
            return;
        }

        if (attackPoint == null)
        {
            Debug.LogError(
                "PlayerMeleeAttack mangler Attack Point.",
                this
            );

            enabled = false;
            return;
        }

        restingRotation = weaponPivot.localRotation;
    }

    private void Update()
    {
        if (isAttacking || Time.time < nextAttackTime)
        {
            return;
        }

        if (AttackWasPressed())
        {
            StartCoroutine(PerformSwipe());
        }
    }

    private bool AttackWasPressed()
    {
        bool mousePressed =
            Mouse.current != null &&
            Mouse.current.leftButton.wasPressedThisFrame;

        bool gamepadPressed =
            Gamepad.current != null &&
            Gamepad.current.rightShoulder.wasPressedThisFrame;

        return mousePressed || gamepadPressed;
    }

    private IEnumerator PerformSwipe()
    {
        isAttacking = true;
        hitTargets.Clear();

        Quaternion startRotation =
            restingRotation *
            Quaternion.Euler(
                0f,
                -swipeAngle * 0.5f,
                0f
            );

        Quaternion endRotation =
            restingRotation *
            Quaternion.Euler(
                0f,
                swipeAngle * 0.5f,
                0f
            );

        float elapsedTime = 0f;
        float duration = Mathf.Max(
            0.01f,
            swipeDuration
        );

        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;

            float progress = Mathf.Clamp01(
                elapsedTime / duration
            );

            float smoothProgress =
                Mathf.SmoothStep(
                    0f,
                    1f,
                    progress
                );

            weaponPivot.localRotation =
                Quaternion.Slerp(
                    startRotation,
                    endRotation,
                    smoothProgress
                );

            CheckForHits();

            yield return null;
        }

        CheckForHits();

        weaponPivot.localRotation = restingRotation;
        nextAttackTime = Time.time + cooldown;
        isAttacking = false;
    }

    private void CheckForHits()
    {
        Collider[] hits = Physics.OverlapSphere(
            attackPoint.position,
            attackRadius,
            targetLayers,
            QueryTriggerInteraction.Ignore
        );

        foreach (Collider hit in hits)
        {
            Damageable damageable =
                hit.GetComponentInParent<Damageable>();

            if (damageable == null)
            {
                continue;
            }

            if (hitTargets.Contains(damageable))
            {
                continue;
            }

            hitTargets.Add(damageable);

            Vector3 knockbackDirection =
                damageable.transform.position -
                transform.position;

            knockbackDirection.y = 0f;

            if (knockbackDirection.sqrMagnitude < 0.01f)
            {
                knockbackDirection = transform.forward;
            }

            damageable.TakeDamage(
                damage,
                knockbackDirection,
                knockbackForce
            );
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (attackPoint == null)
        {
            return;
        }

        Gizmos.color = Color.red;

        Gizmos.DrawWireSphere(
            attackPoint.position,
            attackRadius
        );
    }
}