using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;

//la lógica de movimiento vive en PlayerControllerMovement.cs
//la lógica de combate/buffos vive en PlayerControllerCombat.cs

//estados posibles del jugador (máquina de estados finitos)
public enum PlayerState
{
    Idle,
    Running,
    Jumping,
    Falling,
    WallSliding,
    Dashing,
    Attacking,
    KnockedBack,
    Dead,
    Talking
}

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(CapsuleCollider2D))]
public partial class PlayerController : NetworkBehaviour
{
    [Header("UI del Jugador")]
    [SerializeField] private Image healthImage;
    [SerializeField] private Image dashCooldownImage;

    [Header("Controlador de Sonidos")]
    [SerializeField] private PlayerSoundController soundCtrl;

    [Header("Animator")]
    [SerializeField] private Animator animator;

    [Header("Sistema de Vida")]
    [SerializeField] private int   maxHealth      = 100;

    public NetworkVariable<int> networkHealth = new NetworkVariable<int>(
        100, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private bool isDead;
    //propiedad para que Dialogue.cs pueda leer y escribir sin exponer campo en el inspector
    public bool IsTalking { get; set; }
    private PlayerEffectsController fxController;
    private SpriteRenderer spriteRenderer;

    public int  CurrentHealth        => networkHealth.Value;
    public int  MaxHealth            => maxHealth;
    public bool IsDead               => isDead;
    public int  CurrentSprintCharges => currentSprintCharges;   //definido en Movement partial

    //estado actual derivado de los booleans internos (definidos en partials de Movement y Combat)
    public PlayerState CurrentState
    {
        get
        {
            if (isDead)          return PlayerState.Dead;
            if (IsTalking)       return PlayerState.Talking;
            if (isKnockedBack)   return PlayerState.KnockedBack;
            if (isSprinting)     return PlayerState.Dashing;
            if (isWallSliding)   return PlayerState.WallSliding;
            if (isJumping)       return PlayerState.Jumping;
            if (!isGrounded && rb != null && rb.linearVelocity.y < -0.1f) return PlayerState.Falling;
            if (Mathf.Abs(horizontalInput) > 0.01f) return PlayerState.Running;
            return PlayerState.Idle;
        }
    }

    void Start()
    {
        fxController   = GetComponent<PlayerEffectsController>();
        spriteRenderer = GetComponent<SpriteRenderer>();

        InitMovement();   //playerControllerMovement.cs
        InitCombat();     //playerControllerCombat.cs

        //Solo el dueño del objeto aplica la posición guardada (IsOwner = true en el HOST para P1).
        //En el CLIENT, P1 es un objeto remoto (IsOwner = false): si aplicáramos SaveSystem aquí
        //reposicionaríamos a P1 con datos de save locales del CLIENT → coordenadas incorrectas.
        //IsSpawned puede ser false si Start() corre antes de OnNetworkSpawn (solitario);
        //en ese caso !IsSpawned → esOwner = true → comportamiento original sin cambios.
        bool esOwner = !IsSpawned || IsOwner;
        if (esOwner && SaveSystem.instance != null && SaveSystem.instance.pendingLoad != null)
        {
            SaveData data = SaveSystem.instance.pendingLoad;
            transform.position = new Vector3(data.playerX, data.playerY, transform.position.z);
        }

        spawnPosition = transform.position;   //campo definido en Movement partial
    }

    public override void OnNetworkSpawn()
    {
        //forzar AlwaysAnimate en TODAS las máquinas: el modo "Cull Completely" (por defecto)
        //pausa el Animator cuando el SpriteRenderer no tiene bounds (sprite null al inicio).
        //Esto bloqueaba el SyncAnimationsClientRpc en el cliente → P1 invisible.
        if (animator != null)
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        if (IsServer)
        {
            networkHealth.Value = (SaveSystem.instance != null && SaveSystem.instance.pendingLoad != null)
                ? SaveSystem.instance.pendingLoad.playerHealth
                : maxHealth;

            Debug.Log("Partida Cargada - Jugador con vida: " + networkHealth.Value);
        }

        if (IsOwner)
        {
            CameraFollow cam = FindAnyObjectByType<CameraFollow>();
            if (cam != null) cam.target = transform;

            //los campos de HUD no pueden asignarse en el prefab porque son objetos de escena.
            //los buscamos por nombre aquí; solo el jugador dueño necesita actualizar su propio HUD.
            BuscarHUDEnEscena();
        }

        //En el CLIENTE: P1 no es dueño (IsOwner=false) ni servidor (IsServer=false).
        //Registrarlo directamente en PlayerTargetFinder para que P2 lo encuentre
        //sin depender de FindGameObjectWithTag (falla si el objeto está inactivo
        //durante la inicialización de NGO) o FindAnyObjectByType (también falla).
        if (!IsOwner && !IsServer)
            PlayerTargetFinder.RegisterPlayer1(this);
    }

    private void BuscarHUDEnEscena()
    {
        if (healthImage == null)
        {
            GameObject obj = GameObject.Find("Health");
            if (obj != null) healthImage = obj.GetComponent<Image>();
            if (healthImage == null) Debug.LogWarning("[PlayerController] No se encontró 'Health' Image en la escena.");
        }

        if (dashCooldownImage == null)
        {
            //el objeto se llama "Dashlcon" en la jerarquía
            GameObject obj = GameObject.Find("Dashlcon") ?? GameObject.Find("DashIcon");
            if (obj != null) dashCooldownImage = obj.GetComponent<Image>();
            if (dashCooldownImage == null) Debug.LogWarning("[PlayerController] No se encontró imagen de cooldown de dash.");
        }

    }

    void Update()
    {
        if (isDead || (GameManager.instance != null && GameManager.instance.IsPaused)) return;
        if (Time.timeScale == 0f) return;

        if (IsOwner)
        {
            UpdateHUD();

            if (IsTalking)
            {
                horizontalInput   = 0;
                rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
                UpdateAnimations();
                return;
            }

            ReadInputs();
            CheckSurroundings();
            DetermineWallSlideState();
            HandleJumpInput();
            HandleAttackInput();
            UpdateAnimations();
            UpdateSprintCooldown();
            UpdateJumpState();
        }
    }

    void FixedUpdate()
    {
        if (isDead || (GameManager.instance != null && GameManager.instance.IsPaused) || IsTalking || !IsOwner) return;
        Move();
        ApplyJumpHold();
    }

    private void UpdateHUD()
    {
        if (healthImage != null)
            healthImage.fillAmount = (float)networkHealth.Value / maxHealth;

        //dashCooldownImage: fill = 1 cuando está recargando, 0 cuando está listo
        if (dashCooldownImage != null)
            dashCooldownImage.fillAmount = currentSprintCharges == maxSprintCharges
                ? 0f
                : sprintCooldownTimer / sprintCooldown;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Damage") && IsOwner)
            RequestTakeDamageServerRpc(5);
    }

    public void TakeDamage(int damage, Transform damageSource = null)
    {
        if (!IsServer || isDead) return;
        networkHealth.Value = Mathf.Max(0, networkHealth.Value - damage);
        TakeDamageClientRpc(damageSource != null ? damageSource.position : Vector3.zero);
        if (networkHealth.Value <= 0) DieClientRpc();
    }

    public void Heal(int amount)
    {
        if (!IsServer) return;
        networkHealth.Value = Mathf.Min(networkHealth.Value + amount, maxHealth);
        HealFlashClientRpc();
    }

    [ClientRpc]
    private void TakeDamageClientRpc(Vector3 sourcePosition)
    {
        soundCtrl.PlayRecibirDano();
        if (animator != null) animator.SetTrigger("Hurt");

        if (sourcePosition != Vector3.zero && IsOwner)
        {
            GameObject tempSrc = new GameObject();
            tempSrc.transform.position = sourcePosition;
            StartCoroutine(KnockbackRoutine(tempSrc.transform));   //definido en Combat partial
            Destroy(tempSrc, knockbackDuration + 0.1f);
        }
    }

    [ClientRpc]
    private void DieClientRpc()
    {
        soundCtrl.PlayMuerte();
        isDead = true;

        if (IsServer && GameManager.instance != null) GameManager.instance.ModificarTiempo(90f);
        if (IsServer && CoopManager.instance  != null) CoopManager.instance.OnPlayer1Died();

        if (animator != null) animator.SetTrigger("Die");
        rb.linearVelocity = Vector2.zero;

        if (IsOwner) StartCoroutine(DeathRespawnRoutine());
    }

    private IEnumerator DeathRespawnRoutine()
    {
        yield return new WaitForSeconds(1.5f);
        RequestRespawnServerRpc();
    }

    [ServerRpc]
    private void RequestRespawnServerRpc()
    {
        networkHealth.Value = maxHealth;
        RespawnClientRpc();
    }

    [ClientRpc]
    private void RespawnClientRpc()
    {
        isDead = false;

        //resetear movimiento
        jumpsRemaining       = maxJumps;
        isJumping            = false;
        currentSprintCharges = maxSprintCharges;
        sprintCooldownTimer  = sprintCooldown;

        //resetear buffos de combate
        ResetCombatBuffs();

        if (IsOwner)
        {
            rb.linearVelocity  = Vector2.zero;
            transform.position = spawnPosition;
        }

        if (animator != null)
        {
            animator.SetBool("IsFalling", false);
            animator.Play("idle");
        }
    }

    //usado por CoopManager para mostrar la pantalla de victoria en el cliente.
    //CoopManager no está spawneado → su ClientRpc no llega; este sí porque PlayerController está spawneado.
    [ClientRpc]
    public void MostrarVictoriaClientRpc(float completionTime)
    {
        if (IsServer) return;
        if (CoopManager.instance != null)
            CoopManager.instance.MostrarVictoriaLocal(completionTime);
    }

    //usado por CoopManager para notificar al cliente (P2) que debe cargar la siguiente escena.
    //CoopManager's NetworkObject no está spawneado (DontDestroyOnLoad en Awake lo mueve fuera
    //de la escena antes de que NGO la procese) → sus [ClientRpc] no llegan a P2.
    //PlayerController SÍ está spawneado vía SpawnAsPlayerObject → este RPC sí funciona.
    [ClientRpc]
    public void CargarSiguienteNivelClientRpc(string nombreSiguienteNivel)
    {
        if (IsServer) return; //HOST carga via coroutine en CoopManager
        Debug.Log($"[PlayerController] CLIENTE: cargando '{nombreSiguienteNivel}'");
        CoopNetworkManager.EstaTransicionandoEscena = true;
        Time.timeScale = 1f;
        //Usamos SceneManager directo (sin LoadingScreenManager) para que P2 cargue
        //a la misma velocidad que P1. Así los spawn messages de enemigos/jugadores
        //llegan DESPUÉS de que P2 tiene la escena lista → no más deferred timeouts.
        UnityEngine.SceneManagement.SceneManager.LoadScene(nombreSiguienteNivel);
    }

    [ServerRpc]
    private void RequestTakeDamageServerRpc(int damage) => TakeDamage(damage);

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void ApplyDamageServerRpc(ulong targetNetworkId, int damage)
    {
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(targetNetworkId, out NetworkObject target))
        {
            EnemyBase enemy = target.GetComponent<EnemyBase>();
            if (enemy != null) enemy.TakeDamage(damage, transform);
        }
    }

    [ClientRpc]
    private void SpawnAttackVFXClientRpc(Vector3 position, bool facingRight)
    {
        if (IsOwner) return;
        if (fxController != null) fxController.PlayAttackSlashAt(position, facingRight);
    }

    [ServerRpc]
    private void SyncAnimationsServerRpc(float movement, bool grounded, float yVel, bool falling, bool facing)
        => SyncAnimationsClientRpc(movement, grounded, yVel, falling, facing);

    [ClientRpc]
    private void SyncAnimationsClientRpc(float movement, bool grounded, float yVel, bool falling, bool facingRight)
    {
        if (IsOwner) return;
        if (animator == null) return;
        animator.SetFloat("movement",   movement);
        animator.SetBool ("IsGrounded", grounded);
        animator.SetFloat("yVelocity",  yVel);
        animator.SetBool ("IsFalling",  falling);
        float absX = Mathf.Abs(transform.localScale.x);
        transform.localScale = new Vector3(facingRight ? absX : -absX, transform.localScale.y, transform.localScale.z);
    }

    [ClientRpc]
    private void HealFlashClientRpc() => StartCoroutine(HealFlashRoutine());   //definido en Combat partial

    private void OnDrawGizmosSelected()
    {
        if (groundCheck != null) { Gizmos.color = Color.green;  Gizmos.DrawWireSphere(groundCheck.position, checkRadius); }
        if (wallCheck   != null) { Gizmos.color = Color.blue;   Gizmos.DrawWireSphere(wallCheck.position,   checkRadius); }
        if (attackPoint != null) { Gizmos.color = Color.yellow; Gizmos.DrawWireSphere(attackPoint.position, attackRange); }
    }
}
