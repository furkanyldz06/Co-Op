using Fusion;
using UnityEngine;
using TMPro;

public class PlayerController : NetworkBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float _moveSpeed = 5f;
    [SerializeField] private float _rotationSpeed = 5f; // Lowered for smoother rotation
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
    private Transform _visualChild; // First child (visual model) to rotate

    [Header("Camera")]
    [SerializeField] private GameObject _camera;
    [SerializeField] private float _cameraRotationSpeed = 2f; // Speed of camera rotation when gravity inverts

    [Header("Gravity Toggle Animation")]
    [SerializeField] private float _squashDuration = 0.2f; // Duration to squash to 0
    [SerializeField] private float _stretchDuration = 0.8f; // Duration to stretch back to 1

    [Header("Player Name")]
    [SerializeField] private TMP_Text _nameText;
    [SerializeField] private float _nameYOffset = 2.5f;

    [Header("Pickup System")]
    [SerializeField] private Transform _leftHandBone; // mixamorig:LeftHand
    [SerializeField] private Vector3 _cubeHoldOffset = new Vector3(0.05f, 0.05f, 0.1f); // Local position offset
    [SerializeField] private Vector3 _cubeHoldRotation = new Vector3(0, 0, 0); // Local rotation
    [SerializeField] private float _pickupRange = 2f;
    [SerializeField] private Transform _marker; // mixamorig:LeftHand

    // Networked Properties
    [Networked] public NetworkBool IsWalking { get; set; }
    [Networked] public NetworkBool IsRunning { get; set; }
    [Networked] public NetworkBool IsJumping { get; set; }
    [Networked] private Vector3 NetworkedVelocity { get; set; }
    [Networked] private int JumpCounter { get; set; } // Counter to trigger animation on all clients
    [Networked, Capacity(16)] public string PlayerName { get; set; }
    [Networked] public NetworkBool IsCarrying { get; set; } // Is player carrying a cube?
    [Networked] public NetworkBehaviourId CarriedCubeId { get; set; } // ID of the carried cube


    [SerializeField] private float spherecastRadius = 10f; // Maximum weight player can carry
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

    // Cube highlight tracking
    private PickupableCube _lastHighlightedCube = null;
    private float _lastHighlightUpdateTime = 0f;
    private const float HIGHLIGHT_UPDATE_INTERVAL = 0.1f; // Update every 100ms instead of every frame

    // Carry layer animation
    private float _currentCarryWeight = 0f; // Current carry layer weight
    private const float CARRY_TRANSITION_SPEED = 5f; // Speed of transition (higher = faster)

    // Collider buffer for Physics.OverlapSphere (reuse to avoid allocations)
    private Collider[] _overlapBuffer = new Collider[32]; // Max 32 colliders in range

    // Local gravity state (client-side only for Physics.gravity)
    private bool _isLocalGravityInverted = false;
    private float _normalGravityValue = -9.81f;
    private Vector3 _targetGravity;
    private Vector3 _currentGravity;

    // Networked gravity state (so other clients can see character upside down)
    [Networked] public NetworkBool IsGravityInverted { get; set; }

    // Character rotation for gravity inversion
    private Quaternion _targetCharacterRotation = Quaternion.identity;
    private Quaternion _currentCharacterRotation = Quaternion.identity;

    // Camera rotation for gravity inversion
    private float _targetCameraRoll = 0f;
    private float _currentCameraRoll = 0f;
    // private float _cameraRotationSpeed = 2f; // Speed of camera roll rotation
    private Transform _cameraTransform;
    private GameOrganization.CameraManager _cameraManager;
    private Vector3 _originalCameraOffset;

    // Q key state tracking (to detect press, not hold)
    private bool _wasQKeyPressed = false;

    // Teleport flag to prevent velocity override during teleport
    private bool _isTeleporting = false;

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
        // Cache first child (visual model)
        if (transform.childCount > 0)
        {
            _visualChild = transform.GetChild(0);
            Debug.Log($"[PlayerController] Visual child cached: {_visualChild.name}");
        }
        else
        {
            Debug.LogError($"[PlayerController] No child found! Player must have a visual child.");
        }

        // Initialize gravity state
        _isLocalGravityInverted = false;

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
            // Cache the actual Camera's transform for gravity rotation
            _cameraTransform = cam.transform;
            Debug.Log($"[SetupLocalCamera] Camera transform cached: {_cameraTransform.name}");
        }
        else
        {
            // Fallback to parent if no Camera component found
            _cameraTransform = _camera.transform;
            Debug.LogWarning($"[SetupLocalCamera] No Camera component found, using parent transform");
        }

        _cameraManager = _camera.GetComponent<GameOrganization.CameraManager>();
        if (_cameraManager != null)
        {
            _cameraManager.followObj = transform;

            // Store original camera offset
            _originalCameraOffset = _cameraManager.offset;

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

            // Invert X direction when gravity is inverted (fixes A/D controls)
            // Use networked state for consistency
            if (IsGravityInverted)
            {
                direction.x = -direction.x;
            }

            // Use sqrMagnitude for better performance (avoids sqrt calculation)
            bool isMoving = direction.sqrMagnitude > MOVEMENT_THRESHOLD;

            Vector3 velocity = NetworkedVelocity;

            // Horizontal movement
            if (isMoving)
            {
                direction.Normalize();

                // Rotate towards movement direction (only Y axis - gravity rotation handled in Render())
                Quaternion movementRotation = Quaternion.LookRotation(direction);
                float targetYAngle = movementRotation.eulerAngles.y;

                // Get current rotation and only update Y, preserve X and Z
                Vector3 currentEuler = transform.rotation.eulerAngles;
                float newYAngle = Mathf.LerpAngle(currentEuler.y, targetYAngle, _rotationSpeed * Runner.DeltaTime);

                // Apply new Y rotation while preserving X and Z (gravity rotation)
                transform.rotation = Quaternion.Euler(currentEuler.x, newYAngle, currentEuler.z);

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
            // 3. Not already jumping in the current gravity direction
            bool canJump = (_isGrounded || timeSinceGrounded <= _coyoteTime);
            bool hasJumpRequest = timeSinceJumpRequest <= _jumpBufferTime;

            // Check if not already jumping (depends on gravity direction)
            // Use networked state for consistency
            bool notJumping = IsGravityInverted
                ? velocity.y >= -1f  // Inverted: allow jump if not moving down fast
                : velocity.y <= 1f;   // Normal: allow jump if not moving up fast

            if (canJump && hasJumpRequest && notJumping)
            {
                // Jump direction depends on gravity (use networked state)
                velocity.y = IsGravityInverted ? -_jumpForce : _jumpForce;

                _jumpRequestTime = -999f; // Consume jump request
                _lastGroundedTime = -999f; // Prevent double jump from coyote time

                // Increment jump counter for network sync
                JumpCounter++;
                IsJumping = true;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log($"JUMP! Ground: {_isGrounded}, CoyoteTime: {timeSinceGrounded:F3}s, BufferTime: {timeSinceJumpRequest:F3}s, Inverted: {IsGravityInverted}");
#endif
            }
            else if (_isGrounded)
            {
                IsJumping = false;
            }

            // Handle local gravity toggle (only for local player)
            // Detect Q key press (not hold) - toggle only on press
            if (Object.HasInputAuthority)
            {
                bool isQKeyPressed = data.isTogglingGravity;

                // Toggle only when Q is pressed (transition from not pressed to pressed)
                if (isQKeyPressed && !_wasQKeyPressed)
                {
                    _isLocalGravityInverted = !_isLocalGravityInverted;

                    // Calculate teleport position BEFORE sending RPC
                    float targetY = _isLocalGravityInverted ? -7f : -2.5f;

                    // Send RPC to server to update networked state AND teleport
                    // Server will reset velocity and call RPC_StartGravityAnimation on all clients
                    RPC_ToggleGravity(_isLocalGravityInverted, targetY);

                    Debug.Log($"[PlayerController] Gravity toggle requested! Inverted: {_isLocalGravityInverted}, TargetY: {targetY}");
                }

                // Update previous state
                _wasQKeyPressed = isQKeyPressed;
            }

            // NOTE: We don't change Physics.gravity anymore!
            // Each player uses their own gravity direction via characterGravity (line 507)
            // This allows multiple players to have different gravity directions simultaneously

            // Apply gravity to character
            // Use the networked gravity state for consistency across all clients
            float characterGravity = IsGravityInverted ? -_gravity : _gravity;

            // Ground stick logic - depends on gravity direction
            if (IsGravityInverted)
            {
                // Inverted gravity - stick to ceiling
                if (_isGrounded && velocity.y > 0)
                {
                    velocity.y = -GROUND_STICK_FORCE; // Reverse stick force
                }
                else
                {
                    velocity.y += characterGravity * Runner.DeltaTime;
                }
            }
            else
            {
                // Normal gravity - stick to ground
                if (_isGrounded && velocity.y < 0)
                {
                    velocity.y = GROUND_STICK_FORCE;
                }
                else
                {
                    velocity.y += characterGravity * Runner.DeltaTime;
                }
            }

            // Move character (only if controller is enabled)
            if (_controller.enabled)
            {
                _controller.Move(velocity * Runner.DeltaTime);
            }

            // Update networked state (but don't override velocity during teleport)
            if (!_isTeleporting)
            {
                NetworkedVelocity = velocity;
            }
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

        // OPTIMIZATION: Use non-allocating OverlapSphereNonAlloc
        int hitCount = Physics.OverlapSphereNonAlloc(transform.position, _pickupRange, _overlapBuffer);
        Debug.Log($"[TryPickupCube] Found {hitCount} colliders in range");

        PickupableCube closestCube = null;
        float closestDistanceSqr = float.MaxValue; // Use sqrMagnitude to avoid sqrt

        for (int i = 0; i < hitCount; i++)
        {
            var cube = _overlapBuffer[i].GetComponent<PickupableCube>();
            if (cube != null)
            {
                if (!cube.IsPickedUp)
                {
                    // OPTIMIZATION: Use sqrMagnitude instead of Distance
                    float distanceSqr = (transform.position - cube.transform.position).sqrMagnitude;
                    if (distanceSqr < closestDistanceSqr)
                    {
                        closestDistanceSqr = distanceSqr;
                        closestCube = cube;
                    }
                }
            }
        }

        if (closestCube != null)
        {
            Debug.Log($"[TryPickupCube] ✅ Found cube at distance {Mathf.Sqrt(closestDistanceSqr):F2}, starting pickup");

            // Remove highlight from cube (we're picking it up)
            closestCube.SetHighlight(false);
            _lastHighlightedCube = null;

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
            // Debug.LogWarning($"[TryPickupCube] ❌ No cube found in range! Searched {colliders.Length} colliders.");
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

        // IMMEDIATELY clear carrying state (before any other processing)
        // This ensures UI updates instantly on all clients
        IsCarrying = false;
        CarriedCubeId = default;

        // Store current world position and rotation BEFORE unparenting
        Vector3 dropPosition = _marker.position;
        // Vector3 dropPosition = cube.transform.position;
        Quaternion dropRotation = cube.transform.rotation;

        // RAYCAST: Find ground below the cube to prevent floating
        Collider cubeCollider = cube.GetComponent<Collider>();
        MeshRenderer meshRenderer = cube.GetComponent<MeshRenderer>();

        // Use MESH bounds instead of collider bounds (mesh might be slightly larger)
        float cubeHalfHeight = 0.5f; // Default fallback

        if (meshRenderer != null)
        {
            // Get the actual visual bounds of the mesh
            cubeHalfHeight = meshRenderer.bounds.extents.y;
            Debug.Log($"[RPC_RequestDrop] 📏 Using MESH bounds - Cube half-height: {cubeHalfHeight}, Mesh bounds: {meshRenderer.bounds.size}");
        }
        else if (cubeCollider != null)
        {
            // Fallback to collider bounds
            cubeHalfHeight = cubeCollider.bounds.extents.y;
            Debug.Log($"[RPC_RequestDrop] 📏 Using COLLIDER bounds - Cube half-height: {cubeHalfHeight}");
        }

        Debug.Log($"[RPC_RequestDrop] 📏 Final half-height: {cubeHalfHeight}, Scale: {cube.transform.localScale}");

        // Temporarily disable cube's own collider to prevent raycast hitting itself
        bool wasColliderEnabled = cubeCollider != null && cubeCollider.enabled;
        if (cubeCollider != null)
        {
            cubeCollider.enabled = false;
        }

        // Cast SPHERE downward from cube CENTER position (thicker detection, won't miss small gaps)
        if (Physics.SphereCast(dropPosition, spherecastRadius, Vector3.down, out RaycastHit hit, maxDistance: 10f))
        {
            // Adjust drop position: ground point + half cube height (so bottom of cube touches ground)
            // Add safety offset to prevent clipping into ground
            float safetyOffset = -0.1f;

            // Use hit.normal to align cube with ground surface (works on slopes!)
            dropPosition = hit.point + hit.normal * (cubeHalfHeight + safetyOffset);

            // Align cube rotation to ground normal BUT preserve Y rotation (yaw)
            // 1. Get the original Y rotation
            float originalYRotation = dropRotation.eulerAngles.y;

            // 2. Create rotation aligned to ground normal
            Quaternion slopeRotation = Quaternion.FromToRotation(Vector3.up, hit.normal);

            // 3. Apply Y rotation on top of slope alignment
            dropRotation = slopeRotation * Quaternion.Euler(0, originalYRotation, 0);

            Debug.Log($"[RPC_RequestDrop] 🎯 Ground found at {hit.point}, normal: {hit.normal}, Y rotation preserved: {originalYRotation}°");
        }
        else
        {
            Debug.Log($"[RPC_RequestDrop] ⚠️ No ground found below cube, dropping at current position {dropPosition}");
        }

        // Re-enable collider (will be set to non-trigger later)
        if (cubeCollider != null && wasColliderEnabled)
        {
            cubeCollider.enabled = true;
        }

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

            // Store starting position for lerp animation
            Vector3 startPosition = cube.transform.position;

            // Set rotation immediately (no animation needed)
            cube.transform.rotation = dropRotation;

            Debug.Log($"[HandleDropConfirmedWithDelay] Starting drop animation from {startPosition} to {dropPosition}");

            // LERP ANIMATION: Smoothly drop cube to ground
            float dropDuration = 0.25f; // 0.25 seconds drop animation
            float elapsedTime = 0f;

            while (elapsedTime < dropDuration)
            {
                elapsedTime += Time.deltaTime;
                float t = elapsedTime / dropDuration;

                // Ease-out curve for more natural drop
                t = 1f - Mathf.Pow(1f - t, 3f);

                cube.transform.position = Vector3.Lerp(startPosition, dropPosition, t);
                yield return null;
            }

            // Ensure final position is exact
            cube.transform.position = dropPosition;
            Debug.Log($"[HandleDropConfirmedWithDelay] Drop animation complete, final position: {dropPosition}");

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
        // Update camera rotation for gravity inversion (only for local player)
        if (Object.HasInputAuthority && _cameraTransform != null)
        {
            // Smoothly interpolate camera roll
            _currentCameraRoll = Mathf.Lerp(_currentCameraRoll, _targetCameraRoll, _cameraRotationSpeed * Time.deltaTime);

            // Apply roll rotation to camera (preserve existing rotation)
            Vector3 currentEuler = _cameraTransform.rotation.eulerAngles;
            _cameraTransform.rotation = Quaternion.Euler(currentEuler.x, currentEuler.y, _currentCameraRoll);

            // Debug log
            if (Time.frameCount % 60 == 0)
            {
                Debug.Log($"[Render] Camera - Target: {_targetCameraRoll}, Current: {_currentCameraRoll}, Final: {_cameraTransform.rotation.eulerAngles.z}");
            }
        }

        // Update target character rotation based on networked state (runs on ALL clients)
        // This ensures remote players see the character upside down
        _targetCharacterRotation = IsGravityInverted
            ? Quaternion.Euler(180, 0, 0)  // Upside down (flip on X axis)
            : Quaternion.identity;          // Normal

        // Smoothly interpolate to target rotation
        _currentCharacterRotation = Quaternion.Slerp(_currentCharacterRotation, _targetCharacterRotation, _rotationSpeed * Time.deltaTime);

        // Apply gravity rotation to FIRST CHILD (visual model)
        if (_visualChild != null)
        {
            // Get current Y rotation from root (movement direction)
            Vector3 rootEuler = transform.rotation.eulerAngles;
            float yAngle = rootEuler.y;

            // When gravity is inverted, flip Y rotation 180 degrees (face opposite direction)
            if (IsGravityInverted)
            {
                yAngle += 180f;
            }

            Quaternion yRotation = Quaternion.Euler(0, yAngle, 0);

            // Apply to visual child: Y rotation (movement) + gravity rotation (X flip)
            _visualChild.rotation = yRotation * _currentCharacterRotation;

            // Update local Y position based on gravity state
            Vector3 localPos = _visualChild.localPosition;
            localPos.y = IsGravityInverted ? 1f : 0f;
            _visualChild.localPosition = localPos;

            // Debug log to check if this is running
            if (Time.frameCount % 60 == 0) // Log every 60 frames
            {
                Debug.Log($"[Render] Player {Object.InputAuthority} - IsGravityInverted: {IsGravityInverted}, YAngle: {yAngle}, ChildRot: {_visualChild.rotation.eulerAngles}");
            }
        }

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

        // Update cube highlighting (only for local player, throttled to reduce CPU usage)
        if (Object.HasInputAuthority && !IsCarrying)
        {
            // Only update every HIGHLIGHT_UPDATE_INTERVAL seconds (100ms)
            if (Time.time - _lastHighlightUpdateTime >= HIGHLIGHT_UPDATE_INTERVAL)
            {
                UpdateCubeHighlight();
                _lastHighlightUpdateTime = Time.time;
            }
        }

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

    private void UpdateCubeHighlight()
    {
        // Use non-allocating OverlapSphereNonAlloc to reuse buffer
        int hitCount = Physics.OverlapSphereNonAlloc(transform.position, _pickupRange, _overlapBuffer);

        PickupableCube closestCube = null;
        float closestDistanceSqr = float.MaxValue; // Use sqrMagnitude to avoid sqrt

        // Find the closest pickupable cube
        for (int i = 0; i < hitCount; i++)
        {
            var cube = _overlapBuffer[i].GetComponent<PickupableCube>();
            if (cube != null && !cube.IsPickedUp)
            {
                // Use sqrMagnitude instead of Distance to avoid expensive sqrt
                float distanceSqr = (transform.position - cube.transform.position).sqrMagnitude;
                if (distanceSqr < closestDistanceSqr)
                {
                    closestDistanceSqr = distanceSqr;
                    closestCube = cube;
                }
            }
        }

        // Update highlight state (only if changed)
        if (closestCube != _lastHighlightedCube)
        {
            // Remove highlight from previous cube
            if (_lastHighlightedCube != null)
            {
                _lastHighlightedCube.SetHighlight(false);
            }

            // Add highlight to new cube
            if (closestCube != null)
            {
                closestCube.SetHighlight(true);
            }

            _lastHighlightedCube = closestCube;
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

    // RPC to toggle gravity (called by client, executed on host, synced to all)
    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_ToggleGravity(bool isInverted, float targetY)
    {
        // Host sets the networked property (this will sync to all clients)
        IsGravityInverted = isInverted;

        // Set teleporting flag to prevent velocity override
        _isTeleporting = true;

        // Reset velocity on server to prevent flying (this will sync to all clients)
        NetworkedVelocity = Vector3.zero;

        // Teleport player on server
        Vector3 newPos = transform.position;
        newPos.y = targetY;

        // Disable CharacterController and teleport
        _controller.enabled = false;
        transform.position = newPos;

        Debug.Log($"[RPC] Gravity toggled by server: {IsGravityInverted}, Teleported to Y: {targetY}, Velocity reset, Teleporting flag set");

        // Re-enable CharacterController after 0.25 seconds
        StartCoroutine(ReEnableControllerAfterDelay(0.25f));

        // Notify all clients to start animation
        RPC_StartGravityAnimation(isInverted);
    }

    // RPC to start gravity animation on all clients
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_StartGravityAnimation(bool isInverted)
    {
        Debug.Log($"[RPC_StartGravityAnimation] Starting animation on client, isInverted: {isInverted}");

        // Set teleporting flag on all clients
        _isTeleporting = true;

        // Force velocity to zero on all clients
        NetworkedVelocity = Vector3.zero;
        Debug.Log($"[RPC_StartGravityAnimation] Velocity reset to zero on client, Teleporting flag set");

        // Start squash animation sequence
        if (_visualChild != null)
        {
            StartCoroutine(GravityToggleSequence(isInverted));
        }
    }

    private System.Collections.IEnumerator ReEnableControllerAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);

        if (_controller != null)
        {
            _controller.enabled = true;

            // Force velocity reset again after re-enabling controller
            NetworkedVelocity = Vector3.zero;

            // Wait one more frame to ensure velocity is synced
            yield return null;

            // Clear teleporting flag to allow normal velocity updates
            _isTeleporting = false;

            Debug.Log($"[ReEnableController] CharacterController re-enabled after {delay}s, velocity reset again, Teleporting flag cleared");
        }
    }

    private System.Collections.IEnumerator GravityToggleSequence(bool isInverted)
    {
        // Phase 1: Wait for squash animation to complete
        
        StartCoroutine(SquashAnimation());

        yield return new WaitForSeconds(0.1f);

        Debug.Log($"[GravityToggleSequence] Squash complete, now updating camera");

        // Phase 2: After squash completes, update camera (only for local player)
        if (Object.HasInputAuthority)
        {
            // Update target camera roll (180° flip)
            _targetCameraRoll = isInverted ? 180f : 0f;
            Debug.Log($"[GravityToggleSequence] Camera roll target updated: {_targetCameraRoll}");

            // Update camera offset (invert Y when gravity is inverted)
            if (_cameraManager != null)
            {
                if (isInverted)
                {
                    // Invert Y offset
                    _cameraManager.offset = new Vector3(
                        _originalCameraOffset.x,
                        -_originalCameraOffset.y,
                        _originalCameraOffset.z
                    );
                }
                else
                {
                    // Restore original offset
                    _cameraManager.offset = _originalCameraOffset;
                }

                Debug.Log($"[GravityToggleSequence] Camera offset updated: {_cameraManager.offset}");
            }
        }
    }

    private System.Collections.IEnumerator SquashAnimation()
    {
        if (_visualChild == null) yield break;

        Vector3 originalScale = _visualChild.localScale;
        float elapsedTime = 0f;

        // Phase 1: Scale Y to 0 in _squashDuration seconds
        while (elapsedTime < _squashDuration)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / _squashDuration;

            Vector3 newScale = originalScale;
            newScale.y = Mathf.Lerp(originalScale.y, 0f, t);
            _visualChild.localScale = newScale;

            yield return null;
        }

        // Ensure Y is exactly 0
        Vector3 squashedScale = originalScale;
        squashedScale.y = 0f;
        _visualChild.localScale = squashedScale;

        elapsedTime = 0f;

        // Phase 2: Scale Y back to 1 in _stretchDuration seconds
        while (elapsedTime < _stretchDuration)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / _stretchDuration;

            Vector3 newScale = originalScale;
            newScale.y = Mathf.Lerp(0f, originalScale.y, t);
            _visualChild.localScale = newScale;

            yield return null;
        }

        // Ensure Y is back to original
        _visualChild.localScale = originalScale;

        Debug.Log($"[SquashAnimation] Animation complete, scale reset to {originalScale}");
    }

    private void OnGUI()
    {
        // Only show for local player
        if (!Object.HasInputAuthority) return;

        // Show gravity state on screen
        GUIStyle style = new GUIStyle();
        style.fontSize = 24;
        style.normal.textColor = _isLocalGravityInverted ? Color.red : Color.green;
        style.fontStyle = FontStyle.Bold;

        string gravityText = _isLocalGravityInverted ? "⬆️ GRAVITY: INVERTED" : "⬇️ GRAVITY: NORMAL";
        GUI.Label(new Rect(10, 10, 400, 30), gravityText, style);

        // Instructions
        GUIStyle instructionStyle = new GUIStyle();
        instructionStyle.fontSize = 16;
        instructionStyle.normal.textColor = Color.white;
        GUI.Label(new Rect(10, 40, 400, 25), "Press Q to toggle your world's gravity", instructionStyle);
    }
}
