using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class GameManager : MonoBehaviour {
  [Header("Game objects")]
  [SerializeField] private Transform character;
  [SerializeField] private Transform characterModel;
  [SerializeField] private Transform terrainHolder;
  [SerializeField] private TMPro.TextMeshProUGUI scoreText;

  [Header("Terrain objects")]
  [SerializeField] private Sand sandPrefab;
  [SerializeField] private Road roadPrefab;

  [Header("Game parameters")]
  [SerializeField] private float moveDuration = 0.2f;
  [SerializeField] private int spawnDistance = 20;
  [Header("UI")]
  [SerializeField] private GameObject gameOverPanel;

  enum GameState {
    Ready,
    Moving,
    Dead
  }
  private GameState gameState;
  private Vector2Int characterPos;
  private int spawnLocation;
  private bool stopSpawning = false;
  private Coroutine deathCoroutine = null;
  private List<(float terrainHeight, HashSet<int> locations, GameObject obj)> obstacles = new();
  private int score = 0;

  void Awake() {
    // Check required references
    if (character == null) {
      GameObject charObj = GameObject.Find("Character");
      if (charObj != null) {
        character = charObj.transform;
      } else {
        Debug.LogError("Character Transform is not assigned in GameManager and no GameObject named 'Character' found!");
        return;
      }
    }
    if (terrainHolder == null) {
      Debug.LogError("Terrain Holder Transform is not assigned in GameManager!");
      return;
    }
    if (sandPrefab == null || roadPrefab == null) {
      Debug.LogError("Sand or Road prefabs are not assigned in GameManager!");
      return;
    }
    if (characterModel == null) {
      Debug.LogError("Character Model Transform is not assigned in GameManager!");
      return;
    }
    NewLevel();
  }

  private void NewLevel() {
    if (character == null) {
      Debug.LogError("Cannot start new level: Character Transform is not assigned!");
      return;
    }
    if (terrainHolder == null) {
      Debug.LogError("Cannot start new level: Terrain Holder Transform is not assigned!");
      return;
    }
    gameState = GameState.Ready;
    // Re-enable spawning/movement when starting a new level
    stopSpawning = false;
    deathCoroutine = null;

    // Hide Game Over UI when starting
    if (gameOverPanel != null) {
      gameOverPanel.SetActive(false);
    }

    // Reset character position
    characterPos = new Vector2Int(0, -2);
    character.position = new Vector3(0, 0.2f, -2);
    
    Character characterScript = character.GetComponent<Character>();
    if (characterScript == null) {
      Debug.LogError($"Character script is missing on GameObject '{character.name}'. Make sure the Character script is attached to the character GameObject.");
      return;
    }
    characterScript.Reset();

    // Reset the score
    score = 0;
    if (scoreText != null) {
      scoreText.text = "0";
    }

    // Remove all terrain
    obstacles.Clear();
    foreach (Transform child in terrainHolder) {
      Destroy(child.gameObject);
    }

    // Reset level, and regenerate
    spawnLocation = 0;
    for (int i = 0; i < spawnDistance; i++) {
      Debug.Log($"[NewLevel] Iteration {i}: spawnLocation={spawnLocation}, roadPrefab={(roadPrefab != null ? roadPrefab.name : "null")}, sandPrefab={(sandPrefab != null ? sandPrefab.name : "null")}, terrainHolder={(terrainHolder != null ? terrainHolder.name : "null")}");
      SpawnObstacle();
    }
  }

  private void SpawnObstacle() {
    if (stopSpawning) return;
    if (sandPrefab == null || roadPrefab == null || terrainHolder == null) {
      Debug.LogError("Cannot spawn obstacle: Prefabs or terrain holder not assigned!");
      return;
    }
    // Spawn more roads the further we get, at 250 have 90% chance of a road.
    float roadProbability = Mathf.Lerp(0.5f, 0.9f, spawnLocation / 250f);

    if (Random.value < roadProbability) {
      // Create road with terrain height of 0.1f.
      Road road = Instantiate(roadPrefab, terrainHolder);
      obstacles.Add((0.1f, road.Init(spawnLocation), road.gameObject));
      road.gameObject.name = $"{spawnLocation} - Road";
    } else {
      // Create sand with terrain height of 0.2f.
      Sand sand = Instantiate(sandPrefab, terrainHolder);
      obstacles.Add((0.2f, sand.Init(spawnLocation), sand.gameObject));
      sand.gameObject.name = $"{spawnLocation} - Sand";
    }

    // Update to the next free location
    spawnLocation++;
  }

  private bool InStartArea(Vector2Int location) {
    // Movement anywhere in the starting region is allowed.
    if ((location.y > -5) && (location.y < 0) && (location.x > -6) && (location.x < 6)) {
      return true;
    }
    return false;
  }

  // Update is called once per frame
  void Update() {
    if (character == null) return;
    // Detect arrow key presses.
    if (gameState == GameState.Ready) {
      Vector2Int moveDirection = Vector2Int.zero;
      // Single if/else don't want to move diagonally.
      if (Keyboard.current.upArrowKey.wasPressedThisFrame) {
        character.localRotation = Quaternion.identity;
        moveDirection.y = 1;
      } else if (Keyboard.current.downArrowKey.wasPressedThisFrame) {
        character.localRotation = Quaternion.Euler(0, 180, 0);
        moveDirection.y = -1;
      } else if (Keyboard.current.leftArrowKey.wasPressedThisFrame) {
        character.localRotation = Quaternion.Euler(0, -90, 0);
        moveDirection.x = -1;
      } else if (Keyboard.current.rightArrowKey.wasPressedThisFrame) {
        character.localRotation = Quaternion.Euler(0, 90, 0);
        moveDirection.x = 1;
      }

      // If the user wants to move
      if (moveDirection != Vector2Int.zero) {
        Vector2Int destination = characterPos + moveDirection;
        // In the start area there are no obstacles so you can move anywhere.
        if (InStartArea(destination) || ((destination.y >= 0) && !obstacles[destination.y].locations.Contains(destination.x))) {
          // Update our character grid coordinate.
          characterPos = destination;
          // Call coroutine to move the character object.
          StartCoroutine(MoveCharacter());
          // Update score if necessary.
          if ((destination.y + 1) > score) {
            score = destination.y + 1;
            if (scoreText != null) {
              scoreText.text = $"{score}";
            }
          }
        }

        // Spawn new obstacles if necessary
        while (obstacles.Count < (characterPos.y + spawnDistance)) {
          SpawnObstacle();

          // Destroy old terrain objects as we progress
          int oldIndex = characterPos.y - spawnDistance;
          if ((oldIndex >= 0) && (obstacles[oldIndex].obj != null)) {
            Destroy(obstacles[oldIndex].obj);
          }
        }

        // If we've gone back too far end the game.
        if (characterPos.y < (score - 10)) {
          character.GetComponent<Character>().Kill(character.transform.position + new Vector3(0, 0.2f, 0.5f));
        }
      }
    }

    // Can only use our shortcut to reset the level when we're dead.
    if (gameState == GameState.Dead && Keyboard.current.spaceKey.wasPressedThisFrame) {
      NewLevel();
    }

    // Camera follow at (+2, 4, -3)
    Vector3 cameraPosition = new(character.position.x + 2, 4, character.position.z - 3);

    // Limit camera movement in x direction.
    // Only follow the character as it moves to -3 and +3.
    // The camera offset is +2 so that's -1 to +5 in the camera x position.
    cameraPosition.x = Mathf.Clamp(cameraPosition.x, -1, 5);

    Camera.main.transform.position = cameraPosition;
  }

  private IEnumerator MoveCharacter() {
    if (character == null || characterModel == null) {
      Debug.LogError("Cannot move character: Character or CharacterModel not assigned!");
      yield break;
    }
    gameState = GameState.Moving;
    float elapsedTime = 0f;

    // The yHeight changes if we're on sand or road.
    float yHeight = 0.2f;
    if (characterPos.y >= 0) {
      yHeight = obstacles[characterPos.y].terrainHeight;
    }

    Vector3 startPos = character.position;
    Vector3 endPos = new(characterPos.x, yHeight, characterPos.y);

    Quaternion startRotation = characterModel.localRotation;

    while (elapsedTime < moveDuration) {
      // How far through the animation are we.
      float percent = elapsedTime / moveDuration;

      // Update the character position
      Vector3 newPos = Vector3.Lerp(startPos, endPos, percent);
      // Make the character jump in an arc
      newPos.y = yHeight + (0.5f * Mathf.Sin(Mathf.PI * percent));
      character.position = newPos;

      // Update the model rotation
      Vector3 rotation = characterModel.localRotation.eulerAngles;
      characterModel.localRotation = Quaternion.Euler(-5f * Mathf.PI * Mathf.Cos(Mathf.PI * percent), rotation.y, rotation.z);

      // Update the elapsed time
      elapsedTime += Time.deltaTime;

      yield return null;
    }

    // Ensure we're at the end.
    character.position = endPos;
    characterModel.localRotation = startRotation;

    // Need to check we're still in moving at the end.
    // If we're dead we don't want to go back to ready.
    if (gameState == GameState.Moving) {
      gameState = GameState.Ready;
    }
  }

  public void PlayerCollision() {
    // When we collide, we'll simply update the game state.
    gameState = GameState.Dead;
    if (deathCoroutine == null) {
      deathCoroutine = StartCoroutine(OnPlayerDeath());
    }
  }

  private IEnumerator OnPlayerDeath() {
    // Wait 4 seconds, then stop further spawning and halt current vehicle movement.
    yield return new WaitForSeconds(4f);

    stopSpawning = true;

    // Find all road controllers and tell them to stop moving their vehicles.
    Road[] roads = FindObjectsOfType<Road>();
    foreach (Road r in roads) {
      r.SetMoving(false);
    }

    // Show Game Over UI after vehicles have been halted
    ShowGameOverPanel();

    Debug.Log("Player died: stopped spawning and halted vehicles.");
  }

  // UI helpers
  private void ShowGameOverPanel() {
    if (gameOverPanel != null) gameOverPanel.SetActive(true);
  }

  private void HideGameOverPanel() {
    if (gameOverPanel != null) gameOverPanel.SetActive(false);
  }

  // Public method suitable for UI Button onClick
  public void RestartLevel() {
    HideGameOverPanel();
    NewLevel();
  }
}