using System.Collections;
using UnityEngine;
using Unity.Netcode;

//ataque, knockback, buffos cooperativos (velocidad, daño, curación).
public partial class PlayerController
{
    [Header("Combate")]
    [SerializeField] private Transform  attackPoint;
    [SerializeField] private float      attackRange  = 0.6f;
    [SerializeField] private LayerMask  enemyLayers;
    [SerializeField] private int        attackDamage = 40;

    [Header("Configuración de Balance (Combate)")]
    [SerializeField] private float attackCooldown;
    private float nextAttackTime = 0f;

    [Header("Knockback")]
    [SerializeField] private float knockbackForceX   = 8f;
    [SerializeField] private float knockbackForceY   = 5f;
    [SerializeField] private float knockbackDuration = 0.2f;

    private float baseWalkSpeed;
    private int   baseAttackDamage;
    private Coroutine speedBuffCoroutine;
    private Coroutine damageBuffCoroutine;

    private void InitCombat()
    {
        baseWalkSpeed    = walkSpeed;
        baseAttackDamage = attackDamage;
    }

    private void HandleAttackInput()
    {
        if (IsTalking) return;

        if ((Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.J)) && Time.time >= nextAttackTime)
            Attack();
    }

    private void Attack()
    {
        nextAttackTime = Time.time + attackCooldown;

        soundCtrl.PlayAtaque();
        if (animator     != null) animator.SetTrigger("Attack");
        if (fxController != null) fxController.PlayAttackSlash(isFacingRight);

        SpawnAttackVFXClientRpc(attackPoint.position, isFacingRight);

        //daño a enemigos en rango
        Collider2D[] hits = Physics2D.OverlapCircleAll(attackPoint.position, attackRange, enemyLayers);
        foreach (Collider2D enemy in hits)
        {
            NetworkObject netObj = enemy.GetComponent<NetworkObject>();
            if (netObj != null)
                ApplyDamageServerRpc(netObj.NetworkObjectId, attackDamage);
            else
            {
                EnemyBase script = enemy.GetComponent<EnemyBase>();
                if (script != null) script.TakeDamage(attackDamage, transform);
            }
        }
    }

    private IEnumerator KnockbackRoutine(Transform source)
    {
        isKnockedBack = true;
        float dir = transform.position.x > source.position.x ? 1f : -1f;
        rb.linearVelocity = Vector2.zero;
        rb.AddForce(new Vector2(knockbackForceX * dir, knockbackForceY), ForceMode2D.Impulse);
        yield return new WaitForSeconds(knockbackDuration);
        isKnockedBack = false;
    }

    //llamados por Player2Controller via ApplyBuffClientRpc

    public void ApplySpeedBuff(float multiplier, float duration)
    {
        if (speedBuffCoroutine != null) StopCoroutine(speedBuffCoroutine);
        speedBuffCoroutine = StartCoroutine(SpeedBuffRoutine(multiplier, duration));
    }

    private IEnumerator SpeedBuffRoutine(float multiplier, float duration)
    {
        walkSpeed = baseWalkSpeed * multiplier;
        if (spriteRenderer != null) spriteRenderer.color = new Color(0.4f, 0.8f, 1f);
        yield return new WaitForSeconds(duration);
        walkSpeed = baseWalkSpeed;
        if (spriteRenderer != null) spriteRenderer.color = Color.white;
        speedBuffCoroutine = null;
    }

    public void ApplyDamageBuff(float multiplier, float duration)
    {
        if (damageBuffCoroutine != null) StopCoroutine(damageBuffCoroutine);
        damageBuffCoroutine = StartCoroutine(DamageBuffRoutine(multiplier, duration));
    }

    private IEnumerator DamageBuffRoutine(float multiplier, float duration)
    {
        attackDamage = Mathf.RoundToInt(baseAttackDamage * multiplier);
        if (spriteRenderer != null) spriteRenderer.color = new Color(1f, 0.5f, 0.2f);
        yield return new WaitForSeconds(duration);
        attackDamage = baseAttackDamage;
        if (spriteRenderer != null) spriteRenderer.color = Color.white;
        damageBuffCoroutine = null;
    }

    private IEnumerator HealFlashRoutine()
    {
        if (spriteRenderer != null) spriteRenderer.color = new Color(0.3f, 1f, 0.4f);
        yield return new WaitForSeconds(0.25f);
        if (speedBuffCoroutine == null && damageBuffCoroutine == null)
            if (spriteRenderer != null) spriteRenderer.color = Color.white;
    }

    private void ResetCombatBuffs()
    {
        if (speedBuffCoroutine  != null) { StopCoroutine(speedBuffCoroutine);  speedBuffCoroutine  = null; }
        if (damageBuffCoroutine != null) { StopCoroutine(damageBuffCoroutine); damageBuffCoroutine = null; }
        walkSpeed    = baseWalkSpeed;
        attackDamage = baseAttackDamage;
        if (spriteRenderer != null) spriteRenderer.color = Color.white;
    }
}
