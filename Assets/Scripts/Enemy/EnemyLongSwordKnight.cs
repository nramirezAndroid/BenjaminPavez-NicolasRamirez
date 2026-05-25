using System.Collections;
using UnityEngine;

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
    private bool isFacingRight = false; 

    private float cooldownTimer;
    private bool isAttacking;

    protected override void Start()
    {
        health = 200;
        base.Start(); 
        
        if (player == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) player = p.transform;
        }
    }

    public override void TakeDamage(int damage, Transform damageSource)
    {
        base.TakeDamage(damage, damageSource);

        //Si el enemigo murió, se apaga el trigger "Hurt" antes de que el Animator lo procese
        if (isDead && anim != null)
        {
            anim.ResetTrigger("Hurt"); 
        }

        //Detiene su movimiento físico
        if (rb != null && !isDead)
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f);
    }

    void Update()
    {
        if (isDead || isStunned || player == null) 
        {
            enMovimiento = false;
            if (anim != null) anim.SetBool("enMovimiento", false);
            return; 
        }

        if (cooldownTimer > 0) cooldownTimer -= Time.deltaTime;

        float distanceToPlayer = Vector2.Distance(transform.position, player.position);

        if (isAttacking) return;

        if (distanceToPlayer < detectionRadius)
        {
            float directionX = player.position.x - transform.position.x;

            if (directionX > 0 && !isFacingRight) Flip();
            else if (directionX < 0 && isFacingRight) Flip();

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
        if (anim != null) anim.SetTrigger("Attack"); 

        yield return new WaitForSeconds(attackAnticipationTime);

        if (isDead || isStunned)
        {
            isAttacking = false;
            yield break;
        }

        if (attackPoint == null)
        {
            Debug.LogError("[EnemyLongSwordKnight] attackPoint no está asignado en el Inspector.");
            isAttacking = false;
            yield break;
        }

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

    private bool CheckGroundAhead(float dirX)
    {
        Vector2 origin = new Vector2(
            transform.position.x + (dirX > 0 ? edgeCheckDistance : -edgeCheckDistance),
            transform.position.y + 0.2f 
        );

        RaycastHit2D hit = Physics2D.Raycast(origin, Vector2.down, groundCheckDepth, groundLayer);
        return hit.collider != null;
    }

    private void Flip()
    {
        isFacingRight = !isFacingRight;
        Vector3 localScale = transform.localScale;
        localScale.x *= -1;
        transform.localScale = localScale;
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