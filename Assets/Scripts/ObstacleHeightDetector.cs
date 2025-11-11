using UnityEngine;

/// <summary>
/// Detects obstacle height in front of the player by raycasting forward
/// and adjusting a marker GameObject's Y position dynamically
/// </summary>
public class ObstacleHeightDetector : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform _player;
    [SerializeField] private Transform _heightMarker;

    [Header("Raycast Settings")]
    [SerializeField] private float _raycastDistance = 1f;
    [SerializeField] private float _raycastStartHeight = 0.2f; // Start at foot level
    [SerializeField] private float _raycastStepHeight = 0.1f; // How much to move up each step
    [SerializeField] private float _maxDetectionHeight = 3f; // Maximum height to check
    [SerializeField] private LayerMask _obstacleLayer = -1; // What counts as obstacle

    [Header("Marker Settings")]
    [SerializeField] private float _markerMoveSpeed = 5f; // How fast marker moves up/down

    [Header("Debug")]
    [SerializeField] private bool _showDebugRays = true;
    [SerializeField] private Color _hitRayColor = Color.red;
    [SerializeField] private Color _clearRayColor = Color.green;

    // Current detected obstacle height
    private float _currentObstacleHeight = 0f;
    private float _targetMarkerHeight = 0f;

    // Public property to get current obstacle height
    public float CurrentObstacleHeight => _currentObstacleHeight;

    private void Update()
    {
        if (_player == null)
        {
            Debug.LogWarning("[ObstacleHeightDetector] Player reference is missing!");
            return;
        }

        // Detect obstacle height
        DetectObstacleHeight();

        // Update marker position
        UpdateMarkerPosition();
    }

    private void DetectObstacleHeight()
    {
        // Get player's forward direction (ignore Y component for horizontal raycast)
        Vector3 forwardDirection = _player.forward;
        forwardDirection.y = 0;
        forwardDirection.Normalize();

        // Start position at player's foot level
        Vector3 rayOrigin = _player.position + Vector3.up * _raycastStartHeight;

        float detectedHeight = 0f;
        bool foundClearSpace = false;

        // Cast rays upward until we find clear space or reach max height
        for (float height = _raycastStartHeight; height <= _maxDetectionHeight; height += _raycastStepHeight)
        {
            // Update ray origin height
            rayOrigin = _player.position + Vector3.up * height;

            // Cast ray forward
            bool hitObstacle = Physics.Raycast(rayOrigin, forwardDirection, out RaycastHit hit, _raycastDistance, _obstacleLayer);

            // Debug visualization
            if (_showDebugRays)
            {
                Color rayColor = hitObstacle ? _hitRayColor : _clearRayColor;
                Debug.DrawRay(rayOrigin, forwardDirection * _raycastDistance, rayColor);
            }

            if (!hitObstacle)
            {
                // Found clear space!
                detectedHeight = height;
                foundClearSpace = true;
                break;
            }
        }

        // Update current obstacle height
        if (foundClearSpace)
        {
            _currentObstacleHeight = detectedHeight;
            _targetMarkerHeight = detectedHeight;
        }
        else
        {
            // No clear space found up to max height
            _currentObstacleHeight = _maxDetectionHeight;
            _targetMarkerHeight = _maxDetectionHeight;
        }
    }

    private void UpdateMarkerPosition()
    {
        if (_heightMarker == null)
        {
            return;
        }

        // Marker is a child of player, so we only need to update LOCAL Y position
        // X and Z will follow player automatically due to parent-child relationship
        Vector3 currentLocalPos = _heightMarker.localPosition;
        float targetY = _targetMarkerHeight;

        // Smoothly lerp only the local Y value
        float newY = Mathf.Lerp(currentLocalPos.y, targetY, Time.deltaTime * _markerMoveSpeed);

        // Set local position - only Y changes, X and Z stay as you set them in Inspector
        _heightMarker.localPosition = new Vector3(currentLocalPos.x, newY, currentLocalPos.z);
    }

    private void OnValidate()
    {
        // Auto-assign player if not set
        if (_player == null && transform.parent != null)
        {
            _player = transform.parent;
        }

        // Auto-assign height marker if not set
        if (_heightMarker == null)
        {
            _heightMarker = transform;
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (_player == null) return;

        // Draw detection area
        Vector3 forwardDirection = _player.forward;
        forwardDirection.y = 0;
        forwardDirection.Normalize();

        Vector3 startPos = _player.position + Vector3.up * _raycastStartHeight;
        Vector3 endPos = startPos + forwardDirection * _raycastDistance;

        // Draw detection volume
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(startPos, endPos);

        Vector3 topStart = _player.position + Vector3.up * _maxDetectionHeight;
        Vector3 topEnd = topStart + forwardDirection * _raycastDistance;
        Gizmos.DrawLine(topStart, topEnd);

        // Draw vertical lines
        Gizmos.DrawLine(startPos, topStart);
        Gizmos.DrawLine(endPos, topEnd);

        // Draw current obstacle height
        if (Application.isPlaying)
        {
            Gizmos.color = Color.red;
            Vector3 obstaclePos = _player.position + Vector3.up * _currentObstacleHeight;
            Gizmos.DrawWireSphere(obstaclePos, 0.1f);
        }
    }
#endif
}

