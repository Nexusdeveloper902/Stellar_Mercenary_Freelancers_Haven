using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    [SerializeField] private float speed = 5f;
    [SerializeField] private float walkAnimationSpeed = 1.5f; // Speed for walk animations
    [SerializeField] private float runAnimationSpeed = 2.0f;  // Speed for run animations
    
    private Vector2 movement;
    private Vector2 lastMovement; // To remember last direction for idle animations
    private Rigidbody2D rb;
    private AnimationPlayer animationPlayer;
    
    // Animation state tracking
    private bool isMoving = false;
    private string currentAnimationState = "";
    
    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        animationPlayer = GetComponentInChildren<AnimationPlayer>();
        
        // Initialize with idle_down as default
        lastMovement = Vector2.down;
        
        // Only play animation if AnimationPlayer is properly set up
        if (animationPlayer != null)
        {
            PlayAnimation("idle", lastMovement);
        }
    }
    
    void Update()
    {
        // Get input
        movement = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")).normalized;
        
        // Update animation based on movement
        UpdateAnimation();
    }
    
    void FixedUpdate()
    {
        // Apply movement directly as velocity for snappier controls
        rb.velocity = movement * speed;
    }
    
    void UpdateAnimation()
    {
        bool wasMoving = isMoving;
        isMoving = movement.magnitude > 0.1f;
        
        if (isMoving)
        {
            // Update last movement direction for when we stop
            lastMovement = movement;
            
            // Set animation speed for walking
            animationPlayer.SetAnimationSpeed(walkAnimationSpeed);
            
            // Play walking animation in the direction we're moving
            PlayAnimation("walk", movement);
        }
        else if (wasMoving) // Just stopped moving
        {
            // Set normal speed for idle animations
            animationPlayer.SetAnimationSpeed(1.0f);
            
            // Play idle animation in the last direction we were facing
            PlayAnimation("idle", lastMovement);
        }
    }
    
    private void PlayAnimation(string baseAnimationName, Vector2 direction)
    {
        string directionSuffix = GetDirectionSuffix(direction);
        string animationName = $"{baseAnimationName}_{directionSuffix}";
        
        // Handle sprite flipping for horizontal movement
        if (Mathf.Abs(direction.x) > Mathf.Abs(direction.y))
        {
            HandleSpriteFlipping(direction.x > 0);
        }
        
        // Only play if it's different from current animation
        if (currentAnimationState != animationName)
        {
            if (animationPlayer.HasAnimation(animationName))
            {
                animationPlayer.Play(animationName);
                currentAnimationState = animationName;
            }
            else
            {
                Debug.LogWarning($"Animation not found: {animationName}");
            }
        }
    }
    
    private void HandleSpriteFlipping(bool shouldFaceRight)
    {
        // Use the AnimationPlayer's sprite flipping functionality
        if (animationPlayer.IsFacingRight() != shouldFaceRight)
        {
            animationPlayer.SetFacing(shouldFaceRight);
        }
    }
    
    private string GetDirectionSuffix(Vector2 direction)
    {
        // Determine direction based on the largest component
        if (Mathf.Abs(direction.x) > Mathf.Abs(direction.y))
        {
            return "side"; // Left or right (you can handle flipping separately)
        }
        else
        {
            return direction.y > 0 ? "up" : "down";
        }
    }
    
    // Public methods for triggering other animations
    public void Jump()
    {
        PlayAnimation("jump", lastMovement);
    }
    
    public void Duck()
    {
        PlayAnimation("duck", lastMovement);
    }
    
    public void Run()
    {
        if (isMoving)
        {
            // Set faster animation speed for running
            animationPlayer.SetAnimationSpeed(runAnimationSpeed);
            PlayAnimation("run", movement);
        }
    }
    
    public void Attack()
    {
        // Set normal speed for attack animations
        animationPlayer.SetAnimationSpeed(1.2f);
        PlayAnimation("sword_attack", lastMovement);
    }
    
    public void Slide()
    {
        // Set fast speed for slide animations
        animationPlayer.SetAnimationSpeed(1.8f);
        PlayAnimation("slide", lastMovement);
    }
    
    // Manual sprite flipping (useful for UI or other interactions)
    public void FlipSprite(bool faceRight)
    {
        animationPlayer.SetFacing(faceRight);
    }
}