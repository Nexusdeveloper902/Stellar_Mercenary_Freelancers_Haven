using UnityEngine;
using System.Collections.Generic;

public class AnimationController : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private Animator animator;
    [SerializeField] private SpriteRenderer spriteRenderer;
    
    [Header("Animation Settings")]
    [SerializeField] private float baseAnimationSpeed = 1.0f;
    [SerializeField] private bool spritesDrawnFacingLeft = false;
    
    [Header("Blend Tree Parameters")]
    [SerializeField] private string horizontalParam = "Horizontal";
    [SerializeField] private string verticalParam = "Vertical";
    [SerializeField] private string speedParam = "Speed";
    [SerializeField] private string isMovingParam = "IsMoving";
    
    [Header("State Parameters")]
    [SerializeField] private string isJumpingParam = "IsJumping";
    [SerializeField] private string isDuckingParam = "IsDucking";
    [SerializeField] private string isRunningParam = "IsRunning";
    [SerializeField] private string isAttackingParam = "IsAttacking";
    [SerializeField] private string isSlidingParam = "IsSliding";
    
    [Header("Trigger Parameters")]
    [SerializeField] private string jumpTrigger = "Jump";
    [SerializeField] private string attackTrigger = "Attack";
    [SerializeField] private string slideTrigger = "Slide";
    
    // Cached parameter hashes for performance
    private Dictionary<string, int> parameterHashes;
    
    // Current state tracking
    private Vector2 currentDirection = Vector2.zero;
    private Vector2 lastNonZeroDirection = Vector2.down;
    private bool facingRight = false;
    private float currentSpeed = 0f;
    
    // Animation speed multipliers for different states
    [System.Serializable]
    public class AnimationSpeeds
    {
        [Header("Movement Speeds")]
        public float idle = 1.0f;
        public float walk = 1.5f;
        public float run = 2.0f;
        
        [Header("Action Speeds")]
        public float jump = 1.2f;
        public float duck = 1.0f;
        public float attack = 1.2f;
        public float slide = 1.8f;
    }
    
    [SerializeField] private AnimationSpeeds animationSpeeds = new AnimationSpeeds();
    
    void Awake()
    {
        // Auto-assign components if not set
        if (animator == null)
            animator = GetComponent<Animator>();
            
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();
        
        // Initialize parameter hashes for performance
        InitializeParameterHashes();
        
        // Set initial animator speed
        animator.speed = baseAnimationSpeed;
        
        // Validate animator controller
        if (animator.runtimeAnimatorController == null)
        {
            Debug.LogError($"No AnimatorController assigned to {gameObject.name}! Please assign one in the Animator component.");
        }
        else
        {
            Debug.Log($"✅ AnimationController initialized for {gameObject.name}");
        }
    }
    
    void Start()
    {
        // Set initial idle state
        SetMovementDirection(Vector2.zero, 0f);
    }
    
    private void InitializeParameterHashes()
    {
        parameterHashes = new Dictionary<string, int>();
        
        // Add all parameter names to the hash dictionary
        string[] parameterNames = {
            horizontalParam, verticalParam, speedParam, isMovingParam,
            isJumpingParam, isDuckingParam, isRunningParam, isAttackingParam, isSlidingParam,
            jumpTrigger, attackTrigger, slideTrigger
        };
        
        foreach (string paramName in parameterNames)
        {
            if (!string.IsNullOrEmpty(paramName))
            {
                parameterHashes[paramName] = Animator.StringToHash(paramName);
            }
        }
        
        Debug.Log($"Initialized {parameterHashes.Count} animator parameter hashes");
    }
    
    #region Public Movement Methods
    
    /// <summary>
    /// Set the movement direction and speed for blend tree animations
    /// </summary>
    public void SetMovementDirection(Vector2 direction, float speed)
    {
        currentDirection = direction.normalized;
        currentSpeed = speed;
        
        // Update last non-zero direction for idle animations
        if (direction.magnitude > 0.1f)
        {
            lastNonZeroDirection = currentDirection;
        }
        
        // Handle sprite flipping
        HandleSpriteFlipping(currentDirection);
        
        // Update animator parameters
        UpdateMovementParameters();
        
        // Set appropriate animation speed
        UpdateAnimationSpeed();
    }
    
    /// <summary>
    /// Set movement using just direction (speed will be 1.0 if moving, 0 if not)
    /// </summary>
    public void SetMovementDirection(Vector2 direction)
    {
        float speed = direction.magnitude > 0.1f ? 1.0f : 0f;
        SetMovementDirection(direction, speed);
    }
    
    #endregion
    
    #region Public Action Methods
    
    public void Jump()
    {
        SetBool(isJumpingParam, true);
        SetTrigger(jumpTrigger);
        SetAnimationSpeed(animationSpeeds.jump);
        Debug.Log("🦘 Jump triggered");
    }
    
    public void StopJump()
    {
        SetBool(isJumpingParam, false);
        UpdateAnimationSpeed(); // Reset to appropriate movement speed
    }
    
    public void Duck(bool isDucking)
    {
        SetBool(isDuckingParam, isDucking);
        SetAnimationSpeed(isDucking ? animationSpeeds.duck : GetCurrentMovementSpeed());
        Debug.Log($"🦆 Duck: {isDucking}");
    }
    
    public void Run(bool isRunning)
    {
        SetBool(isRunningParam, isRunning);
        UpdateAnimationSpeed();
        Debug.Log($"🏃 Run: {isRunning}");
    }
    
    public void Attack()
    {
        SetBool(isAttackingParam, true);
        SetTrigger(attackTrigger);
        SetAnimationSpeed(animationSpeeds.attack);
        Debug.Log("⚔️ Attack triggered");
    }
    
    public void StopAttack()
    {
        SetBool(isAttackingParam, false);
        UpdateAnimationSpeed(); // Reset to appropriate movement speed
    }
    
    public void Slide()
    {
        SetBool(isSlidingParam, true);
        SetTrigger(slideTrigger);
        SetAnimationSpeed(animationSpeeds.slide);
        Debug.Log("🛷 Slide triggered");
    }
    
    public void StopSlide()
    {
        SetBool(isSlidingParam, false);
        UpdateAnimationSpeed(); // Reset to appropriate movement speed
    }
    
    #endregion
    
    #region Animation Speed Control
    
    public void SetAnimationSpeed(float speed)
    {
        animator.speed = speed * baseAnimationSpeed;
    }
    
    public void SetBaseAnimationSpeed(float baseSpeed)
    {
        baseAnimationSpeed = baseSpeed;
        UpdateAnimationSpeed();
    }
    
    private void UpdateAnimationSpeed()
    {
        float targetSpeed = GetCurrentMovementSpeed();
        SetAnimationSpeed(targetSpeed);
    }
    
    private float GetCurrentMovementSpeed()
    {
        // Check for special states first
        if (GetBool(isRunningParam) && currentSpeed > 0.1f)
            return animationSpeeds.run;
        else if (currentSpeed > 0.1f)
            return animationSpeeds.walk;
        else
            return animationSpeeds.idle;
    }
    
    #endregion
    
    #region Sprite Flipping
    
    public void SetFacing(bool faceRight)
    {
        if (spriteRenderer == null) return;
        
        facingRight = faceRight;
        
        // Flip logic depends on how your sprites are drawn
        if (spritesDrawnFacingLeft)
        {
            spriteRenderer.flipX = facingRight;
        }
        else
        {
            spriteRenderer.flipX = !facingRight;
        }
        
        Debug.Log($"👀 Set facing: facingRight={facingRight}, flipX={spriteRenderer.flipX}");
    }
    
    // Replace the entire old method with this new one.
    private void HandleSpriteFlipping(Vector2 direction)
    {
        if (spriteRenderer == null) return;

        // We only care about the horizontal component to decide the flip direction.
        // If there's any significant horizontal input, we should update the facing direction.
        if (Mathf.Abs(direction.x) > 0.01f)
        {
            // Determine if we should be facing right based on the sign of the x input.
            bool shouldFaceRight = direction.x > 0;

            // Only call the SetFacing method if the direction has actually changed.
            if (facingRight != shouldFaceRight)
            {
                SetFacing(shouldFaceRight);
            }
        }
    }
    
    public bool IsFacingRight()
    {
        return facingRight;
    }
    
    #endregion
    
    #region Parameter Update Methods
    
    private void UpdateMovementParameters()
    {
        Vector2 blendDirection = lastNonZeroDirection;
        
        if (currentDirection.magnitude > 0.1f)
        {
            blendDirection = currentDirection;
        }
        
        SetFloat(horizontalParam, blendDirection.x);
        SetFloat(verticalParam, blendDirection.y);
        SetFloat(speedParam, currentSpeed);
        SetBool(isMovingParam, currentSpeed > 0.1f);
    }
    
    #endregion
    
    #region Animator Parameter Helpers
    
    private void SetFloat(string paramName, float value)
    {
        if (parameterHashes.ContainsKey(paramName))
        {
            animator.SetFloat(parameterHashes[paramName], value);
        }
        else if (!string.IsNullOrEmpty(paramName))
        {
            Debug.LogWarning($"Parameter '{paramName}' not found in animator or hash dictionary");
        }
    }
    
    private void SetBool(string paramName, bool value)
    {
        if (parameterHashes.ContainsKey(paramName))
        {
            animator.SetBool(parameterHashes[paramName], value);
        }
        else if (!string.IsNullOrEmpty(paramName))
        {
            Debug.LogWarning($"Parameter '{paramName}' not found in animator or hash dictionary");
        }
    }
    
    private void SetTrigger(string paramName)
    {
        if (parameterHashes.ContainsKey(paramName))
        {
            animator.SetTrigger(parameterHashes[paramName]);
        }
        else if (!string.IsNullOrEmpty(paramName))
        {
            Debug.LogWarning($"Parameter '{paramName}' not found in animator or hash dictionary");
        }
    }
    
    private bool GetBool(string paramName)
    {
        if (parameterHashes.ContainsKey(paramName))
        {
            return animator.GetBool(parameterHashes[paramName]);
        }
        return false;
    }
    
    private float GetFloat(string paramName)
    {
        if (parameterHashes.ContainsKey(paramName))
        {
            return animator.GetFloat(parameterHashes[paramName]);
        }
        return 0f;
    }
    
    #endregion
    
    #region Debug Methods
    
    [ContextMenu("Debug Current State")]
    public void DebugCurrentState()
    {
        Debug.Log("=== Animation Controller State ===");
        Debug.Log($"Current Direction: {currentDirection}");
        Debug.Log($"Last Non-Zero Direction: {lastNonZeroDirection}");
        Debug.Log($"Current Speed: {currentSpeed}");
        Debug.Log($"Facing Right: {facingRight}");
        Debug.Log($"Is Moving: {GetBool(isMovingParam)}");
        Debug.Log($"Horizontal: {GetFloat(horizontalParam)}");
        Debug.Log($"Vertical: {GetFloat(verticalParam)}");
        Debug.Log($"Speed: {GetFloat(speedParam)}");
    }
    
    [ContextMenu("List All Parameters")]
    public void ListAllParameters()
    {
        Debug.Log("=== Animator Parameters ===");
        foreach (var kvp in parameterHashes)
        {
            Debug.Log($"  {kvp.Key} -> Hash: {kvp.Value}");
        }
    }
    
    #endregion
    
    #region Getters for External Scripts
    
    public Vector2 GetCurrentDirection() => currentDirection;
    public Vector2 GetLastNonZeroDirection() => lastNonZeroDirection;
    public float GetCurrentSpeed() => currentSpeed;
    public bool IsMoving() => currentSpeed > 0.1f;
    
    #endregion
}