using Fusion;
using UnityEngine;

public class PlayerController : NetworkBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float _moveSpeed = 5f;
    [SerializeField] private float _rotationSpeed = 10f;
    [SerializeField] private float _gravity = -20f;

    [Header("Animation")]
    [SerializeField] private Animator _animator;

    [Networked] public NetworkBool IsWalking { get; set; }
    [Networked] private Vector3 NetworkedVelocity { get; set; }

    private MeshRenderer _renderer;
    private CharacterController _controller;

    [SerializeField] private GameObject camera;

    private void Awake()
    {
        _renderer = GetComponent<MeshRenderer>();
        _controller = GetComponent<CharacterController>();
    }

    public override void Spawned()
    {
        // CharacterController'ı başlat
        if (_controller != null)
        {
            _controller.enabled = false;
            _controller.enabled = true;
        }

        // SADECE KENDI KARAKTERIMIZDE KAMERAYI AKTIF ET!
        if (Object.HasInputAuthority)
        {
            // Kamerayı parent'tan ayır ve aktif et
            if (camera != null)
            {
                camera.transform.parent = null;
                camera.SetActive(true);

                // CameraManager'ı ayarla
                var cameraManager = camera.GetComponent<GameOrganization.CameraManager>();
                if (cameraManager != null)
                {
                    cameraManager.followObj = transform;
                    // lookObj'yi public olarak ayarla (Inspector'dan atanacak)
                    // cameraManager.lookObj = transform; // Bu satırı kaldırdık

                    // CameraMovement'ı başlat
                    var cameraMovement = camera.GetComponent<GameOrganization.CameraMovement>();
                    if (cameraMovement != null)
                    {
                        cameraMovement.firstLook();
                    }
                }
            }
        }
        else
        {
            // Diğer oyuncuların kamerasını deaktif et
            if (camera != null)
            {
                camera.SetActive(false);
            }
        }

        // Material oluştur (her oyuncu için ayrı material instance)
        if (_renderer != null)
        {
            // Yeni material instance oluştur
            _renderer.material = new Material(_renderer.material);

            // Rengi HEMEN ayarla - SPAWN POZİSYONUNA GÖRE!
            // İlk oyuncu x=0, ikinci oyuncu x=2
            bool isFirstPlayer = transform.position.x < 1f;

            Debug.Log($"=== SPAWNED DEBUG ===");
            Debug.Log($"Object.InputAuthority: {Object.InputAuthority}");
            Debug.Log($"Object.InputAuthority.PlayerId: {Object.InputAuthority.PlayerId}");
            Debug.Log($"transform.position: {transform.position}");
            Debug.Log($"Object.HasStateAuthority: {Object.HasStateAuthority}");
            Debug.Log($"Object.HasInputAuthority: {Object.HasInputAuthority}");

            if (isFirstPlayer)
            {
                _renderer.material.color = Color.green;
                Debug.Log($"🟢 YEŞİL oyuncu spawn edildi! (PlayerId: {Object.InputAuthority.PlayerId})");
            }
            else
            {
                _renderer.material.color = Color.red;
                Debug.Log($"🔴 KIRMIZI oyuncu spawn edildi! (PlayerId: {Object.InputAuthority.PlayerId})");
            }
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (_controller == null) return;

        if (GetInput(out NetworkInputData data))
        {
            // Hareket yönü
            Vector3 direction = data.direction;
            bool isMoving = direction.magnitude > 0.1f;

            // Velocity hesapla
            Vector3 velocity = NetworkedVelocity;

            // Yatay hareket
            if (isMoving)
            {
                direction.Normalize();

                // Hareket yönüne dön
                Quaternion targetRotation = Quaternion.LookRotation(direction);
                transform.rotation = Quaternion.Lerp(transform.rotation, targetRotation, _rotationSpeed * Runner.DeltaTime);

                // Yatay velocity
                velocity.x = direction.x * _moveSpeed;
                velocity.z = direction.z * _moveSpeed;
            }
            else
            {
                velocity.x = 0;
                velocity.z = 0;
            }

            // Gravity uygula
            if (_controller.isGrounded && velocity.y < 0)
            {
                velocity.y = -2f; // Zemine yapış
            }
            else
            {
                velocity.y += _gravity * Runner.DeltaTime; // Yerçekimi
            }

            // CharacterController ile hareket
            _controller.Move(velocity * Runner.DeltaTime);

            // Velocity'yi network'e kaydet
            NetworkedVelocity = velocity;

            // Animasyon
            IsWalking = isMoving;
        }
    }

    public override void Render()
    {
        // Animator'ı her frame güncelle (tüm clientlarda, IsWalking networked property'den)
        if (_animator != null)
        {
            _animator.SetBool("Walk", IsWalking);
        }
    }
}

