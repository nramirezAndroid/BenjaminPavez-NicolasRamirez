using System.Collections;
using UnityEngine;
using Unity.Netcode;

public class EnemyBase : NetworkBehaviour
{
    [Header("Estadísticas Base")]
    [SerializeField] protected int maxHealth;
    [SerializeField] protected float deathDelay;
    [SerializeField] protected int contactDamage;

    [Header("Ajustes de Combate")]
    [SerializeField] private float knockbackForce;
    [SerializeField] private float stunDuration;

    [Header("Recompensa de Tiempo")]
    [SerializeField] private float reduccionDeTiempo;

    protected Animator anim;
    protected Rigidbody2D rb;
    protected SpriteRenderer spriteRenderer;
    protected bool isStunned;
    protected bool isDead;

    //propiedad de solo lectura para scripts externos (ej: Player2Controller)
    public bool IsDead => isDead;

    public NetworkVariable<int> networkHealth = new NetworkVariable<int>(
        100, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server
    );

    public NetworkVariable<bool> networkIsPossessed = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server
    );

    protected Color originalColor;

    protected PlayerController GetPlayer1()
    {
        return PlayerTargetFinder.GetPlayer1();
    }

    protected bool IsPlayerInRange(float range)
    {
        PlayerController player = GetPlayer1();
        if (player == null) return false;

        float distance = Vector3.Distance(transform.position, player.transform.position);
        return distance <= range;
    }

    protected Vector3 GetDirectionToPlayer()
    {
        PlayerController player = GetPlayer1();
        if (player == null) return Vector3.zero;

        Vector3 direction = (player.transform.position - transform.position).normalized;
        return direction;
    }

    protected Vector3 GetPlayerPosition()
    {
        PlayerController player = GetPlayer1();
        return player != null ? player.transform.position : Vector3.zero;
    }

    protected float GetDistanceToPlayer()
    {
        PlayerController player = GetPlayer1();
        if (player == null) return float.MaxValue;

        return Vector3.Distance(transform.position, player.transform.position);
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            networkHealth.Value = maxHealth;
            networkIsPossessed.Value = false;
        }

        networkIsPossessed.OnValueChanged += OnPossessionChanged;
        PlayerTargetFinder.ForceRefresh();
    }

    public override void OnNetworkDespawn()
    {
        networkIsPossessed.OnValueChanged -= OnPossessionChanged;
    }

    protected virtual void Start()
    {
        anim           = GetComponent<Animator>();
        rb             = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();

        if (spriteRenderer != null)
            originalColor = spriteRenderer.color;
    }

    protected virtual void Update() { }

    public virtual void TakeDamage(int damage, Transform damageSource)
    {
        if (!IsServer || isDead) return;

        networkHealth.Value -= damage;

        if (networkHealth.Value <= 0)
        {
            Die();
        }
        else
        {
            Vector3 sourcePos = damageSource != null ? damageSource.position : transform.position;
            TakeDamageEffectsClientRpc(sourcePos);

            StopAllCoroutines();
            ApplyKnockback(damageSource);
            StartCoroutine(StunRoutine());
        }
    }

    private void ApplyKnockback(Transform source)
    {
        if (rb == null || source == null) return;
        Vector2 direction = (transform.position - source.position).normalized;
        rb.linearVelocity = Vector2.zero;
        rb.AddForce(new Vector2(direction.x * knockbackForce, 3f), ForceMode2D.Impulse);
    }

    private IEnumerator StunRoutine()
    {
        isStunned = true;
        yield return new WaitForSeconds(stunDuration);
        isStunned = false;
    }

    protected virtual void Die()
    {
        if (!IsServer || isDead) return;
        isDead = true;

        if (networkIsPossessed.Value) networkIsPossessed.Value = false;

        if (GameManager.instance != null)
            GameManager.instance.ModificarTiempo(-reduccionDeTiempo);

        DieEffectsClientRpc();
    }

    public void SetPossessed(bool value)
    {
        if (!IsServer) return;
        networkIsPossessed.Value = value;
    }

    private void OnPossessionChanged(bool previousValue, bool newValue)
    {
        if (spriteRenderer != null)
        {
            spriteRenderer.color = newValue ? new Color(1f, 0.5f, 1f) : originalColor;
        }
    }

    public void MoveAsPossessed(float horizontal)
    {
        if (!networkIsPossessed.Value || rb == null || !IsServer) return;
        rb.linearVelocity = new Vector2(horizontal * 3f, rb.linearVelocity.y);
    }

    //cuando el enemigo poseído colisiona con otro enemigo, le hace daño
    protected virtual void OnCollisionEnter2D(Collision2D collision)
    {
        if (!IsServer || isDead || isStunned) return;
        if (!networkIsPossessed.Value) return;

        EnemyBase otherEnemy = collision.gameObject.GetComponent<EnemyBase>();
        if (otherEnemy != null && !otherEnemy.IsDead && !otherEnemy.networkIsPossessed.Value)
            otherEnemy.TakeDamage(contactDamage, transform);
    }

    // NOTA NGO: los métodos [ClientRpc] NO deben ser virtual ni llamarse con base.XXXClientRpc()
    // desde clases derivadas — NGO intercepta esa llamada y envía otro RPC, causando recursión infinita.
    // Patrón correcto: el ClientRpc llama a un método virtual protegido sin [ClientRpc].
    // Las clases derivadas sobreescriben el método virtual, no el ClientRpc.

    [ClientRpc]
    protected void TakeDamageEffectsClientRpc(Vector3 sourcePosition)
    {
        OnTakeDamageLocal(sourcePosition);
    }

    protected virtual void OnTakeDamageLocal(Vector3 sourcePosition)
    {
        if (anim != null) anim.SetTrigger("Hurt");
    }

    [ClientRpc]
    protected void DieEffectsClientRpc()
    {
        OnDieLocal();
    }

    protected virtual void OnDieLocal()
    {
        isDead = true;
        if (anim != null) anim.SetTrigger("Die");

        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.enabled = false;

        MonoBehaviour[] scripts = GetComponents<MonoBehaviour>();
        foreach (MonoBehaviour script in scripts)
        {
            //apagamos los scripts excepto el de red para que termine de despawnear limpio
            if (script != this && !(script is NetworkBehaviour)) script.enabled = false;
        }

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.gravityScale   = 0f;
        }

        StartCoroutine(FadeOutRoutine());
    }

    private IEnumerator FadeOutRoutine()
    {
        if (spriteRenderer == null)
        {
            yield return new WaitForSeconds(deathDelay);
            if (IsServer) GetComponent<NetworkObject>().Despawn();
            yield break;
        }

        float timer = 0f;
        Color baseColor = spriteRenderer.color;

        while (timer < deathDelay)
        {
            timer += Time.deltaTime;
            float alpha = Mathf.Lerp(1f, 0f, timer / deathDelay);
            spriteRenderer.color = new Color(baseColor.r, baseColor.g, baseColor.b, alpha);
            yield return null;
        }

        if (IsServer) GetComponent<NetworkObject>().Despawn();
    }
}
