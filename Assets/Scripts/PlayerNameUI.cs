using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PlayerNameUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject _nameInputPanel;
    [SerializeField] private TMP_InputField _nameInputField;
    [SerializeField] private Button _startButton;
    [SerializeField] private TMP_Text _errorText;

    [Header("Settings")]
    [SerializeField] private int _minNameLength = 2;
    [SerializeField] private int _maxNameLength = 15;

    [SerializeField] GameObject mainCamera;

    private FusionConnectionManager _connectionManager;

    // Static property to store player name
    public static string PlayerName { get; private set; } = "Player";

    private void Awake()
    {
        // Cache connection manager reference
        _connectionManager = FindFirstObjectByType<FusionConnectionManager>();

        if (_connectionManager == null)
        {
            Debug.LogError("[PlayerNameUI] FusionConnectionManager not found!");
            return;
        }

        // Disable connection manager until name is entered
        _connectionManager.enabled = false;

        // Setup button listener
        if (_startButton != null)
        {
            _startButton.onClick.AddListener(OnStartButtonClicked);
        }

        // Setup input field
        if (_nameInputField != null)
        {
            _nameInputField.characterLimit = _maxNameLength;
            _nameInputField.onValueChanged.AddListener(OnNameChanged);

            // Enable submit on Enter key
            _nameInputField.onSubmit.AddListener(OnSubmit);

            // Focus input field
            _nameInputField.Select();
            _nameInputField.ActivateInputField();
        }

        // Hide error text initially
        if (_errorText != null)
        {
            _errorText.gameObject.SetActive(false);
        }

        // Show name input panel
        if (_nameInputPanel != null)
        {
            _nameInputPanel.SetActive(true);
        }
    }

    private void OnSubmit(string value)
    {
        // Allow Enter key to submit
        OnStartButtonClicked();
    }

    private void OnNameChanged(string newName)
    {
        // Hide error when user types
        if (_errorText != null)
        {
            _errorText.gameObject.SetActive(false);
        }
    }

    private void OnStartButtonClicked()
    {
        string playerName = _nameInputField != null ? _nameInputField.text.Trim() : "";

        // Validate name
        if (string.IsNullOrEmpty(playerName))
        {
            ShowError("Please enter a name!");
            return;
        }

        if (playerName.Length < _minNameLength)
        {
            ShowError($"Name must be at least {_minNameLength} characters!");
            return;
        }

        // Save player name
        PlayerName = playerName;
        Debug.Log($"[PlayerNameUI] Player name set to: {PlayerName}");
        mainCamera.SetActive(false);

        // Hide UI panel
        if (_nameInputPanel != null)
        {
            _nameInputPanel.SetActive(false);
        }

        // Enable connection manager to start game
        if (_connectionManager != null)
        {
            _connectionManager.enabled = true;
        }
    }

    private void ShowError(string message)
    {
        if (_errorText != null)
        {
            _errorText.text = message;
            _errorText.gameObject.SetActive(true);
        }

        Debug.LogWarning($"[PlayerNameUI] {message}");
    }

    private void OnDestroy()
    {
        // Clean up listeners to prevent memory leaks
        if (_startButton != null)
        {
            _startButton.onClick.RemoveListener(OnStartButtonClicked);
        }

        if (_nameInputField != null)
        {
            _nameInputField.onValueChanged.RemoveListener(OnNameChanged);
            _nameInputField.onSubmit.RemoveListener(OnSubmit);
        }
    }
}

