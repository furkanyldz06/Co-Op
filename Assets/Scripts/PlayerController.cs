using Fusion;
using UnityEngine;
using TMPro;

public class PlayerController : NetworkBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float _moveSpeed = 5f;
    [SerializeField] private float _rotationSpeed = 10f;
    [SerializeField] private float _gravity = -20f;

    [Header("Sprint Settings")]
    [SerializeField] private float _sprintMultiplier = 2f;

    [Header("Jump Settings")]
    [SerializeField] private float _jumpForce = 8f;
    [SerializeField] private float _groundCheckDistance = 0.5f;
    [SerializeField] private float _coyoteTime = 0.2f; // Time after leaving ground where jump is still allowed
    [SerializeField] private float _jumpBufferTime = 0.2f; // Time before landing where jump input is buffered
    [SerializeField] private LayerMask _groundLayer = -1; // All layers by default

    [Header("Animation")]
    [SerializeField] private Animator _animator;

    [Header("Camera")]
    [SerializeField] private GameObject _camera;

    [Header("Player Name")]
    [SerializeField] private TMP_Text _nameText;
    [SerializeField] private float _nameYOffset = 2.5f;

    [Header("Pickup System")]
    [SerializeField] private Transform _leftHandBone; // mixamorig:LeftHand
    [SerializeField] private Vector3 _cubeHoldOffset = new Vector3(0.05f, 0.05f, 0.1f); // Local position offset
    [SerializeField] private Vector3 _cubeHoldRotation = new Vector3(0, 0, 0); // Local rotation
    [SerializeField] private float _pickupRange = 2f;

    // Networked Properties
    [Networked] public NetworkBool IsWalking { get; set; }
    [Networked] public NetworkBool IsRunning { get; set; }
    [Networked] public NetworkBool IsJumping { get; set; }
    [Networked] private Vector3 NetworkedVelocity { get; set; }
    [Networked] private int JumpCounter { get; set; } // Counter to trigger animation on all clients
    [Networked, Capacity(16)] public string PlayerName { get; set; }
    [Networked] public NetworkBool IsCarrying { get; set; } // Is player carrying a cube?
    [Networked] public NetworkBehaviourId CarriedCubeId { get; set; } // ID of the carried cube

    // Cached Components
    private MeshRenderer _renderer;
    private CharacterController _controller;
    private Transform _nameCanvas; // Cache canvas transform
    private Camera _mainCamera; // Cache main camera reference
    private PickupableCube _carriedCube; // Reference to the carried cube
    private Collider _carriedCubeCol; // Cached collider

    // Constants
    private const float MOVEMENT_THRESHOLD = 0.01f; // sqrMagnitude için optimize
    private const float GROUND_STICK_FORCE = -2f;
    private const float MIN_CAMERA_DISTANCE_SQR = 0.01f; // For billboard rotation check

    // Ground check state
    private bool _isGrounded;
    private float _lastGroundedTime;
    private float _jumpRequestTime = -999f;
    private int _lastJumpCounter = 0; // Track last jump counter for animation

    // Carry layer animation
    private float _currentCarryWeight = 0f; // Current carry layer weight
    private const float CARRY_TRANSITION_SPEED = 5f; // Speed of transition (higher = faster)

    private void Awake()
    {
        _renderer = GetComponent<MeshRenderer>();
        _controller = GetComponent<CharacterController>();

        // Find LeftHand bone automatically if not set in Inspector
        if (_leftHandBone == null)
        {
            _leftHandBone = FindLeftHandBone(transform);
            if (_leftHandBone != null)
            {
                Debug.Log($"[PlayerController] ✅ Found LeftHand bone: {_leftHandBone.name}");
            }
            else
            {
                Debug.LogError($"[PlayerController] ❌ Could not find LeftHand bone!");
            }
        }
    }

    /// <summary>
    /// Recursively search for LeftHand bone in the hierarchy
    /// </summary>
    private Transform FindLeftHandBone(Transform parent)
    {
        // Check if this transform is the LeftHand
        if (parent.name.Contains("LeftHand"))
        {
            return parent;
        }

        // Recursively search children
        foreach (Transform child in parent)
        {
            Transform result = FindLeftHandBone(child);
            if (result != null)
            {
                return result;
            }
        }

        return null;
    }

    // Static array to avoid allocation every frame
    private static readonly Vector3[] _groundCheckOffsets = new Vector3[]
    {
        new Vector3(0.8f, 0, 0),    // Right (using normalized radius)
        new Vector3(-0.8f, 0, 0),   // Left
        new Vector3(0, 0, 0.8f),    // Forward
        new Vector3(0, 0, -0.8f)    // Back
    };

    private bool CheckGrounded()
    {
        // Early return if CharacterController says grounded
        if (_controller.isGrounded)
        {
            return true;
        }

        // Method 2: SphereCast from center (most reliable for slopes)
        Vector3 center = transform.position + _controller.center;
        float radius = _controller.radius * 0.9f;

        if (Physics.SphereCast(center, radius, Vector3.down, out RaycastHit hit, _groundCheckDistance, _groundLayer))
        {
            return true;
        }

        // Method 3: Multiple raycasts from bottom (for edges)
        Vector3 bottom = transform.position + Vector3.up * 0.1f;
        float checkRadius = _controller.radius;

        // Center raycast
        if (Physics.Raycast(bottom, Vector3.down, _groundCheckDistance, _groundLayer))
        {
            return true;
        }

        // 4 corner raycasts (using static array to avoid allocation)
        for (int i = 0; i < _groundCheckOffsets.Length; i++)
        {
            Vector3 offset = _groundCheckOffsets[i] * checkRadius;
            if (Physics.Raycast(bottom + offset, Vector3.down, _groundCheckDistance, _groundLayer))
            {
                return true;
            }

#if UNITY_EDITOR
            // Visual debug - draw raycasts
            Debug.DrawRay(bottom + offset, Vector3.down * _groundCheckDistance, Color.red);
#endif
        }

#if UNITY_EDITOR
        // Visual debug - center raycast
        Debug.DrawRay(bottom, Vector3.down * _groundCheckDistance, Color.red);
#endif

        return false;
    }

    public override void Spawned()
    {
        // Initialize CharacterController
        if (_controller != null)
        {
            _controller.enabled = false;
            _controller.enabled = true;
        }

        // Setup camera only for local player
        if (Object.HasInputAuthority)
        {
            SetupLocalCamera();

            // Set player name from UI
            string playerName = PlayerNameUI.PlayerName;

            if (Object.HasStateAuthority)
            {
                // Host/Shared mode: Set directly
                PlayerName = playerName;
                Debug.Log($"[PlayerController] Player name set directly: {PlayerName}");
            }
            else
            {
                // Client mode: Use RPC to tell host
                RPC_SetPlayerName(playerName);
                Debug.Log($"[PlayerController] Requesting name via RPC: {playerName}");
            }
        }
        else if (_camera != null)
        {
            _camera.SetActive(false);
        }

        // Setup player color based on spawn position
        SetupPlayerColor();

        // Setup name text
        SetupNameText();
    }

    private void SetupLocalCamera()
    {
        if (_camera == null) return;

        _camera.transform.parent = null;
        _camera.SetActive(true);

        // Set MainCamera tag so Camera.main finds this camera
        Camera cam = _camera.GetComponentInChildren<Camera>();
        if (cam != null)
        {
            cam.tag = "MainCamera";
        }

        var cameraManager = _camera.GetComponent<GameOrganization.CameraManager>();
        if (cameraManager != null)
        {
            cameraManager.followObj = transform;

            var cameraMovement = _camera.GetComponent<GameOrganization.CameraMovement>();
            cameraMovement?.firstLook();
        }
    }

    private void SetupPlayerColor()
    {
        if (_renderer == null) return;

        // Create material instance once
        _renderer.material = new Material(_renderer.material);

        // Set color based on spawn position
        bool isFirstPlayer = transform.position.x < 1f;
        _renderer.material.color = isFirstPlayer ? Color.green : Color.red;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"Player spawned at {transform.position} - Color: {(isFirstPlayer ? "GREEN" : "RED")}");
#endif
    }

    private void SetupNameText()
    {
        if (_nameText == null) return;

        // Cache canvas transform for performance
        _nameCanvas = _nameText.transform.parent;

        // Update name text
        _nameText.text = string.IsNullOrEmpty(PlayerName) ? "Player" : PlayerName;
    }

    public override void FixedUpdateNetwork()
    {
        if (_controller == null) return;

        if (GetInput(out NetworkInputData data))
        {
            // Check ground state
            _isGrounded = CheckGrounded();

            // Update last grounded time for coyote time
            if (_isGrounded)
            {
                _lastGroundedTime = (float)Runner.SimulationTime;
            }

            // Store jump request time (jump buffering)
            if (data.isJumping)
            {
                _jumpRequestTime = (float)Runner.SimulationTime;
            }

            Vector3 direction = data.direction;
            // Use sqrMagnitude for better performance (avoids sqrt calculation)
            bool isMoving = direction.sqrMagnitude > MOVEMENT_THRESHOLD;

            Vector3 velocity = NetworkedVelocity;

            // Horizontal movement
            if (isMoving)
            {
                direction.Normalize();

                // Rotate towards movement direction
                Quaternion targetRotation = Quaternion.LookRotation(direction);
                transform.rotation = Quaternion.Lerp(transform.rotation, targetRotation, _rotationSpeed * Runner.DeltaTime);

                // Calculate speed with sprint multiplier
                float currentSpeed = data.isSprinting ? _moveSpeed * _sprintMultiplier : _moveSpeed;

                // Apply horizontal velocity
                velocity.x = direction.x * currentSpeed;
                velocity.z = direction.z * currentSpeed;
            }
            else
            {
                velocity.x = 0;
                velocity.z = 0;
            }

            // Jump logic with coyote time AND jump buffering
            float currentTime = (float)Runner.SimulationTime;
            float timeSinceGrounded = currentTime - _lastGroundedTime;
            float timeSinceJumpRequest = currentTime - _jumpRequestTime;

            // Can jump if:
            // 1. Currently grounded OR within coyote time after leaving ground
            // 2. Jump was requested recently (within buffer time)
            // 3. Not already jumping upwards
            bool canJump = (_isGrounded || timeSinceGrounded <= _coyoteTime);
            bool hasJumpRequest = timeSinceJumpRequest <= _jumpBufferTime;
            bool notJumpingUp = velocity.y <= 1f; // Allow jump if velocity is low

            if (canJump && hasJumpRequest && notJumpingUp)
            {
                velocity.y = _jumpForce;
                _jumpRequestTime = -999f; // Consume jump request
                _lastGroundedTime = -999f; // Prevent double jump from coyote time

                // Increment jump counter for network sync
                JumpCounter++;
                IsJumping = true;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log($"JUMP! Ground: {_isGrounded}, CoyoteTime: {timeSinceGrounded:F3}s, BufferTime: {timeSinceJumpRequest:F3}s");
#endif
            }
            else if (_isGrounded)
            {
                IsJumping = false;
            }

            // Apply gravity
            if (_isGrounded && velocity.y < 0)
            {
                velocity.y = GROUND_STICK_FORCE;
            }
            else
            {
                velocity.y += _gravity * Runner.DeltaTime;
            }

            // Move character
            _controller.Move(velocity * Runner.DeltaTime);

            // Update networked state
            NetworkedVelocity = velocity;
            IsWalking = isMoving;
            IsRunning = isMoving && data.isSprinting;

            // Pickup logic
            HandlePickup(data);
        }
    }

    private void HandlePickup(NetworkInputData data)
    {
        // F key - Drop cube
        if (data.isDroppingCube && IsCarrying)
        {
            DropCube();
            return;
        }

        // E key - Pickup cube
        if (data.isPickingUp && !IsCarrying)
        {
            TryPickupCube();
        }
    }

    private void TryPickupCube()
    {
        Debug.Log($"[TryPickupCube] 🔍 Searching for cubes in range {_pickupRange}m from position {transform.position}");

        // Find all pickupable cubes in range
        Collider[] colliders = Physics.OverlapSphere(transform.position, _pickupRange);
        Debug.Log($"[TryPickupCube] Found {colliders.Length} colliders in range");

        PickupableCube closestCube = null;
        float closestDistance = float.MaxValue;

        foreach (var collider in colliders)
        {
            var cube = collider.GetComponent<PickupableCube>();
            if (cube != null)
            {
                Debug.Log($"[TryPickupCube] Found PickupableCube: IsPickedUp={cube.IsPickedUp}, Distance={Vector3.Distance(transform.position, cube.transform.position):F2}");

                if (!cube.IsPickedUp)
                {
                    float distance = Vector3.Distance(transform.position, cube.transform.position);
                    if (distance < closestDistance)
                    {
                        closestDistance = distance;
                        closestCube = cube;
                    }
                }
            }
        }

        if (closestCube != null)
        {
            Debug.Log($"[TryPickupCube] ✅ Found cube at distance {closestDistance:F2}, starting pickup");

            // Cache the cube and components
            _carriedCube = closestCube;
            _carriedCubeCol = closestCube.GetComponent<Collider>();

            // Reset carry weight to start transition
            _currentCarryWeight = 0f;

            // Trigger animation
            if (_animator != null)
            {
                _animator.SetTrigger("Take");
            }

            // Send RPC to server
            RPC_RequestPickup(closestCube.Id);

            // DEBUG: Check cube components
            MeshRenderer cubeMesh = closestCube.GetComponent<MeshRenderer>();
            Debug.Log($"[TryPickupCube] ✅ Pickup initiated!");
            Debug.Log($"[TryPickupCube] Cube position: {closestCube.transform.position}");
            Debug.Log($"[TryPickupCube] MeshRenderer: {(cubeMesh != null ? "EXISTS" : "MISSING")} | Enabled: {(cubeMesh != null ? cubeMesh.enabled.ToString() : "N/A")}");
            Debug.Log($"[TryPickupCube] Collider: {(_carriedCubeCol != null ? "EXISTS" : "MISSING")}");
        }
        else
        {
            Debug.LogWarning($"[TryPickupCube] ❌ No cube found in range! Searched {colliders.Length} colliders.");
        }
    }





    private void DropCube()
    {
        // Call RPC to request drop from server
        RPC_RequestDrop();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[DropCube] Requesting drop from server...");
#endif
    }

    /// <summary>
    /// RPC to request picking up a cube (called by client, executed on server)
    /// </summary>
    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_RequestPickup(NetworkBehaviourId cubeId, RpcInfo info = default)
    {
        // Find the cube
        if (!Runner.TryFindBehaviour(cubeId, out PickupableCube cube)) return;
        if (cube.IsPickedUp) return; // Already picked up

        // Update networked state (server-side)
        IsCarrying = true;
        CarriedCubeId = cubeId;

        // Update cube state (server-side)
        cube.IsPickedUp = true;
        cube.PickedUpBy = Object.InputAuthority;

        // Parent cube to left hand on server
        if (_leftHandBone != null)
        {
            // CRITICAL: Disable NetworkTransform completely while being carried
            // This prevents interpolation conflicts between parent transform and NetworkTransform
            NetworkTransform cubeNetTransform = cube.GetComponent<NetworkTransform>();
            if (cubeNetTransform != null)
            {
                cubeNetTransform.enabled = false;
                Debug.Log($"[RPC_RequestPickup] 🔴 SERVER: NetworkTransform DISABLED (will stay disabled while carried)");
            }

            cube.transform.SetParent(_leftHandBone);
            cube.transform.localPosition = _cubeHoldOffset;
            cube.transform.localRotation = Quaternion.Euler(_cubeHoldRotation);

            Debug.Log($"[RPC_RequestPickup] ✅ SERVER: Cube parented to LeftHand at local pos {_cubeHoldOffset}");
        }

        // Call RPC to all clients to disable collider and setup pickup
        RPC_OnPickupConfirmed(cubeId);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[PlayerController] Server: Picked up cube {cubeId}");
#endif
    }

    /// <summary>
    /// RPC to confirm pickup on all clients (called by server, executed on all clients)
    /// </summary>
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_OnPickupConfirmed(NetworkBehaviourId cubeId, RpcInfo info = default)
    {
        // Trigger Take animation
        if (_animator != null)
        {
            _animator.SetTrigger("Take");
        }

        // Force carry weight to start transitioning
        _currentCarryWeight = 0f;

        // Make sure collider is disabled on all clients
        if (Runner.TryFindBehaviour(cubeId, out PickupableCube cube))
        {
            Collider col = cube.GetComponent<Collider>();
            MeshRenderer meshRenderer = cube.GetComponent<MeshRenderer>();

            if (col != null)
            {
                col.isTrigger = true;
                col.enabled = false;
                Debug.Log($"[RPC_OnPickupConfirmed] ✅ Collider DISABLED: enabled={col.enabled}, isTrigger={col.isTrigger}, HasInputAuthority={Object.HasInputAuthority}");
            }

            // Parent cube to left hand on ALL clients
            if (_leftHandBone != null)
            {
                // CRITICAL: Disable NetworkTransform completely while being carried
                // This prevents interpolation conflicts between parent transform and NetworkTransform
                NetworkTransform cubeNetTransform = cube.GetComponent<NetworkTransform>();
                if (cubeNetTransform != null)
                {
                    cubeNetTransform.enabled = false;
                    Debug.Log($"[RPC_OnPickupConfirmed] 🔴 CLIENT: NetworkTransform DISABLED (will stay disabled while carried)");
                }

                cube.transform.SetParent(_leftHandBone);
                cube.transform.localPosition = _cubeHoldOffset;
                cube.transform.localRotation = Quaternion.Euler(_cubeHoldRotation);

                Debug.Log($"[RPC_OnPickupConfirmed] ✅ CLIENT: Cube parented to LeftHand, local pos: {cube.transform.localPosition}, world pos: {cube.transform.position}");
            }
            else
            {
                Debug.LogError($"[RPC_OnPickupConfirmed] ❌ CLIENT: _leftHandBone is NULL!");
            }

            // CRITICAL: Make sure MeshRenderer is enabled
            if (meshRenderer != null)
            {
                if (!meshRenderer.enabled)
                {
                    meshRenderer.enabled = true;
                    Debug.LogError($"[RPC_OnPickupConfirmed] ❌ CLIENT: MeshRenderer was DISABLED! Re-enabled it.");
                }
                Debug.Log($"[RPC_OnPickupConfirmed] ✅ CLIENT: MeshRenderer enabled: {meshRenderer.enabled}, Cube pos: {cube.transform.position}");
            }
            else
            {
                Debug.LogError($"[RPC_OnPickupConfirmed] ❌ CLIENT: NO MeshRenderer on cube!");
            }
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[PlayerController] Client: Pickup confirmed for cube {cubeId}");
#endif
    }

    /// <summary>
    /// RPC to request dropping the carried cube (called by client, executed on server)
    /// </summary>
    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_RequestDrop(RpcInfo info = default)
    {
        // Find the carried cube
        if (CarriedCubeId == default) return;
        if (!Runner.TryFindBehaviour(CarriedCubeId, out PickupableCube cube)) return;

        // Store cube ID before clearing
        NetworkBehaviourId droppedCubeId = CarriedCubeId;

        // Store current world position and rotation BEFORE unparenting
        Vector3 dropPosition = cube.transform.position;
        Quaternion dropRotation = cube.transform.rotation;

        // Unparent the cube FIRST (while NetworkTransform is still disabled)
        cube.transform.SetParent(null);

        // Set the world position and rotation
        cube.transform.position = dropPosition;
        cube.transform.rotation = dropRotation;

        // Get NetworkTransform
        NetworkTransform cubeNetTransform = cube.GetComponent<NetworkTransform>();

        // Re-enable NetworkTransform AFTER position is set
        if (cubeNetTransform != null)
        {
            cubeNetTransform.enabled = true;
            Debug.Log($"[RPC_RequestDrop] 🟢 SERVER: NetworkTransform RE-ENABLED at position {dropPosition}");

            // Immediately teleport to the current position to reset interpolation state
            cubeNetTransform.Teleport(dropPosition, dropRotation);
            Debug.Log($"[RPC_RequestDrop] � SERVER: NetworkTransform teleported to {dropPosition}");
        }

        Debug.Log($"[RPC_RequestDrop] ✅ SERVER: Cube dropped at position {dropPosition}");

        // Update networked state (server-side)
        IsCarrying = false;
        CarriedCubeId = default;

        // Update cube state (server-side)
        cube.IsPickedUp = false;
        cube.PickedUpBy = PlayerRef.None;

        // Notify all clients to re-enable collider and reset carry weight
        // Send the drop position and rotation to ensure all clients use the same values
        RPC_OnDropConfirmed(droppedCubeId, dropPosition, dropRotation);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[RPC_RequestDrop] Server: Dropped cube");
#endif
    }

    /// <summary>
    /// RPC to confirm drop on all clients (called by server, executed on all clients)
    /// </summary>
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_OnDropConfirmed(NetworkBehaviourId cubeId, Vector3 dropPosition, Quaternion dropRotation, RpcInfo info = default)
    {
        // Start coroutine to handle drop with proper timing
        StartCoroutine(HandleDropConfirmedWithDelay(cubeId, dropPosition, dropRotation));

        // Clear local cache
        _carriedCube = null;
        _carriedCubeCol = null;

        // Reset carry weight to transition back to 0
        _currentCarryWeight = 1f; // Start from 1 and let it transition to 0

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[RPC_OnDropConfirmed] Client: Drop confirmed for cube {cubeId}, carry weight will transition to 0");
#endif
    }

    private System.Collections.IEnumerator HandleDropConfirmedWithDelay(NetworkBehaviourId cubeId, Vector3 dropPosition, Quaternion dropRotation)
    {
        if (Runner.TryFindBehaviour(cubeId, out PickupableCube cube))
        {
            // Re-enable collider
            Collider col = cube.GetComponent<Collider>();
            if (col != null)
            {
                col.enabled = true;
                col.isTrigger = false;
                Debug.Log($"[HandleDropConfirmedWithDelay] Collider RE-ENABLED");
            }

            // Unparent the cube FIRST (while NetworkTransform is still disabled)
            cube.transform.SetParent(null);

            // Set the world position and rotation from server
            cube.transform.position = dropPosition;
            cube.transform.rotation = dropRotation;

            Debug.Log($"[HandleDropConfirmedWithDelay] Position set to {dropPosition}");

            // Get NetworkTransform
            NetworkTransform cubeNetTransform = cube.GetComponent<NetworkTransform>();

            // CRITICAL: Only re-enable NetworkTransform on SERVER
            // Clients will receive position updates from server automatically
            if (cubeNetTransform != null && Object.HasStateAuthority)
            {
                cubeNetTransform.enabled = true;
                cubeNetTransform.Teleport(cube.transform.position, cube.transform.rotation);
                Debug.Log($"[HandleDropConfirmedWithDelay] SERVER: NetworkTransform RE-ENABLED and teleported to {cube.transform.position}");
            }
            else if (cubeNetTransform != null)
            {
                // On clients, keep NetworkTransform disabled and just set position manually
                Debug.Log($"[HandleDropConfirmedWithDelay] CLIENT: NetworkTransform stays DISABLED, position set manually to {cube.transform.position}");
                Debug.Log($"[RPC_OnDropConfirmed] � CLIENT: NetworkTransform teleported to {dropPosition}");
            }

            Debug.Log($"[HandleDropConfirmedWithDelay] Cube dropped at position {cube.transform.position}");
        }

        yield return null;
    }

    public override void Render()
    {
        // Update animator every frame (runs on all clients)
        if (_animator != null)
        {
            _animator.SetBool("Walk", IsWalking);
            _animator.SetBool("Run", IsRunning);

            // Trigger jump animation when counter changes (network synced)
            if (JumpCounter != _lastJumpCounter)
            {
                _animator.SetTrigger("Jump");
                _lastJumpCounter = JumpCounter;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log($"[Render] Jump animation triggered! Counter: {JumpCounter}");
#endif
            }

            // Update Carry layer weight with smooth transition (1 second)
            int carryLayerIndex = _animator.GetLayerIndex("Carry");
            if (carryLayerIndex >= 0)
            {
                float targetWeight = IsCarrying ? 1f : 0f;

                // Smoothly interpolate to target weight
                _currentCarryWeight = Mathf.MoveTowards(_currentCarryWeight, targetWeight, CARRY_TRANSITION_SPEED * Time.deltaTime);
                _animator.SetLayerWeight(carryLayerIndex, _currentCarryWeight);
            }
        }

        // Update carried cube position (runs on all clients)
        UpdateCarriedCube();

        // Update name text to face the LOCAL camera (billboard effect)
        if (_nameCanvas != null)
        {
            // Cache Camera.main if not already cached or if it changed
            if (_mainCamera == null || !_mainCamera.enabled)
            {
                _mainCamera = Camera.main;
            }

            if (_mainCamera != null)
            {
                // Update name text only if changed (avoid string allocation)
                if (_nameText != null)
                {
                    string displayName = string.IsNullOrEmpty(PlayerName) ? "Player" : PlayerName;
                    if (_nameText.text != displayName)
                    {
                        _nameText.text = displayName;
                    }
                }

                // Billboard: Always face the local camera
                Vector3 directionToCamera = _mainCamera.transform.position - _nameCanvas.position;
                directionToCamera.y = 0; // Keep text upright

                // Use sqrMagnitude to avoid sqrt calculation
                if (directionToCamera.sqrMagnitude > MIN_CAMERA_DISTANCE_SQR)
                {
                    _nameCanvas.rotation = Quaternion.LookRotation(-directionToCamera);
                }
            }
        }
    }

    private void UpdateCarriedCube()
    {
        // Find the carried cube if we don't have a reference
        if (IsCarrying && _carriedCube == null && CarriedCubeId != default)
        {
            Debug.Log($"[UpdateCarriedCube] 🔍 Trying to find cube with ID: {CarriedCubeId}");

            if (Runner.TryFindBehaviour(CarriedCubeId, out PickupableCube cube))
            {
                _carriedCube = cube;
                _carriedCubeCol = cube.GetComponent<Collider>();

                Debug.Log($"[UpdateCarriedCube] ✅ FOUND CUBE at position: {cube.transform.position}");
            }
        }

        // Clear reference if not carrying
        if (!IsCarrying)
        {
            _carriedCube = null;
            _carriedCubeCol = null;
        }
    }

    // RPC to set player name (called by client, executed on host)
    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_SetPlayerName(string name)
    {
        // Host sets the networked property
        PlayerName = name;
        Debug.Log($"[RPC] Player name set by host: {PlayerName}");
    }
}
