using System.Collections;
using UnityEngine;
using Unity.Netcode; 

public class EnemyFlyingShooter : EnemyBase
{
    [Header("Referencias de Ataque")]
    public Transform player;
    public GameObject projectilePrefab;
    public Transform firePoint;

    [Header("Configuración de Vuelo")]
    public float detectionRange = 12f;   
    public float speed = 4f;            
    public float followDistance = 5f;    

    [Header("Efecto de Flote Sinusoidal")]
    public float waveSpeed = 3f;        
    public float waveMagnitude = 1f;    

    [Header("Configuración de Disparo")]
    public float fireCooldown = 2f;     
    private float fireTimer;

    [Header("Efecto Visual de Daño")]
    public Color flashColor = Color.red;
    public float flashDuration = 0.1f;

    private bool isFacingRight = false;
    private float timeCounter;

    protected override void Start()
    {
        maxHealth = 30; 
        contactDamage = 10;    
        base.Start(); 

        if (rb != null) rb.gravityScale = 0f;

        //El Servidor se encarga de buscar al jugador
        if (IsServer && player == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null) player = playerObj.transform;
        }

        fireTimer = fireCooldown;
    }

    void Update()
    {
        //La IA de los enemigos solo existe en el Servidor
        if (!IsServer) return;

        if (networkIsPossessed.Value) return;

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
            rb.linearVelocity = new Vector2(movimientoBase.x, movimientoBase.y + floteVertical);
            if (anim != null) anim.SetBool("enMovimiento", rb.linearVelocity.magnitude > 0.2f);
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

        //Avisa a los clientes que disparen la animación
        TriggerAttackAnimClientRpc();

        Vector2 direccionDisparo = (player.position - firePoint.position).normalized;
        float angulo = Mathf.Atan2(direccionDisparo.y, direccionDisparo.x) * Mathf.Rad2Deg;

        //El servidor instancia y Spawnea el proyectil en la red
        GameObject projectile = Instantiate(projectilePrefab, firePoint.position, Quaternion.Euler(0, 0, angulo));
        projectile.GetComponent<NetworkObject>().Spawn();
    }

    [ClientRpc]
    private void TriggerAttackAnimClientRpc()
    {
        if (anim != null) anim.SetTrigger("Attack");
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



    [ClientRpc]
    protected override void TakeDamageEffectsClientRpc(Vector3 sourcePosition)
    {
        base.TakeDamageEffectsClientRpc(sourcePosition); 

        if (spriteRenderer != null)
        {
            StopCoroutine(nameof(FlashRoutine));
            StartCoroutine(nameof(FlashRoutine));
        }
    }

    [ClientRpc]
    protected override void DieEffectsClientRpc()
    {
        base.DieEffectsClientRpc(); 
        
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