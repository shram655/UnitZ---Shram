using UnityEngine;
using UnityEngine.UI;

public class PauseMenu : MonoBehaviour
{
    [Header("UI элементы")]
    public GameObject pausePanel;       // Панель меню паузы
    public Button startButton;          // Кнопка "Начать игру"
    public Button continueButton;       // Кнопка "Продолжить"
    public Text statusText;             // Текст статуса (опционально)
    
    private FreeCameraController cameraController;
    private bool isGameActive = false;

    private void Start()
    {
        cameraController = FindObjectOfType<FreeCameraController>();
        
        // Показываем меню при старте
        ShowMenu();
        
        // Настраиваем кнопки
        if (startButton != null)
        {
            startButton.onClick.AddListener(OnStartClicked);
        }
        
        if (continueButton != null)
        {
            continueButton.onClick.AddListener(OnContinueClicked);
            // Скрываем кнопку "Продолжить" при самом первом запуске
            continueButton.gameObject.SetActive(false);
        }
    }

    private void Update()
    {
        // ESC открывает/закрывает меню
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (isGameActive)
            {
                ShowMenu();
            }
            else
            {
                HideMenu();
            }
        }
    }

    private void ShowMenu()
    {
        isGameActive = false;
        
        // Показываем панель
        if (pausePanel != null)
        {
            pausePanel.SetActive(true);
        }
        
        // Показываем курсор
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        
        // Блокируем управление камерой
        if (cameraController != null)
        {
            cameraController.UnlockCursor();
        }
        
        Debug.Log("[PauseMenu] Меню открыто");
    }

    private void HideMenu()
    {
        isGameActive = true;
        
        // Скрываем панель
        if (pausePanel != null)
        {
            pausePanel.SetActive(false);
        }
        
        // Скрываем курсор и захватываем его
        if (cameraController != null)
        {
            cameraController.LockCursor();
        }
        
        Debug.Log("[PauseMenu] Меню закрыто, игра активна");
    }

    private void OnStartClicked()
    {
        // Показываем кнопку "Продолжить" для следующих открытий меню
        if (continueButton != null)
        {
            continueButton.gameObject.SetActive(true);
        }
        
        HideMenu();
    }

    private void OnContinueClicked()
    {
        HideMenu();
    }

    /// <summary>
    /// Публичный метод для проверки состояния игры (нужен для VoxelEditorController)
    /// </summary>
    public bool IsGameActive()
    {
        return isGameActive;
    }
}