using Fusion;
using UnityEngine;

public class PlayerController : NetworkBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float _moveSpeed = 5f;
    [SerializeField] private float _rotationSpeed = 10f;
    [SerializeField] private float _gravity = -20f;

    [Header("Sprint Settings")]
    public float sprintMultiplier = 2f;

    [Header("Jump Settings")]
    public float jumpForce = 8f;
    [SerializeField] private float _groundCheckDistance = 0.5f;
    [SerializeField] private float _coyoteTime = 0.2f; // Time after leaving ground where jump is still allowed
    [SerializeField] private float _jumpBufferTime = 0.2f; // Time before landing where jump input is buffered
    [SerializeField] private LayerMask _groundLayer = -1; // All layers by default

    [Header("Animation")]
    [SerializeField] private Animator _animator;

    [Header("Camera")]
    [SerializeField] private GameObject _camera;

    // Networked Properties
    [Networked] public NetworkBool IsWalking { get; set; }
    [Networked] public NetworkBool IsRunning { get; set; }
    [Networked] private Vector3 NetworkedVelocity { get; set; }

    // Cached Components
    private MeshRenderer _renderer;
    private CharacterController _controller;

    // Constants
    private const float MOVEMENT_THRESHOLD = 0.01f; // sqrMagnitude için optimize
    private const float GROUND_STICK_FORCE = -2f;

    // Ground check state
    private bool _isGrounded;
    private float _lastGroundedTime;
    private float _jumpRequestTime = -999f;

    private void Awake()
    {
        _renderer = GetComponent<MeshRenderer>();
        _controller = GetComponent<CharacterController>();
    }

    private bool CheckGrounded()
    {
        bool grounded = false;

        // Method 1: CharacterController built-in check
        if (_controller.isGrounded)
        {
            grounded = true;
        }

        // Method 2: SphereCast from center
        Vector3 center = transform.position + _controller.center;
        float radius = _controller.radius * 0.9f;

        if (Physics.SphereCast(center, radius, Vector3.down, out RaycastHit hit, _groundCheckDistance, _groundLayer))
        {
            grounded = true;
        }

        // Method 3: Multiple raycasts from bottom (most reliable)
        Vector3 bottom = transform.position + Vector3.up * 0.1f;
        float checkRadius = _controller.radius * 0.8f;

        // Center raycast
        if (Physics.Raycast(bottom, Vector3.down, _groundCheckDistance, _groundLayer))
        {
            grounded = true;
        }

        // 4 corner raycasts
        Vector3[] offsets = new Vector3[]
        {
            new Vector3(checkRadius, 0, 0),
            new Vector3(-checkRadius, 0, 0),
            new Vector3(0, 0, checkRadius),
            new Vector3(0, 0, -checkRadius)
        };

        foreach (var offset in offsets)
        {
            if (Physics.Raycast(bottom + offset, Vector3.down, _groundCheckDistance, _groundLayer))
            {
                grounded = true;
                break;
            }
        }

#if UNITY_EDITOR
        // Visual debug - draw raycasts
        Color debugColor = grounded ? Color.green : Color.red;
        Debug.DrawRay(bottom, Vector3.down * _groundCheckDistance, debugColor);
        foreach (var offset in offsets)
        {
            Debug.DrawRay(bottom + offset, Vector3.down * _groundCheckDistance, debugColor);
        }
#endif

        return grounded;
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
        }
        else if (_camera != null)
        {
            _camera.SetActive(false);
        }

        // Setup player color based on spawn position
        SetupPlayerColor();
    }

    private void SetupLocalCamera()
    {
        if (_camera == null) return;

        _camera.transform.parent = null;
        _camera.SetActive(true);

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
                float currentSpeed = data.isSprinting ? _moveSpeed * sprintMultiplier : _moveSpeed;

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
                velocity.y = jumpForce;
                _jumpRequestTime = -999f; // Consume jump request
                _lastGroundedTime = -999f; // Prevent double jump from coyote time

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log($"JUMP! Ground: {_isGrounded}, CoyoteTime: {timeSinceGrounded:F3}s, BufferTime: {timeSinceJumpRequest:F3}s");
#endif
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
        }
    }
}

