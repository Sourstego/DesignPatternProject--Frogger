using System.Collections.Generic;
using UnityEngine;

public class Sand : MonoBehaviour {
  [SerializeField] private Transform cactusPrefab;
  [SerializeField] private int cactusMinX = -3;
  [SerializeField] private int cactusMaxX = 9;

  public HashSet<int> Init(float z) {
    // Place the obstacle at the location provided.
    transform.position = new Vector3(0, 0, z);

    // We always have obstacles outside the game area.
    HashSet<int> locations = new() { cactusMinX - 1, cactusMaxX + 1 };

    if (cactusPrefab == null) {
      Debug.LogError("Sand.Init: cactusPrefab is not set in the inspector. Cacti cannot spawn.");
      return locations;
    }

    // Populate with some obstacles
    int numCacti = Random.Range(1, 5);

    for (int i = 0; i < numCacti; i++) {
      // Create a new cactus object
      Transform cactus = Instantiate(cactusPrefab, transform);

      // Put it in a random position (inclusive min/max)
      int xPos = Random.Range(cactusMinX, cactusMaxX + 1);
      cactus.position = new Vector3(xPos, 0.2f, z);

      // Record the location in our HashSet.
      locations.Add(xPos);
    }

    return locations;
  }
}