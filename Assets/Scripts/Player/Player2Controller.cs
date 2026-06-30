using UnityEngine;
using TMPro;
using Unity.Netcode;

public class Player2Controller : NetworkBehaviour
{
    [Header("Posesión de enemigos")]
    [SerializeField] private float possessionRange;
    [SerializeField] private LayerMask enemyLayer;

    [Header("Buffos cooperativos")]
    [Tooltip("Multiplicador de velocidad de P1 durante el buffo")]
    [SerializeField] private float speedBuffMultiplier;
    [Tooltip("Multiplicador de daño de P1 durante el buffo")]
    [SerializeField] private float damageBuffMultiplier;
    [Tooltip("Cantidad de vida que se restaura con el buffo de curación")]
    [SerializeField] private int healAmount;
    [Tooltip("Duración en segundos de los buffos de velocidad y daño")]
    [SerializeField] private float buffDuration;
    [Tooltip("Tiempo de recarga entre buffos del mismo tipo (segundos)")]
    [SerializeField] private float buffCooldown;

    //tiempos de recarga individuales por tipo de buffo
    private float cooldownSpeed  = 0f;
    private float cooldownDamage = 0f;
    private float cooldownHeal   = 0f;

    [Header("UI de Buffos (iconos/cooldowns)")]
    [SerializeField] private TextMeshProUGUI cdSpeedText;
    [SerializeField] private TextMeshProUGUI cdDamageText;
    [SerializeField] private TextMeshProUGUI cdHealText;
    [SerializeField] private TextMeshProUGUI cdPossessionText;

    [Header("Cooldown de Posesión")]
    [Tooltip("Segundos de espera después de soltar/perder una posesión antes de poder poseer de nuevo")]
    [SerializeField] private float possessionCooldownDuration = 60f;
    private float possessionCooldownTimer = 0f;
    public bool CanPossess => possessionCooldownTimer <= 0f;

    // ─────────────────────────────────────────────────────────────
    // SPRITE / ANIMACIÓN
    // ─────────────────────────────────────────────────────────────
    [Header("Sprite del hada")]
    [Tooltip("SpriteRenderer del hada — se oculta mientras posee un enemigo")]
    [SerializeField] private SpriteRenderer spriteRenderer;

    // NetworkVariable para sincronizar el estado de posesión a todas las máquinas.
    // El OWNER (Cliente P2) escribe; HOST y otros leen → sprite se oculta en todos los lados.
    private NetworkVariable<bool> netIsPossessing = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner);

    // ─────────────────────────────────────────────────────────────

    [SerializeField] private bool isP2Active;

    private EnemyBase possessedEnemy = null;
    private Camera cam;

    //cacheamos al P1 para enviarle buffos
    private PlayerController player1 = null;

    // ─────────────────────────────────────────────────────────────
    // UNITY / NGO LIFECYCLE
    // ─────────────────────────────────────────────────────────────

    void Start()
    {
        if (cam == null) cam = Camera.main;
        if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        //el hada vuela junto a P1 sin interactuar con la física del nivel
        p2Rigidbody = GetComponent<Rigidbody2D>();
        if (p2Rigidbody != null)
        {
            p2Rigidbody.bodyType      = RigidbodyType2D.Kinematic; //pasa a través de paredes y plataformas
            p2Rigidbody.gravityScale  = 0f;
            p2Rigidbody.constraints   = RigidbodyConstraints2D.FreezeRotation;
        }
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            //teleportar el hada al hombro de P1 inmediatamente al spawnear
            PlayerController p1 = PlayerTargetFinder.GetPlayer1();
            if (p1 != null)
            {
                transform.position = new Vector3(
                    p1.transform.position.x + followOffset.x,
                    p1.transform.position.y + followOffset.y,
                    p1.transform.position.z);
                Debug.Log($"[Player2Controller] Hada teleportada junto a P1 en {transform.position}");
            }
        }

        if (IsOwner)
        {
            isP2Active = true;
            cam = Camera.main;
            BuscarHUDP2EnEscena();
            StartCoroutine(SetupCameraNextFrame());
        }
    }

    private void BuscarHUDP2EnEscena()
    {
        if (cdSpeedText == null)
        {
            GameObject obj = GameObject.Find("Speed Text");
            if (obj != null) cdSpeedText = obj.GetComponent<TextMeshProUGUI>();
            if (cdSpeedText == null) Debug.LogWarning("[Player2Controller] No se encontró 'Speed Text' en la escena.");
        }
        if (cdDamageText == null)
        {
            GameObject obj = GameObject.Find("Damage Text");
            if (obj != null) cdDamageText = obj.GetComponent<TextMeshProUGUI>();
            if (cdDamageText == null) Debug.LogWarning("[Player2Controller] No se encontró 'Damage Text' en la escena.");
        }
        if (cdHealText == null)
        {
            GameObject obj = GameObject.Find("Heal Text");
            if (obj != null) cdHealText = obj.GetComponent<TextMeshProUGUI>();
            if (cdHealText == null) Debug.LogWarning("[Player2Controller] No se encontró 'Heal Text' en la escena.");
        }

        if (cdPossessionText == null)
        {
            GameObject obj = GameObject.Find("Possession Text");
            if (obj != null) cdPossessionText = obj.GetComponent<TextMeshProUGUI>();
            if (cdPossessionText == null) Debug.LogWarning("[Player2Controller] No se encontró 'Possession Text' en la escena.");
        }

        //P2 no tiene dash: ocultar el ícono del cooldown de dash de P1
        GameObject dashIcon = GameObject.Find("Dashlcon") ?? GameObject.Find("DashIcon");
        if (dashIcon != null) dashIcon.SetActive(false);
    }

    System.Collections.IEnumerator SetupCameraNextFrame()
    {
        //El servidor mueve a P2 cerca de P1 en OnNetworkSpawn, pero NetworkTransform
        //no sincroniza la posición en el mismo frame: puede tardar 1-3 ticks de red (~50 ms).
        //Si snapeamos solo 1 frame después, la cámara queda en la posición por defecto
        //del prefab (área bosque) en vez del área dungeon donde está P1.
        //Solución: seguir snapeando cada frame hasta que la posición esté sincronizada
        //(máximo 0.6 s para no quedarse en bucle eterno si algo falla).

        CameraFollow cameraFollow = null;
        float elapsed = 0f;
        const float snapDuration = 30f; //hasta 30 s: cubre spawns tardíos de P1 en el cliente (deferred OnSpawn NGO)

        while (elapsed < snapDuration)
        {
            yield return null;
            elapsed += Time.deltaTime;

            if (cameraFollow == null)
                cameraFollow = FindAnyObjectByType<CameraFollow>();

            if (cameraFollow == null) continue;

            cameraFollow.target = transform;
            cameraFollow.SnapToTarget();

            //salir en cuanto P1 haya spawneado y P2 esté cerca (NetworkTransform ya sincronizó)
            PlayerController p1 = PlayerTargetFinder.GetPlayer1();
            if (p1 != null && Vector3.Distance(transform.position, p1.transform.position) < 12f)
            {
                cameraFollow.SnapToTarget();
                Debug.Log($"[Player2Controller] ✓ Cámara snapped junto a P1 en {transform.position} (tras {elapsed:F2}s)");
                yield break;
            }
        }

        //timeout: snap final con la posición que tenga en ese momento
        if (cameraFollow != null)
        {
            cameraFollow.target = transform;
            cameraFollow.SnapToTarget();
            Debug.Log($"[Player2Controller] ✓ Cámara snapped (timeout 3s) en {transform.position}");
        }
        else
        {
            Debug.LogWarning("[Player2Controller] ⚠️ No se encontró CameraFollow en la escena.");
        }
    }

    // ─────────────────────────────────────────────────────────────
    // UPDATE / FIXED UPDATE
    // ─────────────────────────────────────────────────────────────

    void Update()
    {
        if (Time.timeScale == 0f) return;

        //visuales se actualizan en todas las máquinas (sprite flip, ocultamiento durante posesión)
        UpdateVisuals();

        if (!isP2Active || !IsOwner) return;

        TickCooldowns();
        UpdateCooldownUI();
        HandleEnemyPossession();
        HandleBuffInputs();
    }

    void FixedUpdate()
    {
        //el seguimiento corre solo en el SERVIDOR porque NetworkTransform es Server-authoritative
        if (!IsServer) return;
        FollowPlayer1();
    }

    // ─────────────────────────────────────────────────────────────
    // VISUALES (todas las máquinas)
    // ─────────────────────────────────────────────────────────────

    //Actualiza cosas puramente visuales que todas las máquinas deben ver igual:
    //  · ocultamiento del sprite durante posesión
    //  · flip horizontal según la dirección de P1
    void UpdateVisuals()
    {
        if (spriteRenderer == null) return;

        //ocultar el sprite mientras se posee a un enemigo (leer el NetworkVariable)
        spriteRenderer.enabled = !netIsPossessing.Value;

        //flip del sprite para que el hada mire en la misma dirección que P1
        PlayerController p1 = PlayerTargetFinder.GetPlayer1();
        if (p1 != null)
        {
            bool p1FacingRight = p1.transform.localScale.x > 0f;
            spriteRenderer.flipX = !p1FacingRight;
        }
    }

    // ─────────────────────────────────────────────────────────────
    // POSESIÓN DE ENEMIGOS
    // ─────────────────────────────────────────────────────────────

    void HandleEnemyPossession()
    {
        //tick del cooldown de posesión
        if (possessionCooldownTimer > 0f)
            possessionCooldownTimer -= Time.deltaTime;

        //auto-release: si el enemigo poseído murió → cooldown
        if (possessedEnemy != null && possessedEnemy.IsDead)
        {
            possessedEnemy           = null;
            netIsPossessing.Value    = false;
            possessionCooldownTimer  = possessionCooldownDuration;
            Debug.Log($"[Player2Controller] Enemigo murió. Cooldown: {possessionCooldownDuration}s");
        }

        //movimiento del enemigo poseído (enviamos al servidor)
        if (possessedEnemy != null)
        {
            float h = Input.GetAxisRaw("Horizontal");
            MovePossessedEnemyServerRpc(possessedEnemy.NetworkObjectId, h);
        }

        if (!Input.GetMouseButtonDown(1)) return;

        //clic derecho: liberar posesión → cooldown
        if (possessedEnemy != null)
        {
            ReleasePossessionServerRpc(possessedEnemy.NetworkObjectId);
            possessedEnemy          = null;
            netIsPossessing.Value   = false;
            possessionCooldownTimer = possessionCooldownDuration;
            Debug.Log($"[Player2Controller] Posesión cancelada. Cooldown: {possessionCooldownDuration}s");
            return;
        }

        //intentar poseer un enemigo nuevo
        if (!CanPossess)
        {
            Debug.Log($"[Player2Controller] Posesión en cooldown: {possessionCooldownTimer:F0}s restantes.");
            return;
        }

        if (cam == null) cam = Camera.main;
        if (cam == null) return;

        Vector3 mouseWorld = cam.ScreenToWorldPoint(Input.mousePosition);
        mouseWorld.z = 0f;
        Collider2D hit = Physics2D.OverlapCircle(mouseWorld, possessionRange, enemyLayer);
        if (hit != null)
        {
            EnemyBase      enemy  = hit.GetComponent<EnemyBase>();
            NetworkObject  netObj = hit.GetComponent<NetworkObject>();
            if (enemy != null && netObj != null && !enemy.IsDead)
            {
                //el jefe no puede ser poseído
                if (enemy is EnemyLongSwordKnight)
                {
                    Debug.Log("[Player2Controller] No se puede poseer al jefe.");
                    return;
                }

                possessedEnemy        = enemy;
                netIsPossessing.Value = true;
                PossessEnemyServerRpc(netObj.NetworkObjectId);
            }
        }
    }

    [ServerRpc]
    private void PossessEnemyServerRpc(ulong enemyNetworkId)
    {
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(enemyNetworkId, out NetworkObject netObj))
        {
            EnemyBase enemy = netObj.GetComponent<EnemyBase>();
            if (enemy != null) enemy.SetPossessed(true);
        }
    }

    [ServerRpc]
    private void ReleasePossessionServerRpc(ulong enemyNetworkId)
    {
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(enemyNetworkId, out NetworkObject netObj))
        {
            EnemyBase enemy = netObj.GetComponent<EnemyBase>();
            if (enemy != null) enemy.SetPossessed(false);
        }
    }

    [ServerRpc]
    private void MovePossessedEnemyServerRpc(ulong enemyNetworkId, float horizontal)
    {
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(enemyNetworkId, out NetworkObject netObj))
        {
            EnemyBase enemy = netObj.GetComponent<EnemyBase>();
            if (enemy != null) enemy.MoveAsPossessed(horizontal);
        }
    }

    // ─────────────────────────────────────────────────────────────
    // SEGUIMIENTO A P1 (solo SERVER)
    // ─────────────────────────────────────────────────────────────

    [Header("Seguimiento a P1 (hada en el hombro)")]
    [SerializeField] private float followSpeed;
    [Tooltip("Desplazamiento relativo al hombro de P1 (X se invierte automáticamente según la dirección)")]
    public Vector3 followOffset = new Vector3(0.8f, 0.5f, 0f);

    private Rigidbody2D p2Rigidbody;

    void FollowPlayer1()
    {
        PlayerController p1 = PlayerTargetFinder.GetPlayer1();
        if (p1 == null) return;

        //invertir X del offset para que el hada siempre esté al frente de P1
        bool    p1FacingRight  = p1.transform.localScale.x > 0f;
        Vector2 effectiveOffset = new Vector2(p1FacingRight ? followOffset.x : -followOffset.x, followOffset.y);
        Vector2 targetPos      = (Vector2)p1.transform.position + effectiveOffset;

        //velocidad mínima garantizada de 8 u/s para no quedarse rezagada
        float effectiveSpeed = Mathf.Max(followSpeed, 8f);

        //usar transform directamente: el hada es Kinematic y vuela a través de la geometría
        transform.position = Vector2.MoveTowards(transform.position, targetPos, effectiveSpeed * Time.fixedDeltaTime);
    }

    // ─────────────────────────────────────────────────────────────
    // BUFFOS
    // ─────────────────────────────────────────────────────────────

    //teclas: Q = Velocidad | E = Daño | R = Curación

    void HandleBuffInputs()
    {
        if (Input.GetKeyDown(KeyCode.Q) && cooldownSpeed  <= 0f) RequestBuffServerRpc(BuffType.Speed);
        if (Input.GetKeyDown(KeyCode.E) && cooldownDamage <= 0f) RequestBuffServerRpc(BuffType.Damage);
        if (Input.GetKeyDown(KeyCode.R) && cooldownHeal   <= 0f) RequestBuffServerRpc(BuffType.Heal);
    }

    public enum BuffType { Speed, Damage, Heal }

    [ServerRpc]
    private void RequestBuffServerRpc(BuffType type) => ApplyBuffClientRpc(type);

    [ClientRpc]
    private void ApplyBuffClientRpc(BuffType type)
    {
        if (player1 == null)
            player1 = FindAnyObjectByType<PlayerController>();

        if (player1 == null)
        {
            Debug.LogWarning("[Player2Controller] No se encontró PlayerController para aplicar buffo.");
            return;
        }

        switch (type)
        {
            case BuffType.Speed:
                player1.ApplySpeedBuff(speedBuffMultiplier, buffDuration);
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

    // ─────────────────────────────────────────────────────────────
    // UI / COOLDOWNS
    // ─────────────────────────────────────────────────────────────

    void TickCooldowns()
    {
        if (cooldownSpeed  > 0f) cooldownSpeed  -= Time.deltaTime;
        if (cooldownDamage > 0f) cooldownDamage -= Time.deltaTime;
        if (cooldownHeal   > 0f) cooldownHeal   -= Time.deltaTime;
    }

    void UpdateCooldownUI()
    {
        if (cdSpeedText  != null)
            cdSpeedText.text  = cooldownSpeed  > 0f ? $"Speed Q ({cooldownSpeed:F0}s)"  : "Speed Q ready";
        if (cdDamageText != null)
            cdDamageText.text = cooldownDamage > 0f ? $"Damage E ({cooldownDamage:F0}s)" : "Damage E ready";
        if (cdHealText != null)
            cdHealText.text   = cooldownHeal   > 0f ? $"Healing R ({cooldownHeal:F0}s)"  : "Healing R ready";
        if (cdPossessionText != null)
            cdPossessionText.text = possessionCooldownTimer > 0f
                ? $"Possess ({possessionCooldownTimer:F0}s)"
                : "Possess ready";
    }

    // ─────────────────────────────────────────────────────────────
    // RESET
    // ─────────────────────────────────────────────────────────────

    public void ResetForNewRound()
    {
        possessedEnemy          = null;
        isP2Active              = false;
        cooldownSpeed           = 0f;
        cooldownDamage          = 0f;
        cooldownHeal            = 0f;
        possessionCooldownTimer = 0f;

        if (IsOwner && IsSpawned)
            netIsPossessing.Value = false;

        if (spriteRenderer != null) spriteRenderer.enabled = true;
    }
}
