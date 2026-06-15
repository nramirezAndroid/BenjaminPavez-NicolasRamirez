using System.Collections;
using UnityEngine;
using Unity.Netcode;

public class EnemyLongSwordKnight : EnemyBase 
{
    [Header("Configuración de Persecución (IA)")]
    public Transform player;
    public float detectionRadius = 7f; 
    public float speed = 1.6f;

    [Header("IA: Detección de Borde")]
    public bool usarDeteccionDeBordes = false; 
    public float edgeCheckDistance = 0.5f;   
    public float groundCheckDepth = 2.0f; 
    public LayerMask groundLayer;            

    [Header("Mecánica de Espadón (Tajo Estático Fijo)")]
    [SerializeField] private Transform attackPoint;
    [SerializeField] private float attackRange = 1.6f;
    [SerializeField] private LayerMask playerLayer;
    [SerializeField] private float attackCooldown = 2.5f;
    [SerializeField] private float attackAnticipationTime = 0.5f;
    [SerializeField] private float attackRecoveryTime = 0.5f;
    [SerializeField] private int swordDamage = 30;

    private Vector2 movementDirection;
    private bool enMovimiento; 

    private float cooldownTimer;
    private bool isAttacking;

    //Sincroniza la dirección en la que mira para todos los clientes
    public NetworkVariable<bool> networkIsFacingRight = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server
    );

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        networkIsFacingRight.OnValueChanged += OnFacingRightChanged;
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        networkIsFacingRight.OnValueChanged -= OnFacingRightChanged;
    }

    protected override void Start()
    {
        maxHealth = 200; 
        base.Start(); 
        
        //Solo el servidor necesita buscar y seguir al jugador
        if (IsServer && player == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) player = p.transform;
        }
    }

    void Update()
    {
        //Toda la IA, cooldowns e inputs ocurren en el Servidor
        if (!IsServer) return;

        if (cooldownTimer > 0) cooldownTimer -= Time.deltaTime;

        if (isDead || isStunned) 
        {
            enMovimiento = false;
            if (anim != null) anim.SetBool("enMovimiento", false);
            return; 
        }

        if (isAttacking) return;

        if (networkIsPossessed.Value)
        {
            //El Host (P2) puede presionar Espacio para atacar manualmente
            if (Input.GetKeyDown(KeyCode.Space) && cooldownTimer <= 0)
            {
                StartCoroutine(SwordAttackRoutine());
                return;
            }

            //Sincroniza hacia dónde mira basado en cómo lo mueve el P2
            if (rb.linearVelocity.x > 0.1f && !networkIsFacingRight.Value) networkIsFacingRight.Value = true;
            else if (rb.linearVelocity.x < -0.1f && networkIsFacingRight.Value) networkIsFacingRight.Value = false;

            //Animación de caminar manual
            if (anim != null) anim.SetBool("enMovimiento", Mathf.Abs(rb.linearVelocity.x) > 0.1f);
            
            return;
        }

        if (player == null) return;

        float distanceToPlayer = Vector2.Distance(transform.position, player.position);

        if (distanceToPlayer < detectionRadius)
        {
            float directionX = player.position.x - transform.position.x;

            if (directionX > 0 && !networkIsFacingRight.Value) networkIsFacingRight.Value = true;
            else if (directionX < 0 && networkIsFacingRight.Value) networkIsFacingRight.Value = false;

            bool playerEnRango = attackPoint != null &&
                                 Physics2D.OverlapCircle(attackPoint.position, attackRange, playerLayer) != null;

            if (playerEnRango && cooldownTimer <= 0)
            {
                StartCoroutine(SwordAttackRoutine());
                return;
            }

            if (!playerEnRango)
            {
                if (!usarDeteccionDeBordes || CheckGroundAhead(directionX))
                {
                    movementDirection = new Vector2(directionX, 0).normalized;
                    enMovimiento = true;
                }
                else
                {
                    enMovimiento = false;
                }
            }
            else
            {
                enMovimiento = false;
            }
        }
        else 
        {
            enMovimiento = false;
        }

        if (anim != null) anim.SetBool("enMovimiento", enMovimiento);
    }

    void FixedUpdate()
    {
        //La física la sigue calculando exclusivamente el servidor
        if (!IsServer) return;

        //Si está poseído, la física de movimiento horizontal la maneja EnemyBase mediante MoveAsPossessed()
        if (networkIsPossessed.Value) return;

        if (isDead || isStunned || isAttacking)
        {
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            return;
        }

        if (enMovimiento)
            rb.linearVelocity = new Vector2(movementDirection.x * speed, rb.linearVelocity.y);
        else
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
    }

    private IEnumerator SwordAttackRoutine()
    {
        isAttacking  = true;
        enMovimiento = false;
        
        if (anim != null) anim.SetBool("enMovimiento", false);
        
        //Dispara la animación en todos los clientes al mismo tiempo
        TriggerAttackAnimClientRpc();

        yield return new WaitForSeconds(attackAnticipationTime);

        if (isDead || isStunned)
        {
            isAttacking = false;
            yield break;
        }

        if (attackPoint == null)
        {
            Debug.LogError("[EnemyLongSwordKnight] attackPoint no está asignado.");
            isAttacking = false;
            yield break;
        }

        //El Servidor calcula quién recibe el daño
        Collider2D hitPlayer = Physics2D.OverlapCircle(attackPoint.position, attackRange, playerLayer);
        if (hitPlayer != null)
        {
            PlayerControllerComplete playerScript = hitPlayer.GetComponent<PlayerControllerComplete>();
            if (playerScript != null)
                playerScript.TakeDamage(swordDamage, transform);
        }

        yield return new WaitForSeconds(attackRecoveryTime);

        isAttacking   = false;
        cooldownTimer = attackCooldown;
    }

    [ClientRpc]
    private void TriggerAttackAnimClientRpc()
    {
        if (anim != null) anim.SetTrigger("Attack"); 
    }

    //Evento que se ejecuta en las pantallas de todos los jugadores cuando el servidor cambia la dirección
    private void OnFacingRightChanged(bool previousValue, bool isRight)
    {
        Vector3 localScale = transform.localScale;
        localScale.x = isRight ? -Mathf.Abs(localScale.x) : Mathf.Abs(localScale.x); 
        transform.localScale = localScale;
    }

    private bool CheckGroundAhead(float dirX)
    {
        Vector2 origin = new Vector2(
            transform.position.x + (dirX > 0 ? edgeCheckDistance : -edgeCheckDistance),
            transform.position.y + 0.2f 
        );

        RaycastHit2D hit = Physics2D.Raycast(origin, Vector2.down, groundCheckDepth, groundLayer);
        return hit.collider != null;
    }

    
    [ClientRpc]
    protected override void TakeDamageEffectsClientRpc(Vector3 sourcePosition)
    {
        base.TakeDamageEffectsClientRpc(sourcePosition);
        
        //Detiene la animación de movimiento al recibir un golpe
        if (anim != null) anim.SetBool("enMovimiento", false);
    }

    [ClientRpc]
    protected override void DieEffectsClientRpc()
    {
        base.DieEffectsClientRpc();
        
        if (anim != null) anim.ResetTrigger("Hurt"); 
    }

    private void OnDrawGizmosSelected()
    {
        if (attackPoint != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(attackPoint.position, attackRange);
        }
    }
}