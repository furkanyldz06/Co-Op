using Fusion;
using UnityEngine;

public class PlayerController : NetworkBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float _moveSpeed = 5f;

    // Network senkronize pozisyon ve renk
    [Networked] public Vector3 NetworkedPosition { get; set; }
    [Networked] public int PlayerIndex { get; set; }

    private MeshRenderer _renderer;

    private void Awake()
    {
        // MeshRenderer'ı bul
        _renderer = GetComponent<MeshRenderer>();
    }

    public override void Spawned()
    {
        // İlk pozisyonu network'e kaydet
        NetworkedPosition = transform.position;

        // Player index'i ayarla (sadece state authority olan yapabilir)
        if (Object.HasStateAuthority)
        {
            PlayerIndex = Object.InputAuthority.PlayerId;
        }

        // Material oluştur (her oyuncu için ayrı material instance)
        if (_renderer != null)
        {
            // Yeni material instance oluştur
            _renderer.material = new Material(_renderer.material);

            // İlk giren yeşil (PlayerIndex 0), ikinci giren kırmızı (PlayerIndex 1)
            if (PlayerIndex == 0)
            {
                _renderer.material.color = Color.green;
                Debug.Log($"YEŞİL oyuncu spawn edildi! (PlayerIndex: {PlayerIndex})");
            }
            else
            {
                _renderer.material.color = Color.red;
                Debug.Log($"KIRMIZI oyuncu spawn edildi! (PlayerIndex: {PlayerIndex})");
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

