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

    [Header("Highlight Settings")]
    [SerializeField] private Color _highlightColor = Color.blue;
    [SerializeField] private Color _originalColor = Color.white;

    // Networked Properties
    [Networked] public NetworkBool IsPickedUp { get; set; }
    [Networked] public PlayerRef PickedUpBy { get; set; }

    // Cached Components
    private Collider _collider;
    private MeshRenderer _meshRenderer;
    private Material _material;
    private bool _isHighlighted = false;

    private void Awake()
    {
        _collider = GetComponent<Collider>();
        _meshRenderer = GetComponent<MeshRenderer>();

        // Get material and store original color
        if (_meshRenderer != null)
        {
            _material = _meshRenderer.material; // Creates instance automatically
            _originalColor = _material.color;
        }
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
    /// Highlight the cube (turn blue when player is in range)
    /// </summary>
    public void SetHighlight(bool highlight)
    {
        if (_material == null) return;

        // Only change if state is different
        if (_isHighlighted == highlight) return;

        _isHighlighted = highlight;
        _material.color = highlight ? _highlightColor : _originalColor;
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

        // OPTIMIZATION: Removed per-frame debug logs to improve performance
        // Only check critical errors in development builds
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (_meshRenderer != null && !_meshRenderer.enabled)
        {
            _meshRenderer.enabled = true;
            Debug.LogError($"[PickupableCube] ❌ MeshRenderer was DISABLED! Re-enabled it.");
        }
#endif
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

