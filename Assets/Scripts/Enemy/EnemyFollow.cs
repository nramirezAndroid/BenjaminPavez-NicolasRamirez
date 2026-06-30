using UnityEngine;
using Unity.Netcode;

public class EnemyFollow : EnemyBase
{
    [Header("Configuración de Seguimiento")]
    [SerializeField] private float detectionRadius;
    [SerializeField] private float stoppingDistance;
    [SerializeField] private float speed;

    [Header("IA: Detección de Borde")]
    [SerializeField] private float edgeCheckDistance;
    [SerializeField] private float groundCheckDepth;
    [SerializeField] private LayerMask groundLayer;

    private Vector2 movement;
    private bool EnMovimiento;

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
        base.Start();
    }

    void Update()
    {
        //⭐ CRÍTICO: Solo el servidor ejecuta IA
        if (!IsServer) return;

        if (isDead || isStunned)
        {
            EnMovimiento   = false;
            isPatrolMoving = false;
            if (anim != null) anim.SetBool("enMovimiento", false);
            return;
        }

        //si está poseído por P2, no ejecuta IA
        if (networkIsPossessed.Value)
        {
            if (rb.linearVelocity.x > 0.1f  && !networkIsFacingRight.Value) networkIsFacingRight.Value = true;
            else if (rb.linearVelocity.x < -0.1f &&  networkIsFacingRight.Value) networkIsFacingRight.Value = false;
            if (anim != null) anim.SetBool("enMovimiento", Mathf.Abs(rb.linearVelocity.x) > 0.1f);
            return;
        }

        PlayerController player = GetPlayer1();
        float distanceToPlayer  = player != null && !player.IsDead
            ? GetDistanceToPlayer()
            : float.MaxValue;

        if (distanceToPlayer < detectionRadius)
        {
            //── MODO PERSECUCIÓN ──────────────────────────────────────────────
            isChasing      = true;
            isPatrolMoving = false;

            Vector3 directionToPlayer = GetDirectionToPlayer();
            float directionX = directionToPlayer.x;

            SetFacing(directionX > 0);

            if (distanceToPlayer > stoppingDistance)
            {
                if (CheckGroundAhead(directionX))
                {
                    movement     = new Vector2(directionX, 0).normalized;
                    EnMovimiento = true;
                }
                else
                    EnMovimiento = false;
            }
            else
                EnMovimiento = false;

            if (anim != null) anim.SetBool("enMovimiento", EnMovimiento);
        }
        else
        {
            //── MODO PATRULLAJE ───────────────────────────────────────────────
            isChasing    = false;
            EnMovimiento = false;
            HandlePatrol();   //definido en EnemyBase
        }
    }

    void FixedUpdate()
    {
        if (!IsServer) return;
        if (networkIsPossessed.Value) return;
        if (isDead || isStunned) return;

        if (isChasing && EnMovimiento)
            rb.linearVelocity = new Vector2(movement.x * speed, rb.linearVelocity.y);
        else if (!isChasing && isPatrolMoving)
            rb.linearVelocity = new Vector2(patrolMoveDir * patrolSpeed, rb.linearVelocity.y);
        else
            rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
    }

    // ─── Overrides de EnemyBase ───────────────────────────────────────────────

    // Sincroniza la dirección por red para que todos los clientes vean el flip correcto.
    protected override void SetFacing(bool facingRight)
    {
        if (facingRight  && !networkIsFacingRight.Value) networkIsFacingRight.Value = true;
        if (!facingRight &&  networkIsFacingRight.Value) networkIsFacingRight.Value = false;
    }

    // Usa el mismo raycast de borde que la persecución.
    protected override bool CheckPatrolGroundAhead(float dirX) => CheckGroundAhead(dirX);

    // ─── Helpers privados ─────────────────────────────────────────────────────

    private bool CheckGroundAhead(float dirX)
    {
        Vector2 origin = new Vector2(
            transform.position.x + (dirX > 0 ? edgeCheckDistance : -edgeCheckDistance),
            transform.position.y + 0.2f
        );
        RaycastHit2D hit = Physics2D.Raycast(origin, Vector2.down, groundCheckDepth, groundLayer);
        Debug.DrawRay(origin, Vector2.down * groundCheckDepth, hit.collider != null ? Color.green : Color.red);
        return hit.collider != null;
    }

    private void OnFacingRightChanged(bool previousValue, bool isRight)
    {
        Vector3 localScale = transform.localScale;
        localScale.x = isRight ? -Mathf.Abs(localScale.x) : Mathf.Abs(localScale.x);
        transform.localScale = localScale;
    }

    protected override void OnCollisionEnter2D(Collision2D collision)
    {
        base.OnCollisionEnter2D(collision);

        if (!IsServer || isDead || isStunned) return;

        if (!networkIsPossessed.Value && collision.gameObject.CompareTag("Player"))
        {
            PlayerController p = collision.gameObject.GetComponent<PlayerController>();
            if (p != null) p.TakeDamage(contactDamage, transform);
        }
    }
}
