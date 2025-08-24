using UnityEngine;

public class AnimationEventForwarder : MonoBehaviour
{
    // We will find the PlayerMovement script in the parent object.
    private PlayerMovement playerMovement;

    void Awake()
    {
        // Find the PlayerMovement script in the parent GameObject when the game starts.
        playerMovement = GetComponentInParent<PlayerMovement>();

        if (playerMovement == null)
        {
            Debug.LogError("AnimationEventForwarder could not find a PlayerMovement script in any parent object!", this.gameObject);
        }
    }

    /// <summary>
    /// This is the public function that the Animation Event will call.
    /// It receives the event and forwards the call to the main script.
    /// </summary>
    public void OnSlideAnimationEnd()
    {
        // If we found the script, call the real function on it.
        if (playerMovement != null)
        {
            playerMovement.OnSlideAnimationEnd();
        }
    }

    // You can add more forwarding functions here for other animation events if needed.
    // public void OnFootstep() { ... }
    // public void OnAttackHit() { ... }
}