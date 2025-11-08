using Fusion;
using UnityEngine;

public class BasicSpawner : SimulationBehaviour, IPlayerJoined
{
    [Header("Player Settings")]
    [SerializeField] private NetworkPrefabRef _playerPrefab;
    
    public void PlayerJoined(PlayerRef player)
    {
        // Sadece sunucu/host oyuncuları spawn edebilir
        if (Runner.IsServer)
        {
            // Rastgele bir spawn pozisyonu belirle
            Vector3 spawnPosition = new Vector3(Random.Range(-5f, 5f), 1, Random.Range(-5f, 5f));
            
            // Oyuncu prefab'ını network üzerinden spawn et
            NetworkObject networkPlayerObject = Runner.Spawn(_playerPrefab, spawnPosition, Quaternion.identity, player);
            
            Debug.Log($"Oyuncu spawn edildi: {player.PlayerId} pozisyon: {spawnPosition}");
        }
    }
}

