using UnityEngine;
using UnityEngine.UI;

public class PlayerHealthHUD : MonoBehaviour
{
    [Header("Spiller")]
    [SerializeField] private PlayerHealth playerHealth;

    [Header("HUD-referanser")]
    [SerializeField] private RectTransform panel;
    [SerializeField] private RectTransform healthFill;
    [SerializeField] private RectTransform damageLagFill;
    [SerializeField] private Image healthFillImage;
    [SerializeField] private Text healthText;
    [SerializeField] private Text statusText;
    [SerializeField] private CanvasGroup criticalGlow;
    [SerializeField] private CanvasGroup damageVignette;

    [Header("Animasjon")]
    [SerializeField] private float healthAnimationSpeed = 16f;
    [SerializeField] private float damageLagDelay = 0.3f;
    [SerializeField] private float damageLagSpeed = 4.5f;
    [SerializeField] private float shakeDecay = 55f;

    private readonly Color healthyColor =
        new Color(0.12f, 0.82f, 0.55f, 1f);

    private readonly Color warningColor =
        new Color(0.96f, 0.66f, 0.12f, 1f);

    private readonly Color criticalColor =
        new Color(0.92f, 0.12f, 0.16f, 1f);

    private Vector2 restingPanelPosition;
    private float targetHealth;
    private float displayedHealth;
    private float lagHealth;
    private float maxHealth = 1f;
    private float lagStartsAt;
    private float vignetteAlpha;
    private float shakeStrength;
    private bool subscribed;

    public void SetTarget(PlayerHealth target)
    {
        if (playerHealth == target)
        {
            return;
        }

        Unsubscribe();
        playerHealth = target;

        if (isActiveAndEnabled)
        {
            Subscribe();
        }
    }

    public void Configure(
        RectTransform panelReference,
        RectTransform healthFillReference,
        RectTransform damageLagReference,
        Image healthFillImageReference,
        Text healthTextReference,
        Text statusTextReference,
        CanvasGroup criticalGlowReference,
        CanvasGroup damageVignetteReference
    )
    {
        panel = panelReference;
        healthFill = healthFillReference;
        damageLagFill = damageLagReference;
        healthFillImage = healthFillImageReference;
        healthText = healthTextReference;
        statusText = statusTextReference;
        criticalGlow = criticalGlowReference;
        damageVignette = damageVignetteReference;
    }

    private void Awake()
    {
        if (panel != null)
        {
            restingPanelPosition = panel.anchoredPosition;
        }

        if (criticalGlow != null)
        {
            criticalGlow.alpha = 0f;
        }

        if (damageVignette != null)
        {
            damageVignette.alpha = 0f;
        }
    }

    private void OnEnable()
    {
        Subscribe();
    }

    private void Start()
    {
        if (!subscribed)
        {
            Subscribe();
        }

        RefreshFromPlayer();
    }

    private void RefreshFromPlayer()
    {
        if (playerHealth == null)
        {
            return;
        }

        maxHealth = Mathf.Max(1f, playerHealth.MaxHealth);
        targetHealth = playerHealth.CurrentHealth;
        displayedHealth = targetHealth;
        lagHealth = targetHealth;
        UpdateStatus();
        UpdateVisuals();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void Update()
    {
        AnimateHealth();
        AnimateDamageFeedback();
        AnimateCriticalState();
    }

    private void Subscribe()
    {
        if (!Application.isPlaying || subscribed)
        {
            return;
        }

        if (playerHealth == null)
        {
            playerHealth = FindAnyObjectByType<PlayerHealth>();
        }

        if (playerHealth == null)
        {
            Debug.LogWarning(
                "Player HUD fant ingen PlayerHealth i scenen.",
                this
            );
            return;
        }

        playerHealth.HealthChanged += HandleHealthChanged;
        playerHealth.Damaged += HandleDamaged;
        playerHealth.Died += HandleDeath;
        playerHealth.Respawned += HandleRespawn;
        subscribed = true;

        maxHealth = Mathf.Max(1f, playerHealth.MaxHealth);
        targetHealth = playerHealth.CurrentHealth;
        displayedHealth = targetHealth;
        lagHealth = targetHealth;
        statusText.text = string.Empty;
        UpdateVisuals();
    }

    private void Unsubscribe()
    {
        if (!subscribed || playerHealth == null)
        {
            subscribed = false;
            return;
        }

        playerHealth.HealthChanged -= HandleHealthChanged;
        playerHealth.Damaged -= HandleDamaged;
        playerHealth.Died -= HandleDeath;
        playerHealth.Respawned -= HandleRespawn;
        subscribed = false;
    }

    private void HandleHealthChanged(float current, float maximum)
    {
        bool wasHealing = current > targetHealth;

        maxHealth = Mathf.Max(1f, maximum);
        targetHealth = Mathf.Clamp(current, 0f, maxHealth);
        lagStartsAt = Time.unscaledTime + damageLagDelay;

        if (wasHealing)
        {
            lagHealth = targetHealth;
        }

        UpdateStatus();
        UpdateHealthText();
    }

    private void HandleDamaged(float amount)
    {
        vignetteAlpha = Mathf.Clamp01(0.45f + amount * 0.08f);
        shakeStrength = Mathf.Clamp(6f + amount * 2f, 6f, 14f);
    }

    private void HandleDeath()
    {
        statusText.text = "LIVSKRAFT TOM  •  RESPAWNER";
    }

    private void HandleRespawn()
    {
        vignetteAlpha = 0f;
        shakeStrength = 0f;
        UpdateStatus();
    }

    private void AnimateHealth()
    {
        displayedHealth = Mathf.MoveTowards(
            displayedHealth,
            targetHealth,
            healthAnimationSpeed * Time.unscaledDeltaTime
        );

        if (Time.unscaledTime >= lagStartsAt)
        {
            lagHealth = Mathf.MoveTowards(
                lagHealth,
                targetHealth,
                damageLagSpeed * Time.unscaledDeltaTime
            );
        }

        UpdateVisuals();
    }

    private void AnimateDamageFeedback()
    {
        vignetteAlpha = Mathf.MoveTowards(
            vignetteAlpha,
            0f,
            1.9f * Time.unscaledDeltaTime
        );

        if (damageVignette != null)
        {
            damageVignette.alpha = vignetteAlpha;
        }

        if (panel == null)
        {
            return;
        }

        if (shakeStrength > 0.05f)
        {
            panel.anchoredPosition = restingPanelPosition +
                Random.insideUnitCircle * shakeStrength;

            shakeStrength = Mathf.MoveTowards(
                shakeStrength,
                0f,
                shakeDecay * Time.unscaledDeltaTime
            );
        }
        else
        {
            panel.anchoredPosition = restingPanelPosition;
        }
    }

    private void AnimateCriticalState()
    {
        if (criticalGlow == null)
        {
            return;
        }

        float ratio = targetHealth / maxHealth;

        if (ratio > 0f && ratio <= 0.25f)
        {
            criticalGlow.alpha =
                0.45f + (Mathf.Sin(Time.unscaledTime * 6.5f) + 1f) * 0.2f;
        }
        else
        {
            criticalGlow.alpha = Mathf.MoveTowards(
                criticalGlow.alpha,
                0f,
                3f * Time.unscaledDeltaTime
            );
        }
    }

    private void UpdateVisuals()
    {
        float displayedRatio = Mathf.Clamp01(displayedHealth / maxHealth);
        float lagRatio = Mathf.Clamp01(lagHealth / maxHealth);

        SetHorizontalFill(healthFill, displayedRatio);
        SetHorizontalFill(damageLagFill, lagRatio);

        if (healthFillImage != null)
        {
            healthFillImage.color = GetHealthColor(displayedRatio);
        }

        UpdateHealthText();
    }

    private void UpdateStatus()
    {
        if (playerHealth != null && playerHealth.IsDead)
        {
            return;
        }

        float ratio = targetHealth / maxHealth;
        statusText.text = ratio > 0f && ratio <= 0.25f
            ? "KRITISK LIVSKRAFT"
            : "";
    }

    private void UpdateHealthText()
    {
        if (healthText == null)
        {
            return;
        }

        healthText.text =
            Mathf.CeilToInt(targetHealth) + "  /  " +
            Mathf.CeilToInt(maxHealth);
    }

    private Color GetHealthColor(float ratio)
    {
        if (ratio <= 0.25f)
        {
            return criticalColor;
        }

        if (ratio <= 0.55f)
        {
            return warningColor;
        }

        return healthyColor;
    }

    private static void SetHorizontalFill(
        RectTransform fill,
        float ratio
    )
    {
        if (fill == null)
        {
            return;
        }

        fill.anchorMax = new Vector2(ratio, 1f);
    }
}
