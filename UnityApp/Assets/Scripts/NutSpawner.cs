using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class NutSpawner : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Auto-populated from all NutSpawnLocation components in children.")]
    public List<NutSpawnLocation> spawnPoints = new List<NutSpawnLocation>();
    public GameObject nutPrefab;

    [Header("Counts")]
    public int minNuts = 4;
    public int maxNuts = 9;

    [Header("Options")]
    public bool parentToTree = true;
    [Tooltip("Non-zero = same tree always spawns the same nuts. 0 = fully random each time.")]
    public int seed = 0;

    void OnValidate()
    {
        // Auto-populate in the editor whenever something changes
        RefreshSpawnPoints();
    }

    void Reset()
    {
        RefreshSpawnPoints();
    }

    [ContextMenu("Refresh Spawn Points")]
    public void RefreshSpawnPoints()
    {
        spawnPoints = GetComponentsInChildren<NutSpawnLocation>(true).ToList();
    }

    void Start()
    {
        SpawnNuts();
    }

    public void SpawnNuts()
    {
        if (spawnPoints == null || spawnPoints.Count == 0)
            RefreshSpawnPoints();

        if (nutPrefab == null || spawnPoints.Count == 0) return;

        var rng = (seed != 0) ? new System.Random(seed) : new System.Random();

        var shuffled = spawnPoints.Where(p => p != null)
            .OrderBy(_ => rng.Next())
            .ToList();

        int count = Mathf.Clamp(rng.Next(minNuts, maxNuts + 1), 0, shuffled.Count);

        for (int i = 0; i < count; i++)
        {
            Transform point = shuffled[i].transform;
            Transform parent = parentToTree ? transform : null;
            Instantiate(nutPrefab, point.position, point.rotation, parent);
        }
    }
}