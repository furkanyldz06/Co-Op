using Fusion;
using UnityEngine;

/// <summary>
/// Networked pickupable cube that can be picked up by players
/// </summary>
public class PickupableCube : NetworkBehaviour
{
    [Header("Pickup Settings")]
    [SerializeField] private float _pickupRange = 2f;
    [SerializeField] private LayerMask _playerLayer = -1;

    // Networked Properties
    [Networked] public NetworkBool IsPickedUp { get; set; }
    [Networked] public PlayerRef PickedUpBy { get; set; }

    // Cached Components
    private Rigidbody _rigidbody;
    private Collider _collider;
    private MeshRenderer _meshRenderer;

    private void Awake()
    {
        _rigidbody = GetComponent<Rigidbody>();
        _collider = GetComponent<Collider>();
        _meshRenderer = GetComponent<MeshRenderer>();
    }

    public override void Spawned()
    {
        // Initialize as not picked up
        if (Object.HasStateAuthority)
        {
            IsPickedUp = false;
            PickedUpBy = PlayerRef.None;
        }
    }

    /// <summary>
    /// Check if a player is in range to pick up this cube
    /// </summary>
    public bool IsPlayerInRange(Vector3 playerPosition)
    {
        float distance = Vector3.Distance(transform.position, playerPosition);
        return distance <= _pickupRange;
    }



    public override void Render()
    {
        // Physics is now handled by PlayerController for better synchronization
        // This prevents conflicts between server and client updates

        // DEBUG: Make sure MeshRenderer is always enabled
        if (_meshRenderer != null)
        {
            if (!_meshRenderer.enabled)
            {
                _meshRenderer.enabled = true;
                Debug.LogError($"[PickupableCube] ❌ MeshRenderer was DISABLED! Re-enabled it.");
            }

            // Check if cube is visible
            bool isVisible = _meshRenderer.isVisible;
            if (IsPickedUp && !isVisible)
            {
                Debug.LogWarning($"[PickupableCube] ⚠️ Cube is picked up but NOT VISIBLE! Position: {transform.position}, Layer: {gameObject.layer}, Parent: {(transform.parent != null ? transform.parent.name : "NULL")}");
            }
        }
        else
        {
            Debug.LogError($"[PickupableCube] ❌ NO MeshRenderer component!");
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        // Draw pickup range
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, _pickupRange);
    }
#endif
}

