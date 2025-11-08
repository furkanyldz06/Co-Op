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

    // Cached input reference
    private Keyboard _keyboard;

    private void Start()
    {
        // Cache keyboard reference
        _keyboard = Keyboard.current;

        StartGame(_gameMode);
    }

    async void StartGame(GameMode mode)
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

        await _runner.StartGame(startGameArgs);
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

        if (_keyboard != null)
        {
            // Use bitwise operations for better performance
            if (_keyboard.wKey.isPressed)
                data.direction.z += 1f;

            if (_keyboard.sKey.isPressed)
                data.direction.z -= 1f;

            if (_keyboard.aKey.isPressed)
                data.direction.x -= 1f;

            if (_keyboard.dKey.isPressed)
                data.direction.x += 1f;

            data.isSprinting = _keyboard.leftShiftKey.isPressed;
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
}

