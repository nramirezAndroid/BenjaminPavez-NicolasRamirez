using UnityEngine;

public class EnemyFollow : EnemyBase 
{
    [Header("Configuración de Seguimiento")]
    public Transform player;
    public float detectionRadius = 7f; //Aumentado para colinas
    public float stoppingDistance = 0.8f; 
    public float speed = 3f;

    [Header("IA: Detección de Borde")]
    public float edgeCheckDistance = 0.5f;   
    public float groundCheckDepth = 1.5f; //Rayo largo para pendientes
    public LayerMask groundLayer;            

    private Vector2 movement;
    private bool EnMovimiento; 
    private bool isFacingRight = false; 

    protected override void Start()
    {
        base.Start(); 
        if (player == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) player = p.transform;
        }
    }

    void Update()
    {
        if (isDead || isStunned || player == null) 
        {
            EnMovimiento = false;
            if (anim != null) anim.SetBool("enMovimiento", false);
            return; 
        }

        float distanceToPlayer = Vector2.Distance(transform.position, player.position);

        if (distanceToPlayer < detectionRadius)
        {
            float directionX = player.position.x - transform.position.x;

            if (directionX > 0 && !isFacingRight) Flip();
            else if (directionX < 0 && isFacingRight) Flip();

            //Movimiento solo si hay suelo
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
            else EnMovimiento = false;
        }
        else EnMovimiento = false;

        if (anim != null) anim.SetBool("enMovimiento", EnMovimiento);
    }

    void FixedUpdate()
    {
        if (isDead || isStunned) return;

        if (EnMovimiento)
            rb.linearVelocity = new Vector2(movement.x * speed, rb.linearVelocity.y);
        else
            rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
    }

    private bool CheckGroundAhead(float dirX)
    {
        //Lanzamos el rayo desde un poco arriba para evitar errores en colinas
        Vector2 origin = new Vector2(
            transform.position.x + (dirX > 0 ? edgeCheckDistance : -edgeCheckDistance),
            transform.position.y + 0.2f 
        );

        RaycastHit2D hit = Physics2D.Raycast(origin, Vector2.down, groundCheckDepth, groundLayer);
        Debug.DrawRay(origin, Vector2.down * groundCheckDepth, hit.collider != null ? Color.green : Color.red);

        return hit.collider != null;
    }

    private void Flip()
    {
        isFacingRight = !isFacingRight;
        Vector3 localScale = transform.localScale;
        localScale.x *= -1;
        transform.localScale = localScale;
    }

    private void OnCollisionEnter2D(Collision2D collision)
{
    if (isDead || isStunned) return;


    if (collision.gameObject.CompareTag("Player"))
    {
        PlayerControllerComplete p = collision.gameObject.GetComponent<PlayerControllerComplete>();
        if (p != null) 
        {
            //Le pasa el daño personalizado desde la variable de EnemyBase
            p.TakeDamage(contactDamage, transform); 
        }
    }
}
} 