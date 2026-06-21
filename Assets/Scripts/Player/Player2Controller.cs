using UnityEngine;
using TMPro;
using Unity.Netcode;

/// <summary>
/// Controlador del Jugador 2 en modo cooperativo.
/// P2 NO se mueve por sí mismo. Solo puede:
/// - Poseer enemigos (clic derecho) para moverlos y atacar con ellos.
/// - Lanzar buffos sobre P1: velocidad (Q), daño (E), curación (R).
/// </summary>
public class Player2Controller : NetworkBehaviour
{
    // ─────────────────────────────────────────────────────────────────────────
    // POSESIÓN DE ENEMIGOS
    // ─────────────────────────────────────────────────────────────────────────

    [Header("Posesión de enemigos")]
    public float possessionRange = 3f;
    public LayerMask enemyLayer;

    // ─────────────────────────────────────────────────────────────────────────
    // BUFFOS COOPERATIVOS
    // ─────────────────────────────────────────────────────────────────────────

    [Header("Buffos cooperativos")]
    [Tooltip("Multiplicador de velocidad de P1 durante el buffo")]
    public float speedBuffMultiplier = 1.6f;
    [Tooltip("Multiplicador de daño de P1 durante el buffo")]
    public float damageBuffMultiplier = 2f;
    [Tooltip("Cantidad de vida que se restaura con el buffo de curación")]
    public int healAmount = 30;
    [Tooltip("Duración en segundos de los buffos de velocidad y daño")]
    public float buffDuration = 8f;
    [Tooltip("Tiempo de recarga entre buffos del mismo tipo (segundos)")]
    public float buffCooldown = 20f;

    // Tiempos de recarga individuales por tipo de buffo
    private float cooldownSpeed  = 0f;
    private float cooldownDamage = 0f;
    private float cooldownHeal   = 0f;

    // ─────────────────────────────────────────────────────────────────────────
    // UI
    // ─────────────────────────────────────────────────────────────────────────

    [Header("UI de Buffos (iconos/cooldowns)")]
    public TextMeshProUGUI cdSpeedText;
    public TextMeshProUGUI cdDamageText;
    public TextMeshProUGUI cdHealText;

    // ─────────────────────────────────────────────────────────────────────────
    // ESTADO INTERNO
    // ─────────────────────────────────────────────────────────────────────────

    public bool isP2Active = false;

    private EnemyBase possessedEnemy = null;
    private Camera cam;

    // Cacheamos al P1 para enviarle buffos. Se busca al inicio.
    private PlayerControllerComplete player1 = null;

    // ─────────────────────────────────────────────────────────────────────────
    // UNITY / NGO
    // ─────────────────────────────────────────────────────────────────────────

    void Start()
    {
        // cam se reasigna también en OnNetworkSpawn (más confiable para objetos
        // instanciados dinámicamente por red), pero lo dejamos aquí como
        // fallback por si este componente se usa fuera de un contexto de red.
        if (cam == null) cam = Camera.main;
    }

    public override void OnNetworkSpawn()
    {
        // [DIAGNÓSTICO TEMPORAL] — quitar una vez resuelto el problema de input.
        Debug.Log($"[Player2Controller] OnNetworkSpawn — IsOwner={IsOwner}, IsServer={IsServer}, IsClient={IsClient}, NetworkObjectId={NetworkObjectId}");

        // Cada cliente activa sus propios controles solo en su instancia local.
        // No se replica desde el servidor porque isP2Active es un campo simple,
        // no una NetworkVariable — cada cliente decide por sí mismo si es su
        // turno de jugar, basado en si es el dueño (IsOwner) de este objeto.
        if (IsOwner)
        {
            isP2Active = true;
            // Reconfirmamos la cámara aquí: en el momento de OnNetworkSpawn
            // el estado de escena/red es más estable que en Start().
            cam = Camera.main;

            // [DIAGNÓSTICO TEMPORAL]
            Debug.Log($"[Player2Controller] Soy el Owner. isP2Active={isP2Active}, cam={(cam != null ? cam.name : "NULL")}");
        }
    }

    void Update()
    {
        // [DIAGNÓSTICO TEMPORAL] — log solo una vez por segundo para no saturar consola.
        if (Time.frameCount % 60 == 0)
        {
            Debug.Log($"[Player2Controller] Update check — isP2Active={isP2Active}, IsOwner={IsOwner}, timeScale={Time.timeScale}");
        }

        if (!isP2Active || !IsOwner) return;
        if (Time.timeScale == 0f) return;

        TickCooldowns();
        UpdateCooldownUI();

        HandleEnemyPossession();
        HandleBuffInputs();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // POSESIÓN DE ENEMIGOS (clic derecho: poseer/liberar)
    // ─────────────────────────────────────────────────────────────────────────

    void HandleEnemyPossession()
    {
        // Movimiento del enemigo poseído (WASD/flechas horizontales)
        if (possessedEnemy != null)
        {
            float h = Input.GetAxisRaw("Horizontal");
            possessedEnemy.MoveAsPossessed(h);
        }

        if (!Input.GetMouseButtonDown(1)) return;

        // Clic derecho: poseer / liberar
        if (possessedEnemy != null)
        {
            possessedEnemy.SetPossessed(false);
            possessedEnemy = null;
            return;
        }

        if (cam == null) cam = Camera.main;
        if (cam == null) return;

        Vector3 mouseWorld = cam.ScreenToWorldPoint(Input.mousePosition);
        mouseWorld.z = 0f;
        Collider2D hit = Physics2D.OverlapCircle(mouseWorld, possessionRange, enemyLayer);
        if (hit != null)
        {
            EnemyBase enemy = hit.GetComponent<EnemyBase>();
            if (enemy != null && !enemy.isDead)
            {
                possessedEnemy = enemy;
                enemy.SetPossessed(true);
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // BUFFOS COOPERATIVOS
    // Teclas: Q = Velocidad | E = Daño | R = Curación
    // ─────────────────────────────────────────────────────────────────────────

    void HandleBuffInputs()
    {
        if (Input.GetKeyDown(KeyCode.Q) && cooldownSpeed <= 0f)
            RequestBuffServerRpc(BuffType.Speed);

        if (Input.GetKeyDown(KeyCode.E) && cooldownDamage <= 0f)
            RequestBuffServerRpc(BuffType.Damage);

        if (Input.GetKeyDown(KeyCode.R) && cooldownHeal <= 0f)
            RequestBuffServerRpc(BuffType.Heal);
    }

    public enum BuffType { Speed, Damage, Heal }

    [ServerRpc]
    private void RequestBuffServerRpc(BuffType type)
    {
        // El servidor aplica el buffo y confirma a todos los clientes
        ApplyBuffClientRpc(type);
    }

    [ClientRpc]
    private void ApplyBuffClientRpc(BuffType type)
    {
        // Buscamos al P1 si aún no lo tenemos
        if (player1 == null)
            player1 = FindAnyObjectByType<PlayerControllerComplete>();

        if (player1 == null)
        {
            Debug.LogWarning("[Player2Controller] No se encontró PlayerControllerComplete para aplicar buffo.");
            return;
        }

        switch (type)
        {
            case BuffType.Speed:
                player1.ApplySpeedBuff(speedBuffMultiplier, buffDuration);
                // Iniciamos cooldown local en el cliente P2
                if (IsOwner) cooldownSpeed = buffCooldown;
                break;

            case BuffType.Damage:
                player1.ApplyDamageBuff(damageBuffMultiplier, buffDuration);
                if (IsOwner) cooldownDamage = buffCooldown;
                break;

            case BuffType.Heal:
                player1.Heal(healAmount);
                if (IsOwner) cooldownHeal = buffCooldown;
                break;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // COOLDOWNS Y UI
    // ─────────────────────────────────────────────────────────────────────────

    void TickCooldowns()
    {
        if (cooldownSpeed  > 0f) cooldownSpeed  -= Time.deltaTime;
        if (cooldownDamage > 0f) cooldownDamage -= Time.deltaTime;
        if (cooldownHeal   > 0f) cooldownHeal   -= Time.deltaTime;
    }

    void UpdateCooldownUI()
    {
        if (cdSpeedText  != null)
            cdSpeedText.text  = cooldownSpeed  > 0f ? $"Q ({cooldownSpeed:F0}s)"  : "Q listo";
        if (cdDamageText != null)
            cdDamageText.text = cooldownDamage > 0f ? $"E ({cooldownDamage:F0}s)" : "E listo";
        if (cdHealText   != null)
            cdHealText.text   = cooldownHeal   > 0f ? $"R ({cooldownHeal:F0}s)"   : "R listo";
    }

    // ─────────────────────────────────────────────────────────────────────────
    // RESET ENTRE PARTIDAS
    // ─────────────────────────────────────────────────────────────────────────

    public void ResetForNewRound()
    {
        possessedEnemy = null;
        isP2Active     = false;
        cooldownSpeed  = 0f;
        cooldownDamage = 0f;
        cooldownHeal   = 0f;
    }
}
