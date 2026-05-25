using System.Collections;
using UnityEngine;


public class EnemyFlyingShooter : EnemyBase
{
    [Header("Referencias de Ataque")]
    public Transform player;
    public GameObject projectilePrefab;
    public Transform firePoint;

    [Header("Configuración de Vuelo")]
    public float detectionRange = 12f;   //Radio de visión para perseguir
    public float speed = 4f;            //Velocidad de traslación
    public float followDistance = 5f;    //Distancia que mantendrá flotando ante el jugador

    [Header("Efecto de Flote Sinusoidal (Estilo Castlevania)")]
    public float waveSpeed = 3f;        //Qué tan rápido ondula de arriba a abajo
    public float waveMagnitude = 1f;    //Amplitud de la onda de flote

    [Header("Configuración de Disparo")]
    public float fireCooldown = 2f;     //Segundos entre proyectiles
    private float fireTimer;

    [Header("Efecto Visual de Daño")]
    public Color flashColor = Color.red;
    public float flashDuration = 0.1f;
    private Color originalColor;

    private bool isFacingRight = false;
    private float timeCounter;

    protected override void Start()
    {
        health = 30;           //Vida del enemigo flotante
        contactDamage = 10;    //Daño si toca al player
        base.Start(); 

        if (spriteRenderer != null) originalColor = spriteRenderer.color;

        //Desactiva la gravedad inicial para permitir el vuelo libre
        if (rb != null) rb.gravityScale = 0f;

        //Búsqueda automatizada del Player por Tag
        if (player == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null) player = playerObj.transform;
        }

        fireTimer = fireCooldown;
    }

    void Update()
    {
        //Si el enemigo muere o está aturdido, congela el movimiento
        if (isDead || isStunned || player == null)
        {
            if (rb != null && !isDead) rb.linearVelocity = Vector2.zero;
            return;
        }

        float distanceToPlayer = Vector2.Distance(transform.position, player.position);

        if (distanceToPlayer <= detectionRange)
        {
            ManejarGiroMirada();
            ManejarMovimientoVolador();
            ManejarTemporizadorDisparo();
        }
        else
        {
            //Estado de reposo (Idle): Flota suavemente arriba y abajo en su lugar usando linearVelocity
            if (rb != null)
            {
                rb.linearVelocity = new Vector2(0f, Mathf.Sin(Time.time * waveSpeed) * waveMagnitude * 0.5f);
            }
            
        }
    }

    void ManejarMovimientoVolador()
    {
        Vector2 direccionAlPlayer = (player.position - transform.position).normalized;
        Vector2 posicionObjetivo = (Vector2)player.position - (direccionAlPlayer * followDistance);
        Vector2 movimientoBase = (posicionObjetivo - (Vector2)transform.position).normalized * speed;

        timeCounter += Time.deltaTime;
        float floteVertical = Mathf.Sin(timeCounter * waveSpeed) * waveMagnitude;

        if (rb != null)
        {
            //Aplica las físicas unificadas con la propiedad linearVelocity
            rb.linearVelocity = new Vector2(movimientoBase.x, movimientoBase.y + floteVertical);
            
            
        }
    }

    void ManejarTemporizadorDisparo()
    {
        fireTimer -= Time.deltaTime;
        if (fireTimer <= 0f)
        {
            DispararProyectil();
            fireTimer = fireCooldown;
        }
    }

    void DispararProyectil()
    {
        if (projectilePrefab == null || firePoint == null) return;

        if (anim != null) anim.SetTrigger("Attack");

        Vector2 direccionDisparo = (player.position - firePoint.position).normalized;
        float angulo = Mathf.Atan2(direccionDisparo.y, direccionDisparo.x) * Mathf.Rad2Deg;

        Instantiate(projectilePrefab, firePoint.position, Quaternion.Euler(0, 0, angulo));
    }

    void ManejarGiroMirada()
    {
        if (player.position.x > transform.position.x && !isFacingRight) Flip();
        else if (player.position.x < transform.position.x && isFacingRight) Flip();
    }

    private void Flip()
    {
        isFacingRight = !isFacingRight;
        Vector3 localScale = transform.localScale;
        localScale.x *= -1;
        transform.localScale = localScale;
    }

    //Polimorfismo para añadir el parpadeo rojo sobre tu sistema base
    public override void TakeDamage(int damage, Transform damageSource)
    {
        if (isDead) return;

        //Ejecuta el daño, el trigger "Hurt", el knockback y el stun
        base.TakeDamage(damage, damageSource); 

        if (spriteRenderer != null)
        {
            StopCoroutine(nameof(FlashRoutine));
            StartCoroutine(nameof(FlashRoutine));
        }
    }

    //Forzamos al enemigo a caer físicamente en lugar de flotar al morir
    protected override void Die()
    {
        //Ejecuta el trigger "Die"
        base.Die(); 
        
        this.enabled = false; 
        if (rb != null)
        {
            rb.gravityScale = 1f;
        }
    }

    private IEnumerator FlashRoutine()
    {
        spriteRenderer.color = flashColor;
        yield return new WaitForSeconds(flashDuration);
        spriteRenderer.color = originalColor;
    }
}