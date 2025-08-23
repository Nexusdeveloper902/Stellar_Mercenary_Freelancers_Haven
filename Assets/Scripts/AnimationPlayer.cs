using UnityEngine;
using System.Collections.Generic;
using System.IO;

public class AnimationPlayer : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private TextAsset animationListJson; // Drag your AnimationList.json here
    [SerializeField] private string variant = "A"; // Set this in inspector (A, B, C, D, E)
    [SerializeField] private SpriteRenderer spriteRenderer; // For sprite flipping
    [SerializeField] private float animationSpeed = 1.5f; // Speed multiplier for animations
    
    private Dictionary<string, int> animationHashes;
    private bool facingRight = false; // Track which direction we're facing
    
    [System.Serializable]
    private class AnimationData
    {
        public string name;
        public string path;
    }
    
    [System.Serializable]
    private class AnimationListWrapper
    {
        public List<AnimationData> animations;
    }
    
    void Awake()
    {
        if (animator == null)
            animator = GetComponent<Animator>();
            
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();
        
        // Set animation speed
        animator.speed = animationSpeed;
        
        animationHashes = new Dictionary<string, int>();
        
        if (animationListJson != null)
        {
            Debug.Log($"JSON Content (first 500 chars): {animationListJson.text.Substring(0, Mathf.Min(500, animationListJson.text.Length))}...");
            
            try
            {
                // First try to parse as a wrapper with "animations" array
                AnimationListWrapper list = null;
                try
                {
                    list = JsonUtility.FromJson<AnimationListWrapper>(animationListJson.text);
                }
                catch
                {
                    // If that fails, try to parse as direct array
                    Debug.Log("Failed to parse as wrapper, trying direct array...");
                    var jsonWithWrapper = "{\"animations\":" + animationListJson.text + "}";
                    list = JsonUtility.FromJson<AnimationListWrapper>(jsonWithWrapper);
                }
                
                if (list != null && list.animations != null)
                {
                    foreach (var anim in list.animations)
                    {
                        if (!string.IsNullOrEmpty(anim.name))
                        {
                            int hash = Animator.StringToHash(anim.name);
                            animationHashes[anim.name] = hash;
                            Debug.Log($"Added animation to dictionary: '{anim.name}' -> Hash: {hash}");
                        }
                        else
                        {
                            Debug.LogWarning("Found animation with empty name in JSON");
                        }
                    }
                    
                    Debug.Log($"✅ Loaded {animationHashes.Count} animations into dictionary.");
                    
                    // List some examples
                    Debug.Log("=== First 5 animations loaded ===");
                    int count = 0;
                    foreach (var kvp in animationHashes)
                    {
                        if (count >= 5) break;
                        Debug.Log($"  {kvp.Key}");
                        count++;
                    }
                }
                else
                {
                    Debug.LogError("JSON parsing failed or animations list is null");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error parsing JSON: {e.Message}\nJSON content: {animationListJson.text}");
            }
        }
        else
        {
            Debug.LogError("No JSON file assigned!");
        }
        
        // Debug: List all states in the AnimatorController
        if (animator.runtimeAnimatorController != null)
        {
            Debug.Log("=== AnimatorController States ===");
            var controller = animator.runtimeAnimatorController as UnityEditor.Animations.AnimatorController;
            if (controller != null)
            {
                for (int i = 0; i < controller.layers.Length; i++)
                {
                    var layer = controller.layers[i];
                    Debug.Log($"Layer {i}: {layer.name}");
                    foreach (var state in layer.stateMachine.states)
                    {
                        Debug.Log($"  State: {state.state.name}");
                    }
                }
            }
        }
        else
        {
            Debug.LogError("No AnimatorController assigned to the Animator!");
        }
    }
    
    public void Play(string animationName, string variant = null)
    {
        Debug.Log($"🎬 Attempting to play animation: {animationName}");
        
        // Check if animator has a controller
        if (animator.runtimeAnimatorController == null)
        {
            Debug.LogError("Animator does not have an AnimatorController assigned! Please assign one in the Animator component.");
            return;
        }
        
        // Use provided variant or fall back to the serialized one
        string targetVariant = variant ?? this.variant;
        
        if (animationHashes.ContainsKey(animationName))
        {
            Debug.Log($"Found animation in dictionary: {animationName}");
            
            // Try to play the animation
            try
            {
                animator.Play(animationHashes[animationName]);
                Debug.Log($"▶ Successfully triggered animation: {animationName} (Variant {targetVariant})");
                
                // Wait a frame then check if it actually played
                StartCoroutine(CheckAnimationAfterFrame(animationName));
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error playing animation {animationName}: {e.Message}");
            }
        }
        else
        {
            Debug.LogWarning($"Animation not found in dictionary: {animationName} for variant {targetVariant}");
            Debug.Log("Available animations:");
            foreach (var key in animationHashes.Keys)
            {
                Debug.Log($"  - {key}");
            }
        }
    }
    
    private System.Collections.IEnumerator CheckAnimationAfterFrame(string expectedAnimationName)
    {
        yield return null; // Wait one frame
        
        var currentStateInfo = animator.GetCurrentAnimatorStateInfo(0);
        bool isCorrectAnimation = currentStateInfo.IsName(expectedAnimationName);
        
        Debug.Log($"🎭 Current animation state: {currentStateInfo.shortNameHash}");
        Debug.Log($"🎯 Expected animation: {expectedAnimationName}");
        Debug.Log($"✅ Is playing correct animation: {isCorrectAnimation}");
        
        if (!isCorrectAnimation)
        {
            Debug.LogWarning($"Animation {expectedAnimationName} was triggered but didn't play. Current state hash: {currentStateInfo.shortNameHash}");
        }
    }
    
    // Helper method to play directional animations
    public void PlayDirectional(string baseAnimationName, Vector2 direction, string variant = null)
    {
        string directionSuffix = GetDirectionSuffix(direction);
        string fullAnimationName = $"{baseAnimationName}_{directionSuffix}";
        
        Debug.Log($"🧭 Playing directional animation: {baseAnimationName} + {directionSuffix} = {fullAnimationName}");
        
        // Handle sprite flipping for horizontal movement
        HandleSpriteFlipping(direction);
        
        Play(fullAnimationName, variant);
    }
    
    [SerializeField] private bool spritesDrawnFacingLeft = false; // Check this if your sprites face LEFT by default
    
    private void HandleSpriteFlipping(Vector2 direction)
    {
        if (spriteRenderer == null) return;
        
        // Only flip for horizontal movement
        if (Mathf.Abs(direction.x) > Mathf.Abs(direction.y))
        {
            bool shouldFaceRight = direction.x > 0;
            
            // Only update if direction changed to avoid unnecessary operations
            if (facingRight != shouldFaceRight)
            {
                facingRight = shouldFaceRight;
                
                // Flip logic depends on how your sprites are drawn
                if (spritesDrawnFacingLeft)
                {
                    // If sprites face LEFT by default, flip when we want to face RIGHT
                    spriteRenderer.flipX = facingRight;
                }
                else
                {
                    // If sprites face RIGHT by default, flip when we want to face LEFT
                    spriteRenderer.flipX = !facingRight;
                }
                
                Debug.Log($"🔄 Flipping sprite: direction.x={direction.x}, facingRight={facingRight}, flipX={spriteRenderer.flipX}, spritesDrawnFacingLeft={spritesDrawnFacingLeft}");
            }
        }
    }
    
    public void SetFacing(bool faceRight)
    {
        if (spriteRenderer == null) return;
        
        facingRight = faceRight;
        
        // Flip logic depends on how your sprites are drawn
        if (spritesDrawnFacingLeft)
        {
            // If sprites face LEFT by default, flip when we want to face RIGHT
            spriteRenderer.flipX = facingRight;
        }
        else
        {
            // If sprites face RIGHT by default, flip when we want to face LEFT
            spriteRenderer.flipX = !facingRight;
        }
        
        Debug.Log($"👀 Set facing: facingRight={facingRight}, flipX={spriteRenderer.flipX}, spritesDrawnFacingLeft={spritesDrawnFacingLeft}");
    }
    
    public bool IsFacingRight()
    {
        return facingRight;
    }
    
    // Method to change animation speed at runtime
    public void SetAnimationSpeed(float speed)
    {
        animationSpeed = speed;
        animator.speed = animationSpeed;
        Debug.Log($"⚡ Animation speed set to: {speed}");
    }
    
    public float GetAnimationSpeed()
    {
        return animationSpeed;
    }
    
    private string GetDirectionSuffix(Vector2 direction)
    {
        // Determine direction based on the largest component
        if (Mathf.Abs(direction.x) > Mathf.Abs(direction.y))
        {
            return "side"; // Left or right
        }
        else
        {
            return direction.y > 0 ? "up" : "down";
        }
    }
    
    // Helper method to check if an animation exists
    public bool HasAnimation(string animationName)
    {
        bool hasAnim = animationHashes.ContainsKey(animationName);
        Debug.Log($"🔍 Checking if animation exists: {animationName} -> {hasAnim}");
        return hasAnim;
    }
    
    // Debug method to list all available animations
    [ContextMenu("List All Animations")]
    public void ListAllAnimations()
    {
        Debug.Log("=== Available Animations ===");
        foreach (var kvp in animationHashes)
        {
            Debug.Log($"  {kvp.Key} -> Hash: {kvp.Value}");
        }
    }
}