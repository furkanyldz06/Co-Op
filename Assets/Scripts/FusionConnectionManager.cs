using System;
using System.Collections.Generic;
using Fusion;
using Fusion.Sockets;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;

public class FusionConnectionManager : MonoBehaviour, INetworkRunnerCallbacks
{
    [Header("Fusion Settings")]
    [SerializeField] private NetworkRunner _runnerPrefab;
    [SerializeField] private NetworkPrefabRef _playerPrefab;

    [Header("Game Mode")]
    [Tooltip("AutoHostOrClient = Mac/Windows (Client-Server)\nShared = WebGL (Peer-to-Peer)")]
    [SerializeField] private GameMode _gameMode = GameMode.AutoHostOrClient;

    private NetworkRunner _runner;
    private int _spawnIndex = 0;

    private void Start()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        // WebGL MUST use Shared mode (peer-to-peer)
        StartGame(GameMode.Shared);
#else
        // Desktop uses AutoHostOrClient (client-server)
        StartGame(_gameMode);
#endif
    }

    async void StartGame(GameMode mode)
    {
        try
        {
            _runner = gameObject.AddComponent<NetworkRunner>();
            _runner.ProvideInput = true;

            var startGameArgs = new StartGameArgs()
            {
                GameMode = mode,
                SessionName = "TestRoom",
                Scene = SceneRef.FromIndex(SceneManager.GetActiveScene().buildIndex),
                SceneManager = gameObject.AddComponent<NetworkSceneManagerDefault>()
            };

            Debug.Log($"[Fusion] Starting game in {mode} mode...");
            var result = await _runner.StartGame(startGameArgs);

            if (result.Ok)
            {
                Debug.Log("[Fusion] Successfully connected!");
            }
            else
            {
                Debug.LogError($"[Fusion] Failed to start: {result.ShutdownReason}");
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[Fusion] Exception during StartGame: {ex.Message}\n{ex.StackTrace}");
        }
    }

    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        if (runner.IsServer || runner.GameMode == GameMode.Shared)
        {
            Vector3 spawnPosition = new Vector3(_spawnIndex * 2, 1, 0);
            runner.Spawn(_playerPrefab, spawnPosition, Quaternion.identity, player);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"Player {player.PlayerId} spawned at {spawnPosition}");
#endif
            _spawnIndex++;
        }
    }

    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"Player {player.PlayerId} left");
#endif
    }

    public void OnInput(NetworkRunner runner, NetworkInput input)
    {
        var data = new NetworkInputData();

        // Get keyboard safely (WebGL compatible)
        var keyboard = Keyboard.current;
        if (keyboard != null)
        {
            // Movement input
            if (keyboard.wKey.isPressed)
                data.direction.z += 1f;

            if (keyboard.sKey.isPressed)
                data.direction.z -= 1f;

            if (keyboard.aKey.isPressed)
                data.direction.x -= 1f;

            if (keyboard.dKey.isPressed)
                data.direction.x += 1f;

            // Sprint input
            data.isSprinting = keyboard.leftShiftKey.isPressed;

            // Jump input - use isPressed instead of wasPressedThisFrame for better reliability
            data.isJumping = keyboard.spaceKey.isPressed;

            // Pickup input (E key)
            data.isPickingUp = keyboard.eKey.isPressed;

            // Drop cube input (F key)
            data.isDroppingCube = keyboard.fKey.isPressed;
        }

        input.Set(data);
    }

    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) { }
    public void OnConnectedToServer(NetworkRunner runner) { }
    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { }
    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }
    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
    public void OnSceneLoadDone(NetworkRunner runner) { }
    public void OnSceneLoadStart(NetworkRunner runner) { }
    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
}

public struct NetworkInputData : INetworkInput
{
    public Vector3 direction;
    public NetworkBool isSprinting;
    public NetworkBool isJumping;
    public NetworkBool isPickingUp; // E key for pickup
    public NetworkBool isDroppingCube; // F key for dropping cube
}

