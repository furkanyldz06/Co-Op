using Fusion;
using UnityEngine;

public class PlayerController : NetworkBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float _moveSpeed = 5f;

    // Network senkronize pozisyon
    [Networked] public Vector3 NetworkedPosition { get; set; }

    private MeshRenderer _renderer;

    [SerializeField] private GameObject camera;

    private void Awake()
    {
        // MeshRenderer'ı bul
        _renderer = GetComponent<MeshRenderer>();
        camera.transform.parent = null;
    }

    public override void Spawned()
    {
        // İlk pozisyonu network'e kaydet
        NetworkedPosition = transform.position;

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
            // Hareket yönünü normalize et
            data.direction.Normalize();

            // Hareketi uygula
            Vector3 move = data.direction * _moveSpeed * Runner.DeltaTime;

            // Pozisyonu direkt güncelle
            transform.position += move;

            // Network pozisyonunu güncelle
            NetworkedPosition = transform.position;
        }
        else
        {
            // Input yetkisi yoksa network pozisyonunu kullan
            transform.position = NetworkedPosition;
        }
    }
}

