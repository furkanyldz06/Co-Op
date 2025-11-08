using Fusion;
using UnityEngine;

public class PlayerController : NetworkBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float _moveSpeed = 5f;
    
    [Header("Visual Settings")]
    [SerializeField] private MeshRenderer _renderer;
    
    private CharacterController _controller;
    private Vector3 _velocity;

    private void Awake()
    {
        _controller = GetComponent<CharacterController>();
        
        // Eğer CharacterController yoksa ekle
        if (_controller == null)
        {
            _controller = gameObject.AddComponent<CharacterController>();
        }
    }

    public override void Spawned()
    {
        // Kendi oyuncumuz ise farklı renk ver
        if (Object.HasInputAuthority)
        {
            if (_renderer != null)
            {
                _renderer.material.color = Color.green; // Kendi karakterimiz yeşil
            }
        }
        else
        {
            if (_renderer != null)
            {
                _renderer.material.color = Color.red; // Diğer oyuncular kırmızı
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
            
            // Yerçekimi ekle
            _velocity.y += Physics.gravity.y * Runner.DeltaTime;
            move.y = _velocity.y * Runner.DeltaTime;
            
            // CharacterController ile hareket et
            _controller.Move(move);
            
            // Yere değiyorsa yerçekimini sıfırla
            if (_controller.isGrounded && _velocity.y < 0)
            {
                _velocity.y = -2f;
            }
        }
    }
}

