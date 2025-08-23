using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float walkSpeed = 5f;
    [SerializeField] private float runSpeed = 8f;
    [SerializeField] private float acceleration = 10f;
    [SerializeField] private float deceleration = 15f;
    
    [Header("Input Settings")]
    [SerializeField] private KeyCode runKey = KeyCode.LeftShift;
    [SerializeField] private KeyCode jumpKey = KeyCode.Space;
    [SerializeField] private KeyCode duckKey = KeyCode.LeftControl;
    [SerializeField] private KeyCode attackKey = KeyCode.Z;
    [SerializeField] private KeyCode slideKey = KeyCode.X;
    
    // Components
    private Vector2 inputDirection;
    private Vector2 currentVelocity;
    private Rigidbody2D rb;
    private AnimationController animationController;
    
    // State tracking
    private bool isRunning = false;
    private bool isDucking = false;
    private bool isJumping = false;
    private bool isAttacking = false;
    private bool isSliding = false;
    
    // Action timers
    private float jumpTimer = 0f;
    private float attackTimer = 0f;
    private float slideTimer = 0f;
    
    [Header("Action Durations")]
    [SerializeField] private float jumpDuration = 0.5f;
    [SerializeField] private float attackDuration = 0.6f;
    [SerializeField] private float slideDuration = 0.8f;
    
    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        animationController = GetComponent<AnimationController>();
        
        if (animationController == null)
        {
            animationController = GetComponentInChildren<AnimationController>();
        }
        
        if (animationController == null)
        {
            Debug.LogError($"No AnimationController found on {gameObject.name} or its children!");
        }
        
        // Initialize with idle state
        if (animationController != null)
        {
            animationController.SetMovementDirection(Vector2.zero, 0f);
        }
    }
    
    void Update()
    {
        HandleInput();
        UpdateActionTimers();
        UpdateMovement();
        UpdateAnimations();
    }
    
    void FixedUpdate()
    {
        ApplyMovement();
    }
    
    private void HandleInput()
    {
        // Get movement input
        inputDirection = new Vector2(
            Input.GetAxisRaw("Horizontal"), 
            Input.GetAxisRaw("Vertical")
        ).normalized;
        
        // Handle run input
        bool runPressed = Input.GetKey(runKey);
        if (runPressed != isRunning)
        {
            isRunning = runPressed;
            if (animationController != null)
            {
                animationController.Run(isRunning);
            }
        }
        
        // Handle duck input
        bool duckPressed = Input.GetKey(duckKey);
        if (duckPressed != isDucking)
        {
            isDucking = duckPressed;
            if (animationController != null)
            {
                animationController.Duck(isDucking);
            }
        }
        
        // Handle action inputs (only if not already performing the action)
        if (Input.GetKeyDown(jumpKey) && !isJumping)
        {
            StartJump();
        }
        
        if (Input.GetKeyDown(attackKey) && !isAttacking)
        {
            StartAttack();
        }
        
        if (Input.GetKeyDown(slideKey) && !isSliding)
        {
            StartSlide();
        }
    }
    
    private void UpdateActionTimers()
    {
        // Update jump timer
        if (isJumping)
        {
            jumpTimer -= Time.deltaTime;
            if (jumpTimer <= 0f)
            {
                EndJump();
            }
        }
        
        // Update attack timer
        if (isAttacking)
        {
            attackTimer -= Time.deltaTime;
            if (attackTimer <= 0f)
            {
                EndAttack();
            }
        }
        
        // Update slide timer
        if (isSliding)
        {
            slideTimer -= Time.deltaTime;
            if (slideTimer <= 0f)
            {
                EndSlide();
            }
        }
    }
    
    private void UpdateMovement()
    {
        // Calculate target speed based on run state and ducking
        float targetSpeed = isRunning ? runSpeed : walkSpeed;
        
        // Reduce speed while ducking
        if (isDucking)
        {
            targetSpeed *= 0.5f;
        }
        
        // Calculate target velocity
        Vector2 targetVelocity = inputDirection * targetSpeed;
        
        // Apply acceleration/deceleration
        float accelerationRate = inputDirection.magnitude > 0.1f ? acceleration : deceleration;
        currentVelocity = Vector2.MoveTowards(currentVelocity, targetVelocity, accelerationRate * Time.deltaTime);
    }
    
    private void ApplyMovement()
    {
        // Don't move while sliding (slide handles its own movement)
        if (!isSliding)
        {
            rb.velocity = currentVelocity;
        }
    }
    
    private void UpdateAnimations()
    {
        if (animationController == null) return;
        
        // Calculate animation direction and speed
        Vector2 animDirection = inputDirection;
        float animSpeed = currentVelocity.magnitude / walkSpeed; // Normalize speed for animation
        
        // Update animation controller
        animationController.SetMovementDirection(animDirection, animSpeed);
    }
    
    #region Action Methods
    
    private void StartJump()
    {
        isJumping = true;
        jumpTimer = jumpDuration;
        
        if (animationController != null)
        {
            animationController.Jump();
        }
        
        Debug.Log("Started jump");
    }
    
    private void EndJump()
    {
        isJumping = false;
        
        if (animationController != null)
        {
            animationController.StopJump();
        }
        
        Debug.Log("Ended jump");
    }
    
    private void StartAttack()
    {
        isAttacking = true;
        attackTimer = attackDuration;
        
        if (animationController != null)
        {
            animationController.Attack();
        }
        
        Debug.Log("Started attack");
    }
    
    private void EndAttack()
    {
        isAttacking = false;
        
        if (animationController != null)
        {
            animationController.StopAttack();
        }
        
        Debug.Log("Ended attack");
    }
    
    private void StartSlide()
    {
        isSliding = true;
        slideTimer = slideDuration;
        
        if (animationController != null)
        {
            animationController.Slide();
        }
        
        // Apply slide velocity in the direction we're facing
        Vector2 slideDirection = animationController.GetLastNonZeroDirection();
        rb.velocity = slideDirection * runSpeed * 1.5f; // Slide faster than running
        
        Debug.Log("Started slide");
    }
    
    private void EndSlide()
    {
        isSliding = false;
        
        if (animationController != null)
        {
            animationController.StopSlide();
        }
        
        Debug.Log("Ended slide");
    }
    
    #endregion
    
    #region Public Methods for External Scripts
    
    public bool IsMoving() => currentVelocity.magnitude > 0.1f;
    public bool IsRunning() => isRunning;
    public bool IsDucking() => isDucking;
    public bool IsJumping() => isJumping;
    public bool IsAttacking() => isAttacking;
    public bool IsSliding() => isSliding;
    
    public Vector2 GetMovementDirection() => inputDirection;
    public Vector2 GetVelocity() => currentVelocity;
    public Vector2 GetFacingDirection() => animationController?.GetLastNonZeroDirection() ?? Vector2.down;
    
    // Manual control methods for external scripts
    public void SetMovementInput(Vector2 direction)
    {
        inputDirection = direction.normalized;
    }
    
    public void ForceJump()
    {
        if (!isJumping)
        {
            StartJump();
        }
    }
    
    public void ForceAttack()
    {
        if (!isAttacking)
        {
            StartAttack();
        }
    }
    
    public void ForceSlide()
    {
        if (!isSliding)
        {
            StartSlide();
        }
    }
    
    public void SetFacing(bool faceRight)
    {
        if (animationController != null)
        {
            animationController.SetFacing(faceRight);
        }
    }
    
    #endregion
    
    #region Debug
    
    [ContextMenu("Debug Movement State")]
    public void DebugMovementState()
    {
        Debug.Log("=== Player Movement State ===");
        Debug.Log($"Input Direction: {inputDirection}");
        Debug.Log($"Current Velocity: {currentVelocity}");
        Debug.Log($"Is Running: {isRunning}");
        Debug.Log($"Is Ducking: {isDucking}");
        Debug.Log($"Is Jumping: {isJumping} (Timer: {jumpTimer:F2})");
        Debug.Log($"Is Attacking: {isAttacking} (Timer: {attackTimer:F2})");
        Debug.Log($"Is Sliding: {isSliding} (Timer: {slideTimer:F2})");
    }
    
    #endregion
}