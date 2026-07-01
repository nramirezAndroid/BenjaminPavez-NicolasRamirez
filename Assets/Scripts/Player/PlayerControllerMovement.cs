using System.Collections;
using UnityEngine;

//caminar, sprint/dash, salto, doble salto, wall slide/jump, agacharse, flip.
public partial class PlayerController
{
    [Header("Movimiento Horizontal")]
    [SerializeField] private float walkSpeed = 5f;

    [Header("Mecánica de Sprint / Dash")]
    [SerializeField] private float sprintSpeed      = 13f;
    [SerializeField] private float sprintDuration   = 0.3f;
    [SerializeField] private int   maxSprintCharges = 1;
    [SerializeField] private float sprintCooldown   = 1.5f;

    [Header("Salto y Doble Salto")]
    [SerializeField] private float jumpForce         = 7f;
    [SerializeField] private int   maxJumps          = 2;
    [SerializeField] private float jumpHoldForce     = 25f;
    [SerializeField] private float jumpHoldMaxTime   = 0.2f;
    [SerializeField] private float jumpCutMultiplier = 0.4f;

    [Header("Mecánica de Paredes")]
    [SerializeField] private float   wallSlideSpeed   = 3f;   //velocidad de caída constante al deslizarse
    [SerializeField] private Vector2 wallJumpForce    = new Vector2(10f, 15f);
    [SerializeField] private float   wallJumpDuration = 0.2f;

    [Header("Detección")]
    [SerializeField] private Transform groundCheck;
    [SerializeField] private Transform wallCheck;
    [SerializeField] private float     checkRadius = 0.2f;
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private LayerMask wallLayer;

    private Rigidbody2D       rb;
    private CapsuleCollider2D capsuleCollider;

    private float horizontalInput;
    private bool  isFacingRight = true;

    private bool isGrounded;
    private bool isTouchingWall;

    private bool isWallSliding;
    private bool isWallJumping;
    private bool isSprinting;
    private bool isKnockedBack;

    private int   jumpsRemaining;
    private int   currentSprintCharges;
    private float sprintCooldownTimer;
    private float currentDashDirection;

    private bool  jumpHeld;
    private float jumpHoldTimer;
    private bool  isJumping;

    private Vector3 spawnPosition;

    private void InitMovement()
    {
        rb              = GetComponent<Rigidbody2D>();
        capsuleCollider = GetComponent<CapsuleCollider2D>();

        currentSprintCharges = maxSprintCharges;
        sprintCooldownTimer  = sprintCooldown;
        jumpsRemaining       = maxJumps;
    }

    private void ReadInputs()
    {
        horizontalInput = Input.GetAxisRaw("Horizontal");

        if ((Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.LeftShift)) && currentSprintCharges > 0 && !isSprinting)
        {
            if (currentSprintCharges == maxSprintCharges)
                sprintCooldownTimer = sprintCooldown;

            currentDashDirection = horizontalInput != 0
                ? Mathf.Sign(horizontalInput)
                : (isFacingRight ? 1f : -1f);

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
        bool jumpPressed = Input.GetKeyDown(KeyCode.Space)
                        || Input.GetKeyDown(KeyCode.W)
                        || Input.GetKeyDown(KeyCode.UpArrow);
        if (!jumpPressed) return;

        if (isWallSliding)
        {
            WallJump();
            jumpsRemaining = maxJumps - 1;
        }
        else if (jumpsRemaining > 0)
        {
            if (animator != null)
                animator.SetTrigger(jumpsRemaining == maxJumps ? "Jump" : "DoubleJump");
            PerformJump(jumpForce);
            jumpsRemaining--;
        }
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

    private void UpdateJumpState()
    {
        if (isGrounded && rb.linearVelocity.y <= 0.1f)
        {
            jumpsRemaining = maxJumps;
            isJumping      = false;
            if (animator != null) animator.ResetTrigger("DoubleJump");
        }

        if (!jumpHeld && isJumping && rb.linearVelocity.y > 0)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, rb.linearVelocity.y * jumpCutMultiplier);
            isJumping = false;
        }
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
            float speed = walkSpeed;
            rb.linearVelocity = new Vector2(horizontalInput * speed, rb.linearVelocity.y);
        }

        //deslizamiento por pared: fuerza velocidad Y constante hacia abajo (inmediato)
        if (isWallSliding)
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, -wallSlideSpeed);
    }

    private void DetermineWallSlideState()
    {
        // Solo detección por capa: wallCheck debe superponerse con wallLayer.
        // Esto garantiza que superficies en otras capas (ej: Suelo/limitMap)
        // nunca activen el wall-slide, aunque bloqueen físicamente al jugador.
        isTouchingWall = wallCheck != null &&
            Physics2D.OverlapCircle(wallCheck.position, checkRadius, wallLayer);

        bool canSlide = isTouchingWall && !isGrounded && Mathf.Abs(horizontalInput) > 0.1f;

        // Verificar que el jugador esté empujando hacia la pared detectada
        if (canSlide)
        {
            float dirToWall = isFacingRight ? 1f : -1f;
            canSlide = Mathf.Sign(horizontalInput) == dirToWall;
        }

        isWallSliding = canSlide;
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

    private IEnumerator SprintRoutine()
    {
        soundCtrl.PlayDash();
        isSprinting = true;
        currentSprintCharges--;
        yield return new WaitForSeconds(sprintDuration);
        isSprinting = false;
    }

    private void UpdateSprintCooldown()
    {
        if (currentSprintCharges < maxSprintCharges)
        {
            sprintCooldownTimer -= Time.deltaTime;
            if (sprintCooldownTimer <= 0f)
            {
                currentSprintCharges++;
                sprintCooldownTimer = sprintCooldown;
            }
        }
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
        animator.SetBool ("IsGrounded", isGrounded);
        animator.SetFloat("yVelocity",  rb.linearVelocity.y);
        animator.SetBool ("IsFalling",  isFalling);

        //sincroniza animaciones a P2 / otros clientes
        SyncAnimationsServerRpc(Mathf.Abs(horizontalInput), isGrounded, rb.linearVelocity.y, isFalling, isFacingRight);
    }
}
