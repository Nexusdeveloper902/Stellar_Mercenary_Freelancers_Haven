using UnityEngine;
using System.Collections;

public class PlayerMovement : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float walkSpeed = 5f;
    [SerializeField] private float runSpeed = 8f;
    [SerializeField] private float acceleration = 10f;
    [SerializeField] private float deceleration = 20f;

    [Header("Jump Settings")]
    [SerializeField] private float jumpSpeedBoost = 4f;
    [SerializeField] private float jumpDuration = 0.4f;

    [Header("Input Settings")]
    [SerializeField] private KeyCode runKey = KeyCode.LeftShift;
    [SerializeField] private KeyCode jumpKey = KeyCode.Space;
    [SerializeField] private KeyCode duckKey = KeyCode.LeftControl;
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
    private bool isSliding = false;
    private bool isMovementLocked = false;

    private Vector2 slideVelocity;
    private float jumpTimer = 0f;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        animationController = GetComponentInChildren<AnimationController>();
        if (animationController == null) Debug.LogError($"No AnimationController found on {gameObject.name} or its children!");
        else animationController.SetMovementDirection(Vector2.zero, 0f);
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
        if (isSliding) rb.velocity = slideVelocity;
        else ApplyMovement();
    }

    private void HandleInput()
    {
        if (!isDucking && !isSliding && !isMovementLocked)
        {
            inputDirection = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")).normalized;
        }
        else
        {
            inputDirection = Vector2.zero;
        }

        isRunning = Input.GetKey(runKey);
        bool duckPressed = Input.GetKey(duckKey);
        isDucking = duckPressed && !isSliding;

        if (animationController != null)
        {
            animationController.Run(isRunning);
            animationController.Duck(isDucking);
        }

        if (Input.GetKeyDown(jumpKey) && !isJumping && !isSliding && !isMovementLocked && !isDucking) StartJump();
        if (Input.GetKeyDown(slideKey) && !isSliding && IsMoving() && !isMovementLocked) StartSlide();
    }

    private void UpdateActionTimers()
    {
        if (isJumping)
        {
            jumpTimer -= Time.deltaTime;
            if (jumpTimer <= 0f) EndJump();
        }
    }

    private void UpdateMovement()
    {
        if (isSliding)
        {
            currentVelocity = rb.velocity;
            return;
        }

        if (isDucking)
        {
            currentVelocity = Vector2.MoveTowards(currentVelocity, Vector2.zero, deceleration * Time.deltaTime);
            return;
        }

        float baseSpeed = isRunning ? runSpeed : walkSpeed;
        float targetSpeed = baseSpeed;

        if (isJumping)
        {
            targetSpeed += jumpSpeedBoost;
        }

        Vector2 targetVelocity = inputDirection * targetSpeed;
        float rate = (inputDirection.magnitude > 0.1f) ? acceleration : deceleration;
        currentVelocity = Vector2.MoveTowards(currentVelocity, targetVelocity, rate * Time.deltaTime);
    }

    private void ApplyMovement() { rb.velocity = currentVelocity; }

    private void UpdateAnimations()
    {
        if (animationController == null) return;
        animationController.SetMovementDirection(inputDirection, currentVelocity.magnitude / walkSpeed);
    }

    #region Action Methods
    private void StartJump()
    {
        isJumping = true;
        jumpTimer = jumpDuration;
        if (animationController != null) animationController.Jump();
    }

    private void EndJump()
    {
        isJumping = false;
        if (animationController != null) animationController.StopJump();
    }

    private void StartSlide()
    {
        isSliding = true;
        isMovementLocked = true;
        isDucking = false;

        if (animationController != null)
        {
            animationController.Duck(false);
            animationController.Slide();
        }

        Vector2 slideDirection = inputDirection.normalized;
        if (slideDirection.magnitude < 0.1f) slideDirection = animationController.GetLastNonZeroDirection();

        slideVelocity = slideDirection * runSpeed * 1.5f;
        currentVelocity = slideVelocity;
    }

    public void OnSlideAnimationEnd()
    {
        if (!isSliding) return;
        isSliding = false;
        isMovementLocked = false;
        if (animationController != null) animationController.StopSlide();
    }
    #endregion

    public bool IsMoving() => currentVelocity.magnitude > 0.1f;
}