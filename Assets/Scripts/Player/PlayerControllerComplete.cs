using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(CapsuleCollider2D))]
public class PlayerControllerComplete : MonoBehaviour
{
    [Header("UI del Jugador")]
    public Image healthImage;       
    public Image dashCooldownImage; 
    public Text dashChargesText;    

    [Header("Controlador de Sonidos")]
    public PlayerSoundController soundCtrl;

    [Header("Animator")]
    public Animator animator;

    [Header("Movimiento Horizontal")]
    [SerializeField] private float walkSpeed = 5f;

    [Header("Mecánica de Sprint / Dash")]
    [SerializeField] private float sprintSpeed     = 13f;
    [SerializeField] private float sprintDuration  = 0.3f;
    [SerializeField] private int   maxSprintCharges = 3;
    [SerializeField] private float sprintCooldown  = 1.5f; 

    [Header("Salto y Doble Salto")]
    [SerializeField] private float jumpForce         = 7f;
    [SerializeField] private int   maxJumps          = 2;
    [SerializeField] private float jumpHoldForce     = 25f;
    [SerializeField] private float jumpHoldMaxTime   = 0.2f;
    [SerializeField] private float jumpCutMultiplier = 0.4f;

    [Header("Agacharse")]
    [SerializeField] private float crouchSpeedMultiplier = 0.5f;
    [SerializeField] private float crouchHeightMultiplier = 0.55f;

    [Header("Mecánica de Paredes")]
    [SerializeField] private float   wallSlideSpeed    = 2f;
    [SerializeField] private float   wallSlideDuration = 1.2f;
    [SerializeField] private Vector2 wallJumpForce     = new Vector2(10f, 15f);
    [SerializeField] private float   wallJumpDuration  = 0.2f;

    [Header("Detección")]
    [SerializeField] private Transform groundCheck;
    [SerializeField] private Transform wallCheck;
    [SerializeField] private float     checkRadius = 0.2f;
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private LayerMask wallLayer;

    [Header("Combate")]
    [SerializeField] private Transform attackPoint;
    [SerializeField] private float     attackRange  = 0.6f;
    [SerializeField] private LayerMask enemyLayers;
    [SerializeField] private int       attackDamage = 40;
    
    //Tiempo que tarda el jugador en poder volver a atacar
    [Header("Configuración de Balance (Combate)")]
    public float attackCooldown = 0.5f; 
    private float nextAttackTime = 0f;

    [Header("Sistema de Vida")]
    [SerializeField] private int maxHealth = 100;
    [SerializeField] private float voidYThreshold = -20f;

    [Header("Knockback")]
    [SerializeField] private float knockbackForceX = 8f;
    [SerializeField] private float knockbackForceY = 5f;
    [SerializeField] private float knockbackDuration = 0.2f;

    private Rigidbody2D       rb;
    private CapsuleCollider2D capsuleCollider;

    private float horizontalInput;
    private bool  isFacingRight = true;

    private bool isGrounded;
    private bool isTouchingWall;

    private bool isWallSliding;
    private bool isWallJumping;
    private bool isSprinting;
    private bool isCrouching;
    private bool isKnockedBack = false;

    private float wallSlideTimer;
    private bool  wallSlideExhausted;

    private int   jumpsRemaining;
    private int   currentSprintCharges;
    private float sprintCooldownTimer;
    private float currentDashDirection; 

    private bool  jumpHeld;
    private float jumpHoldTimer;
    private bool  isJumping;

    private int   currentHealth;
    private bool isDead;

    private Vector3 spawnPosition;
    private float   originalColliderHeight;
    private float   originalColliderOffsetY;
    public bool isTalking = false;
    private PlayerEffectsController fxController;
    
    void Start()
    {
        rb              = GetComponent<Rigidbody2D>();
        capsuleCollider = GetComponent<CapsuleCollider2D>();

        currentSprintCharges = maxSprintCharges;
        sprintCooldownTimer  = sprintCooldown; 
        jumpsRemaining       = maxJumps;

        originalColliderHeight  = capsuleCollider.size.y;
        originalColliderOffsetY = capsuleCollider.offset.y;
        fxController = GetComponent<PlayerEffectsController>();

        //
        if (SaveSystem.instance != null && SaveSystem.instance.pendingLoad != null)
        {
            SaveData data = SaveSystem.instance.pendingLoad;
            
            //Restaura posición
            transform.position = new Vector3(data.playerX, data.playerY, transform.position.z);
            
            //Restaura vida
            currentHealth = data.playerHealth;
            
            Debug.Log("Partida Cargada - Jugador posicionado en: " + transform.position + " con vida: " + currentHealth);
        }
        else
        {
            currentHealth = maxHealth;
        }

        spawnPosition = transform.position;
    }

    void Update()
    {
        if (isDead || (GameManager.instance != null && GameManager.instance.isPaused)) return;

        if (isTalking)
        {
            horizontalInput = 0; 
            rb.linearVelocity = new Vector2(0, rb.linearVelocity.y); 
            UpdateAnimations(); 
            return; 
        }

        ReadInputs();
        CheckSurroundings();
        HandleCrouch();
        DetermineWallSlideState();
        HandleJumpInput();
        HandleAttackInput();
        UpdateAnimations();

        //Lógica del Cooldown del Dash
        if (currentSprintCharges < maxSprintCharges)
        {
            sprintCooldownTimer -= Time.deltaTime;
            
            if (sprintCooldownTimer <= 0f)
            {
                currentSprintCharges++; 
                sprintCooldownTimer = sprintCooldown; 
            }
        }

        //Resetea saltos al aterrizar
        if (isGrounded && rb.linearVelocity.y <= 0.1f)
        {
            jumpsRemaining = maxJumps;
            isJumping      = false;
            if (animator != null) animator.ResetTrigger("DoubleJump");
        }

        //Corte de salto
        if (!jumpHeld && isJumping && rb.linearVelocity.y > 0)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, rb.linearVelocity.y * jumpCutMultiplier);
            isJumping = false;
        }

        if (healthImage != null)
        {
            healthImage.fillAmount = (float)currentHealth / maxHealth;
        }

        if (dashChargesText != null)
        {
            dashChargesText.text = currentSprintCharges.ToString();
        }

        if (dashCooldownImage != null)
        {
            if (currentSprintCharges == maxSprintCharges)
            {
                dashCooldownImage.fillAmount = 0f; 
            }
            else
            {
                dashCooldownImage.fillAmount = sprintCooldownTimer / sprintCooldown;
            }
        }
    }

    void FixedUpdate()
    {
        if (isDead || (GameManager.instance != null && GameManager.instance.isPaused) || isTalking) return;

        Move();
        ApplyJumpHold();
    }

    private void ReadInputs()
    {
        horizontalInput = Input.GetAxisRaw("Horizontal");
        
        if (isTalking) return;

        //Sprint con clic derecho o con la tecla shift izquierda
        if ((Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.LeftShift)) && currentSprintCharges > 0 && !isSprinting)
        {
            if (currentSprintCharges == maxSprintCharges)
            {
                sprintCooldownTimer = sprintCooldown;
            }

            if (horizontalInput != 0)
            {
                currentDashDirection = Mathf.Sign(horizontalInput);
            }
            else
            {
                currentDashDirection = isFacingRight ? 1f : -1f;
            }
            
            StartCoroutine(SprintRoutine());
        }

        jumpHeld = Input.GetKey(KeyCode.Space) || Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow);

        if (!isWallJumping)
        {
            if      (horizontalInput > 0 && !isFacingRight) Flip();
            else if (horizontalInput < 0 &&  isFacingRight) Flip();
        }
    }

    private void HandleJumpInput()
    {
        bool jumpPressed = Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow);

        if (!jumpPressed) return;

        if (isWallSliding)
        {
            WallJump();
            jumpsRemaining = maxJumps - 1;
        }
        else if (jumpsRemaining > 0)
        {
            if (animator != null)
            {
                if (jumpsRemaining == maxJumps) animator.SetTrigger("Jump");
                else animator.SetTrigger("DoubleJump");
            }

            PerformJump(jumpForce);
            jumpsRemaining--;
        }
    }

    private void HandleAttackInput()
    {
        if (isTalking) return; 

        //Detecta el botón de golpe
        if (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.J))
        {

            if (Time.time >= nextAttackTime)
            {
                Attack();
            }
        }
    }

    private void HandleCrouch()
    {
        bool crouchInput = Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow);

        if (crouchInput && isGrounded)
        {
            if (!isCrouching) EnterCrouch();
        }
        else
        {
            if (isCrouching) ExitCrouch();
        }
    }

    private void EnterCrouch()
    {
        if (isCrouching) return;
        isCrouching = true;

        float newHeight  = originalColliderHeight * crouchHeightMultiplier;
        float baseY      = originalColliderOffsetY - originalColliderHeight / 2f;
        float newOffsetY = baseY + newHeight / 2f;
        float correctionY = 0.55f;

        capsuleCollider.size   = new Vector2(capsuleCollider.size.x, newHeight);
        capsuleCollider.offset = new Vector2(capsuleCollider.offset.x, newOffsetY - correctionY);

        if (animator != null) animator.SetBool("IsCrouching", true);
    }

    private void ExitCrouch()
    {
        if (!isCrouching) return;
        isCrouching = false;

        capsuleCollider.size   = new Vector2(capsuleCollider.size.x, originalColliderHeight);
        capsuleCollider.offset = new Vector2(capsuleCollider.offset.x, originalColliderOffsetY);

        if (animator != null) animator.SetBool("IsCrouching", false);
    }

    private void CheckSurroundings()
    {
        isGrounded     = Physics2D.OverlapCircle(groundCheck.position, checkRadius, groundLayer);
        isTouchingWall = Physics2D.OverlapCircle(wallCheck.position,   checkRadius, wallLayer);
    }

    private void Move()
    {
        if (isWallJumping || isKnockedBack) return;

        if (isSprinting)
        {
            rb.linearVelocity = new Vector2(currentDashDirection * sprintSpeed, rb.linearVelocity.y);
        }
        else
        {
            float speed = isCrouching ? walkSpeed * crouchSpeedMultiplier : walkSpeed;
            rb.linearVelocity = new Vector2(horizontalInput * speed, rb.linearVelocity.y);
        }

        if (isWallSliding && rb.linearVelocity.y < -wallSlideSpeed)
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, -wallSlideSpeed);
    }

    private void PerformJump(float force)
    {
        soundCtrl.PlaySalto();
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f);
        rb.AddForce(Vector2.up * force, ForceMode2D.Impulse);
        isJumping     = true;
        jumpHoldTimer = 0f;
        if (fxController != null) fxController.PlayJumpDust();
    }

    private void ApplyJumpHold()
    {
        if (!isJumping || !jumpHeld || jumpHoldTimer >= jumpHoldMaxTime) return;

        jumpHoldTimer += Time.fixedDeltaTime;
        rb.AddForce(Vector2.up * jumpHoldForce * Time.fixedDeltaTime, ForceMode2D.Force);
    }

    private void DetermineWallSlideState()
    {
        bool canSlide = isTouchingWall && !isGrounded && horizontalInput != 0;

        if (canSlide)
        {
            float directionToWall = isFacingRight ? 1f : -1f;
            canSlide = Mathf.Sign(horizontalInput) == directionToWall;
        }

        if (canSlide && !wallSlideExhausted)
        {
            if (!isWallSliding)
            {
                wallSlideTimer = wallSlideDuration;
            }

            wallSlideTimer -= Time.deltaTime;

            if (wallSlideTimer <= 0f)
            {
                wallSlideExhausted = true;
                isWallSliding = false;
            }
            else
            {
                isWallSliding = true;
            }
        }
        else
        {
            isWallSliding = false;
        }

        if (isGrounded)
        {
            wallSlideExhausted = false;
            wallSlideTimer     = wallSlideDuration;
        }
    }

    private void WallJump()
    {
        isWallSliding = false;
        Flip();
        Vector2 force = new Vector2(wallJumpForce.x * (isFacingRight ? 1 : -1), wallJumpForce.y);
        rb.linearVelocity = Vector2.zero;
        rb.AddForce(force, ForceMode2D.Impulse);
        isJumping     = true;
        jumpHoldTimer = 0f;
        StartCoroutine(WallJumpRoutine());
        if (animator != null) animator.SetTrigger("Jump");
    }

    private IEnumerator WallJumpRoutine()
    {
        isWallJumping = true;
        yield return new WaitForSeconds(wallJumpDuration);
        isWallJumping = false;
    }

    private void Attack()
    {
        nextAttackTime = Time.time + attackCooldown;

        soundCtrl.PlayAtaque();
        if (animator != null) animator.SetTrigger("Attack");
        if (fxController != null) fxController.PlayAttackSlash(isFacingRight);

        Collider2D[] hits = Physics2D.OverlapCircleAll(attackPoint.position, attackRange, enemyLayers);
        foreach (Collider2D enemy in hits)
        {
            EnemyBase enemyScript = enemy.GetComponent<EnemyBase>();
            if (enemyScript != null)
            {
                enemyScript.TakeDamage(attackDamage, transform); 
            }
        }
    }

    public void TakeDamage(int damage, Transform damageSource = null)
    {
        soundCtrl.PlayRecibirDano();
        if (isDead) return;

        currentHealth = Mathf.Max(0, currentHealth - damage);
        if (animator != null) animator.SetTrigger("Hurt");

        if (damageSource != null) StartCoroutine(KnockbackRoutine(damageSource));

        if (currentHealth <= 0) Die();
    }

    private IEnumerator KnockbackRoutine(Transform source)
    {
        isKnockedBack = true;
        float direction = transform.position.x > source.position.x ? 1f : -1f;
        rb.linearVelocity = Vector2.zero;
        rb.AddForce(new Vector2(knockbackForceX * direction, knockbackForceY), ForceMode2D.Impulse);

        yield return new WaitForSeconds(knockbackDuration);
        isKnockedBack = false;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if(collision.gameObject.tag == "Damage")
        {
            TakeDamage(5, collision.transform);
        }
    }

    private void Die()
    {
        soundCtrl.PlayMuerte();
        isDead = true;

        
        if (GameManager.instance != null)
        {
            GameManager.instance.ModificarTiempo(90f); 
        }

        if (animator != null) animator.SetTrigger("Die");
        rb.linearVelocity = Vector2.zero;
        StartCoroutine(DeathRespawnRoutine());
    }

    private IEnumerator DeathRespawnRoutine()
    {
        yield return new WaitForSeconds(1.5f);
        Respawn();
    }

    private void CheckVoid()
    {
        if (transform.position.y < voidYThreshold) Respawn();
    }

    private void Respawn()
    {
        isDead         = false;
        currentHealth  = maxHealth;
        jumpsRemaining = maxJumps;
        isJumping      = false;
        isCrouching    = false;
        
        currentSprintCharges = maxSprintCharges;
        sprintCooldownTimer  = sprintCooldown;

        capsuleCollider.size   = new Vector2(capsuleCollider.size.x, originalColliderHeight);
        capsuleCollider.offset = new Vector2(capsuleCollider.offset.x, originalColliderOffsetY);

        rb.linearVelocity  = Vector2.zero;
        transform.position = spawnPosition;

        if (animator != null)
        {
            animator.SetBool("IsCrouching", false);
            animator.SetBool("IsFalling",   false);
            animator.Play("idle");          
        }
    }

    private IEnumerator SprintRoutine()
    {
        soundCtrl.PlayDash();
        isSprinting = true;
        currentSprintCharges--; 
        yield return new WaitForSeconds(sprintDuration);
        isSprinting = false;
    }

    private void Flip()
    {
        isFacingRight = !isFacingRight;
        transform.localScale = new Vector3(
            -Mathf.Abs(transform.localScale.x) * (isFacingRight ? -1 : 1),
            transform.localScale.y,
            transform.localScale.z);
    }

    private void UpdateAnimations()
    {
        if (animator == null) return;

        bool isFalling = !isGrounded && rb.linearVelocity.y < -0.1f;

        animator.SetFloat("movement",   Mathf.Abs(horizontalInput));
        animator.SetBool("IsGrounded",  isGrounded);
        animator.SetFloat("yVelocity",  rb.linearVelocity.y);
        animator.SetBool("IsFalling",   isFalling);
        animator.SetBool("IsCrouching", isCrouching);
    }

    public int  CurrentHealth => currentHealth;
    public int  MaxHealth     => maxHealth;
    public bool IsDead        => isDead;
    public int  CurrentSprintCharges => currentSprintCharges; 

    private void OnDrawGizmosSelected()
    {
        if (groundCheck  != null) { Gizmos.color = Color.green;  Gizmos.DrawWireSphere(groundCheck.position,  checkRadius); }
        if (wallCheck    != null) { Gizmos.color = Color.blue;   Gizmos.DrawWireSphere(wallCheck.position,    checkRadius); }
        if (attackPoint  != null) { Gizmos.color = Color.yellow; Gizmos.DrawWireSphere(attackPoint.position,  attackRange); }

        Gizmos.color = Color.red;
        Gizmos.DrawLine(new Vector3(-100, voidYThreshold, 0), new Vector3(100, voidYThreshold, 0));
    }
}