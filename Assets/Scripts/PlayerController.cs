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

    // Networked Properties
    [Networked] public NetworkBool IsWalking { get; set; }
    [Networked] public NetworkBool IsRunning { get; set; }
    [Networked] public NetworkBool IsJumping { get; set; }
    [Networked] private Vector3 NetworkedVelocity { get; set; }
    [Networked] private int JumpCounter { get; set; } // Counter to trigger animation on all clients
    [Networked, Capacity(16)] public string PlayerName { get; set; }

    // Cached Components
    private MeshRenderer _renderer;
    private CharacterController _controller;
    private Transform _nameCanvas; // Cache canvas transform
    private Camera _mainCamera; // Cache main camera reference

    // Constants
    private const float MOVEMENT_THRESHOLD = 0.01f; // sqrMagnitude için optimize
    private const float GROUND_STICK_FORCE = -2f;
    private const float MIN_CAMERA_DISTANCE_SQR = 0.01f; // For billboard rotation check

    // Ground check state
    private bool _isGrounded;
    private float _lastGroundedTime;
    private float _jumpRequestTime = -999f;
    private int _lastJumpCounter = 0; // Track last jump counter for animation

    private void Awake()
    {
        _renderer = GetComponent<MeshRenderer>();
        _controller = GetComponent<CharacterController>();
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
        }
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

    // RPC to set player name (called by client, executed on host)
    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_SetPlayerName(string name)
    {
        // Host sets the networked property
        PlayerName = name;
        Debug.Log($"[RPC] Player name set by host: {PlayerName}");
    }
}
