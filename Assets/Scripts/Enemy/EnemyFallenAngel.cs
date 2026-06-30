using System.Collections;
using UnityEngine;
using Unity.Netcode; 

public class EnemyFlyingShooter : EnemyBase
{
    [Header("Referencias de Ataque")]
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private Transform firePoint;

    [Header("Configuración de Vuelo")]
    [SerializeField] private float detectionRange;
    [SerializeField] private float speed;
    [SerializeField] private float followDistance;

    [Header("Efecto de Flote Sinusoidal")]
    [SerializeField] private float waveSpeed;
    [SerializeField] private float waveMagnitude;

    [Header("Configuración de Disparo")]
    [SerializeField] private float fireCooldown;
    private float fireTimer;

    [Header("Efecto Visual de Daño")]
    [SerializeField] private Color flashColor;
    [SerializeField] private float flashDuration;

    private bool isFacingRight = false;
    private float timeCounter;

    protected override void Start()
    {
        maxHealth = 30; 
        contactDamage = 10;    
        base.Start(); 

        if (rb != null) rb.gravityScale = 0f;
        
        //⭐ BUSCA el FirePoint automáticamente en los children
        if (firePoint == null)
        {
            firePoint = transform.Find("FirePoint");
            
            if (firePoint == null)
            {
                Debug.LogWarning($"[EnemyFlyingShooter] {gameObject.name} - FirePoint no encontrado en children!");
            }
            else
            {
                Debug.Log($"[EnemyFlyingShooter] {gameObject.name} - FirePoint encontrado automáticamente");
            }
        }

        fireTimer = fireCooldown;
    }

    void Update()
    {
        //⭐ CRÍTICO: Solo el servidor ejecuta IA
        if (!IsServer) return;

        if (networkIsPossessed.Value) return;

        //⭐ Obtén al jugador de forma segura con GetPlayer1()
        PlayerController player = GetPlayer1();
        
        if (isDead || isStunned || player == null)
        {
            if (rb != null && !isDead) rb.linearVelocity = Vector2.zero;
            return;
        }

        //⭐ Usa GetDistanceToPlayer() en lugar de calcular manualmente
        float distanceToPlayer = GetDistanceToPlayer();

        if (distanceToPlayer <= detectionRange)
        {
            //── MODO PERSECUCIÓN ──────────────────────────────────────────────
            isChasing = true;
            ManejarGiroMirada(player);
            ManejarMovimientoVolador(player);
            ManejarTemporizadorDisparo(player);
        }
        else
        {
            //── MODO PATRULLAJE ───────────────────────────────────────────────
            // HandlePatrol() actualiza patrolMoveDir e isPatrolMoving.
            // Aplicamos la velocidad X de patrullaje manteniendo el flote sinusoidal en Y.
            isChasing = false;
            HandlePatrol();
            if (rb != null)
                rb.linearVelocity = new Vector2(
                    patrolMoveDir * patrolSpeed,
                    Mathf.Sin(Time.time * waveSpeed) * waveMagnitude * 0.5f
                );
        }
    }

    void ManejarMovimientoVolador(PlayerController player)
    {
        if (player == null) return;

        //⭐ Usa GetDirectionToPlayer() para obtener la dirección
        Vector2 direccionAlPlayer = GetDirectionToPlayer();
        Vector2 posicionObjetivo = (Vector2)player.transform.position - (direccionAlPlayer * followDistance);
        Vector2 movimientoBase = (posicionObjetivo - (Vector2)transform.position).normalized * speed;

        timeCounter += Time.deltaTime;
        float floteVertical = Mathf.Sin(timeCounter * waveSpeed) * waveMagnitude;

        if (rb != null)
        {
            rb.linearVelocity = new Vector2(movimientoBase.x, movimientoBase.y + floteVertical);
            
        }
    }

    void ManejarTemporizadorDisparo(PlayerController player)
    {
        fireTimer -= Time.deltaTime;
        if (fireTimer <= 0f)
        {
            DispararProyectil(player);
            fireTimer = fireCooldown;
        }
    }

    void DispararProyectil(PlayerController player)
    {
        if (projectilePrefab == null || firePoint == null || player == null) return;

        //avisa a los clientes que disparen la animación
        TriggerAttackAnimClientRpc();

        Vector2 direccionDisparo = (player.transform.position - firePoint.position).normalized;
        float angulo = Mathf.Atan2(direccionDisparo.y, direccionDisparo.x) * Mathf.Rad2Deg;

        //el servidor instancia y Spawnea el proyectil en la red
        GameObject projectile = Instantiate(projectilePrefab, firePoint.position, Quaternion.Euler(0, 0, angulo));
        projectile.GetComponent<NetworkObject>().Spawn();
    }

    [ClientRpc]
    private void TriggerAttackAnimClientRpc()
    {
        if (anim != null) anim.SetTrigger("Attack");
    }

    void ManejarGiroMirada(PlayerController player)
    {
        if (player == null) return;
        
        if (player.transform.position.x > transform.position.x && !isFacingRight) 
            Flip();
        else if (player.transform.position.x < transform.position.x && isFacingRight) 
            Flip();
    }

    private void Flip()
    {
        isFacingRight = !isFacingRight;
        Vector3 localScale = transform.localScale;
        localScale.x *= -1;
        transform.localScale = localScale;
    }

    // ─── Overrides de EnemyBase ───────────────────────────────────────────────

    // El volador no usa NetworkVariable de dirección: reutiliza su propio sistema Flip().
    protected override void SetFacing(bool facingRight)
    {
        if (facingRight != isFacingRight) Flip();
    }

    // Sin detección de bordes: vuela sobre vacíos sin problema.
    // CheckPatrolGroundAhead devuelve true por defecto en EnemyBase, no hace falta override.

    protected override void OnTakeDamageLocal(Vector3 sourcePosition)
    {
        base.OnTakeDamageLocal(sourcePosition);
        if (spriteRenderer != null)
        {
            StopCoroutine(nameof(FlashRoutine));
            StartCoroutine(nameof(FlashRoutine));
        }
    }

    protected override void OnDieLocal()
    {
        base.OnDieLocal();
        this.enabled = false;
        if (rb != null) rb.gravityScale = 1f;
    }

    private IEnumerator FlashRoutine()
    {
        spriteRenderer.color = flashColor;
        yield return new WaitForSeconds(flashDuration);
        spriteRenderer.color = originalColor;
    }
}