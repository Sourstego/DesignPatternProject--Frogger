using UnityEngine;

public class Character : MonoBehaviour {
  [SerializeField] private GameManager gameManager;
  [SerializeField] private GameObject character;
  [SerializeField] private ParticleSystem deathParticles;
  
  private float collisionGraceTime = 1f;
  private float timeSinceSpawn = 0f;

  private void Start() {
    timeSinceSpawn = 0f;
  }

  private void Update() {
    timeSinceSpawn += Time.deltaTime;
  }

  private void OnCollisionEnter(Collision collision) {
    // Don't process collisions during grace period after spawn
    if (timeSinceSpawn < collisionGraceTime) {
      return;
    }
    
    if (collision == null || collision.gameObject == null || character == null) {
      return;
    }
    if (!character.activeSelf) {
      return;
    }
    
    // Check if the colliding object has a Rigidbody (vehicles have Rigidbodies)
    // This is a reliable way to detect vehicles without relying on tags
    if (collision.gameObject.GetComponent<Rigidbody>() != null) {
      Debug.Log($"Character collision detected with: {collision.gameObject.name} at {collision.GetContact(0).point}");
      Kill(collision.GetContact(0).point);
    }
  }

  public void Kill(Vector3 collisionPoint) {
    // Hide the character model
    character.SetActive(false);

    // Orient the particles relative to the collision.
    deathParticles.transform.position = collisionPoint;
    deathParticles.transform.LookAt(transform.position + Vector3.up);

    // Show the particles.
    deathParticles.Play();

    // Tell the GameManager we've collided.
    gameManager.PlayerCollision();
  }

  public void Reset() {
    // Re-enable the character model.
    character.SetActive(true);
    // Remove any left over particles.
    deathParticles.Clear();
    // Reset grace period timer
    timeSinceSpawn = 0f;
  }
}