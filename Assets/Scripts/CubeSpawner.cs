using Fusion;
using UnityEngine;

/// <summary>
/// Spawns multiple pickupable cubes at the start of the game
/// </summary>
public class CubeSpawner : NetworkBehaviour
{
    [Header("Spawn Settings")]
    [SerializeField] private Vector3 _spawnAreaCenter = new Vector3(0f, 5f, 0f);
    [SerializeField] private float _spawnAreaRadius = 20f;
    [SerializeField] private int _cubeCount = 30;
    [SerializeField] private Vector3 _cubeSize = new Vector3(0.5f, 0.5f, 0.5f);
    [SerializeField] private Color _cubeColor = Color.yellow;

    [Header("Player Spawn Exclusion")]
    [SerializeField] private Vector3 _playerSpawnCenter = new Vector3(0f, 1f, 0f);
    [SerializeField] private float _playerSpawnExclusionRadius = 5f;

    [Header("Prefab")]
    [SerializeField] private NetworkPrefabRef _cubePrefab;

    private System.Collections.Generic.List<NetworkObject> _spawnedCubes = new System.Collections.Generic.List<NetworkObject>();

    public override void Spawned()
    {
        // Only the host/server spawns the cubes
        if (Object.HasStateAuthority)
        {
            SpawnCubes();
        }
    }

    private void SpawnCubes()
    {
        if (_cubePrefab == default)
        {
            Debug.LogError("[CubeSpawner] Cube prefab is not assigned!");
            return;
        }

        Debug.Log($"[CubeSpawner] Starting to spawn {_cubeCount} cubes...");

        int successfulSpawns = 0;
        int attempts = 0;
        int maxAttempts = _cubeCount * 10; // Prevent infinite loop

        while (successfulSpawns < _cubeCount && attempts < maxAttempts)
        {
            attempts++;

            // Generate random position within spawn area
            Vector3 randomPosition = GenerateRandomSpawnPosition();

            // Check if position is too close to player spawn area
            if (IsPositionInPlayerSpawnArea(randomPosition))
            {
                continue; // Skip this position, try another
            }

            // Try to spawn cube at this position
            if (TrySpawnCubeAt(randomPosition))
            {
                successfulSpawns++;
            }
        }

        Debug.Log($"[CubeSpawner] Spawned {successfulSpawns}/{_cubeCount} cubes in {attempts} attempts");
    }

    private Vector3 GenerateRandomSpawnPosition()
    {
        // Generate random position within circular area
        float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
        float distance = Random.Range(0f, _spawnAreaRadius);

        float x = _spawnAreaCenter.x + Mathf.Cos(angle) * distance;
        float z = _spawnAreaCenter.z + Mathf.Sin(angle) * distance;

        // Use spawn area center Y as starting height
        return new Vector3(x, _spawnAreaCenter.y, z);
    }

    private bool IsPositionInPlayerSpawnArea(Vector3 position)
    {
        // OPTIMIZATION: Use sqrMagnitude instead of Distance to avoid expensive sqrt
        // Check horizontal distance only (ignore Y)
        float dx = position.x - _playerSpawnCenter.x;
        float dz = position.z - _playerSpawnCenter.z;
        float distanceSqr = dx * dx + dz * dz;

        return distanceSqr < (_playerSpawnExclusionRadius * _playerSpawnExclusionRadius);
    }

    private bool TrySpawnCubeAt(Vector3 spawnPosition)
    {
        // Calculate ground-aligned spawn position and rotation
        Vector3 finalSpawnPosition = spawnPosition;
        Quaternion finalSpawnRotation = Quaternion.identity;

        // Calculate cube half-height from spawn size
        float cubeHalfHeight = _cubeSize.y * 0.5f;

        // Raycast downward to find ground
        if (Physics.Raycast(spawnPosition, Vector3.down, out RaycastHit hit, maxDistance: 50f))
        {
            // Safety offset to prevent clipping
            float safetyOffset = 0.02f;

            // Position cube on ground surface aligned to normal
            finalSpawnPosition = hit.point + hit.normal * (cubeHalfHeight + safetyOffset);

            // Align rotation to ground normal
            finalSpawnRotation = Quaternion.FromToRotation(Vector3.up, hit.normal);
        }
        else
        {
            // No ground found, skip this position
            return false;
        }

        // Spawn the networked cube at ground-aligned position
        NetworkObject spawnedCube = Runner.Spawn(
            _cubePrefab,
            finalSpawnPosition,
            finalSpawnRotation,
            Object.InputAuthority
        );

        if (spawnedCube != null)
        {
            _spawnedCubes.Add(spawnedCube);
            return true;
        }

        return false;
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        // Draw spawn area (green circle)
        Gizmos.color = Color.green;
        DrawCircle(_spawnAreaCenter, _spawnAreaRadius, 32);

        // Draw player spawn exclusion area (red circle)
        Gizmos.color = Color.red;
        DrawCircle(_playerSpawnCenter, _playerSpawnExclusionRadius, 32);

        // Draw center markers
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(_spawnAreaCenter, 0.5f);
        Gizmos.DrawWireSphere(_playerSpawnCenter, 0.5f);
    }

    private void DrawCircle(Vector3 center, float radius, int segments)
    {
        float angleStep = 360f / segments;
        Vector3 prevPoint = center + new Vector3(radius, 0, 0);

        for (int i = 1; i <= segments; i++)
        {
            float angle = i * angleStep * Mathf.Deg2Rad;
            Vector3 newPoint = center + new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius);
            Gizmos.DrawLine(prevPoint, newPoint);
            prevPoint = newPoint;
        }
    }
#endif
}

