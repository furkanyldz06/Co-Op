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

        // Spawn the networked cube
        _spawnedCube = Runner.Spawn(
            _cubePrefab,
            _spawnPosition,
            Quaternion.identity,
            Object.InputAuthority
        );

        Debug.Log($"[CubeSpawner] Spawned cube at {_spawnPosition}");
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

