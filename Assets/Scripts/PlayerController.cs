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

    private void Awake()
    {
        _renderer = GetComponent<MeshRenderer>();
        _controller = GetComponent<CharacterController>();
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

            // Apply gravity
            if (_controller.isGrounded && velocity.y < 0)
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

