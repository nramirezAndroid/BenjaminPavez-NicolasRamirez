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
            EnMovimiento = false;
            if (anim != null) anim.SetBool("enMovimiento", false);
            return; 
        }

        //si está poseído por P2, no ejecuta IA
        if (networkIsPossessed.Value)
        {
            //sincroniza hacia dónde mira basado en cómo lo mueve el P2
            if (rb.linearVelocity.x > 0.1f && !networkIsFacingRight.Value) 
                networkIsFacingRight.Value = true;
            else if (rb.linearVelocity.x < -0.1f && networkIsFacingRight.Value) 
                networkIsFacingRight.Value = false;

            //animación de caminar manual
            if (anim != null) anim.SetBool("enMovimiento", Mathf.Abs(rb.linearVelocity.x) > 0.1f);
            return;
        }

        //⭐ Obtén al jugador de forma segura con GetPlayer1()
        PlayerController player = GetPlayer1();
        if (player == null || player.IsDead)
        {
            EnMovimiento = false;
            if (anim != null) anim.SetBool("enMovimiento", false);
            return; 
        }

        //⭐ Usa GetDistanceToPlayer() en lugar de calcular manualmente
        float distanceToPlayer = GetDistanceToPlayer();

        if (distanceToPlayer < detectionRadius)
        {
            //⭐ Usa GetDirectionToPlayer() para obtener solo la dirección X
            Vector3 directionToPlayer = GetDirectionToPlayer();
            float directionX = directionToPlayer.x;

            if (directionX > 0 && !networkIsFacingRight.Value) 
                networkIsFacingRight.Value = true;
            else if (directionX < 0 && networkIsFacingRight.Value) 
                networkIsFacingRight.Value = false;

            //movimiento solo si hay suelo
            if (distanceToPlayer > stoppingDistance)
            {
                if (CheckGroundAhead(directionX))
                {
                    movement = new Vector2(directionX, 0).normalized;
                    EnMovimiento = true;
                }
                else
                {
                    EnMovimiento = false; 
                }
            }
            else 
                EnMovimiento = false;
        }
        else 
            EnMovimiento = false;

        if (anim != null) anim.SetBool("enMovimiento", EnMovimiento);
    }

    void FixedUpdate()
    {
        //la física la sigue calculando exclusivamente el servidor
        if (!IsServer) return;

        //si está poseído, la física de movimiento horizontal la maneja EnemyBase mediante MoveAsPossessed()
        if (networkIsPossessed.Value) return;

        if (isDead || isStunned) return;

        if (EnMovimiento)
            rb.linearVelocity = new Vector2(movement.x * speed, rb.linearVelocity.y);
        else
            rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
    }

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
        //base maneja el daño a otros enemigos cuando está poseído
        base.OnCollisionEnter2D(collision);

        if (!IsServer || isDead || isStunned) return;

        //cuando NO está poseído, daña al jugador por contacto
        if (!networkIsPossessed.Value && collision.gameObject.CompareTag("Player"))
        {
            PlayerController p = collision.gameObject.GetComponent<PlayerController>();
            if (p != null) p.TakeDamage(contactDamage, transform);
        }
    }
}