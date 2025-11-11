using Fusion;
using UnityEngine;

/// <summary>
/// Spawns a pickupable cube at the start of the game
/// </summary>
public class CubeSpawner : NetworkBehaviour
{
    [Header("Spawn Settings")]
    [SerializeField] private Vector3 _spawnPosition = new Vector3(5f, 1f, 0f);
    [SerializeField] private Vector3 _cubeSize = new Vector3(0.5f, 0.5f, 0.5f);
    [SerializeField] private Color _cubeColor = Color.yellow;

    [Header("Prefab")]
    [SerializeField] private NetworkPrefabRef _cubePrefab;

    private NetworkObject _spawnedCube;

    public override void Spawned()
    {
        // Only the host/server spawns the cube
        if (Object.HasStateAuthority)
        {
            SpawnCube();
        }
    }

    private void SpawnCube()
    {
        if (_cubePrefab == default)
        {
            Debug.LogError("[CubeSpawner] Cube prefab is not assigned!");
            return;
        }

        // Calculate ground-aligned spawn position and rotation
        Vector3 finalSpawnPosition = _spawnPosition;
        Quaternion finalSpawnRotation = Quaternion.identity;

        // Calculate cube half-height from spawn size
        float cubeHalfHeight = _cubeSize.y * 0.5f;

        // Raycast downward to find ground
        if (Physics.Raycast(_spawnPosition, Vector3.down, out RaycastHit hit, maxDistance: 10f))
        {
            // Safety offset to prevent clipping
            float safetyOffset = 0.02f;

            // Position cube on ground surface aligned to normal
            finalSpawnPosition = hit.point + hit.normal * (cubeHalfHeight + safetyOffset);

            // Align rotation to ground normal (preserves Y rotation which is 0 for identity)
            finalSpawnRotation = Quaternion.FromToRotation(Vector3.up, hit.normal);

            Debug.Log($"[CubeSpawner] Ground found at {hit.point}, normal: {hit.normal}, spawning at {finalSpawnPosition}");
        }
        else
        {
            Debug.LogWarning($"[CubeSpawner] No ground found below spawn position {_spawnPosition}, using original position");
        }

        // Spawn the networked cube at ground-aligned position
        _spawnedCube = Runner.Spawn(
            _cubePrefab,
            finalSpawnPosition,
            finalSpawnRotation,
            Object.InputAuthority
        );

        Debug.Log($"[CubeSpawner] Spawned cube at {finalSpawnPosition} with rotation {finalSpawnRotation.eulerAngles}");
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        // Draw spawn position
        Gizmos.color = _cubeColor;
        Gizmos.DrawWireCube(_spawnPosition, _cubeSize);
    }
#endif
}

