using Fusion;
using UnityEngine;

public class PlayerController : NetworkBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float _moveSpeed = 5f;
    [SerializeField] private float _rotationSpeed = 10f; // Rotation speed (higher = faster, lower = slower)

    [Header("Animation")]
    [SerializeField] private Animator _animator;

    // Network senkronize pozisyon ve rotasyon
    [Networked] public Vector3 NetworkedPosition { get; set; }
    [Networked] public Quaternion NetworkedRotation { get; set; }
    [Networked] public NetworkBool IsWalking { get; set; }

    private MeshRenderer _renderer;

    [SerializeField] private GameObject camera;

    private void Awake()
    {
        // MeshRenderer'ı bul
        _renderer = GetComponent<MeshRenderer>();
    }

    public override void Spawned()
    {
        // İlk pozisyonu ve rotasyonu network'e kaydet
        NetworkedPosition = transform.position;
        NetworkedRotation = transform.rotation;

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
        // Sadece input yetkisi olan oyuncu hareket edebilir
        if (GetInput(out NetworkInputData data))
        {
            // Hareket varsa
            bool isMoving = data.direction.magnitude > 0.1f;

            if (isMoving)
            {
                // Hareket yönünü normalize et
                data.direction.Normalize();

                // Hareketi uygula
                Vector3 move = data.direction * _moveSpeed * Runner.DeltaTime;
                transform.position += move;

                // Hareket yönüne doğru yumuşakça dön (Lerp ile)
                Quaternion targetRotation = Quaternion.LookRotation(data.direction);
                transform.rotation = Quaternion.Lerp(transform.rotation, targetRotation, _rotationSpeed * Runner.DeltaTime);

                // Network pozisyon ve rotasyonu güncelle
                NetworkedPosition = transform.position;
                NetworkedRotation = transform.rotation;
            }

            // Animasyon durumunu güncelle (network senkronize)
            IsWalking = isMoving;
        }
        else
        {
            // Input yetkisi yoksa network pozisyon ve rotasyonu kullan
            transform.position = NetworkedPosition;
            transform.rotation = NetworkedRotation;
        }

        // Animator'ı güncelle (tüm clientlarda)
        if (_animator != null)
        {
            _animator.SetBool("Walk", IsWalking);
        }
    }
}

