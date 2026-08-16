using UnityEngine;

[RequireComponent(typeof(Camera))]
public class FreeCameraController : MonoBehaviour
{
    [Header("Настройки движения")]
    public float moveSpeed = 10f;
    public float sprintMultiplier = 3f;
    public float mouseSensitivity = 2f;
    
    private float pitch = 0f;
    private float yaw = 0f;
    
    private bool isCursorLocked = false;

    private void Start()
    {
        Vector3 euler = transform.eulerAngles;
        yaw = euler.y;
        pitch = euler.x;
        
        // Изначально курсор свободен
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void Update()
    {
        // Принудительная синхронизация состояния курсора
        if (isCursorLocked)
        {
            if (Cursor.lockState != CursorLockMode.Locked)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }
        else
        {
            if (Cursor.lockState != CursorLockMode.None)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }
        
        HandleMovement();
        HandleRotation();
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus && isCursorLocked)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    public void LockCursor()
    {
        isCursorLocked = true;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public void UnlockCursor()
    {
        isCursorLocked = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public bool IsCursorLocked()
    {
        return isCursorLocked;
    }

    private void HandleMovement()
    {
        if (!isCursorLocked) return;
        
        float speed = moveSpeed;
        
        if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
        {
            speed *= sprintMultiplier;
        }
        
        Vector3 forward = transform.forward;
        Vector3 right = transform.right;
        
        forward.y = 0f;
        right.y = 0f;
        forward.Normalize();
        right.Normalize();
        
        Vector3 movement = Vector3.zero;
        
        if (Input.GetKey(KeyCode.W)) movement += forward;
        if (Input.GetKey(KeyCode.S)) movement -= forward;
        if (Input.GetKey(KeyCode.D)) movement += right;
        if (Input.GetKey(KeyCode.A)) movement -= right;
        
        if (Input.GetKey(KeyCode.Space)) movement += Vector3.up;
        if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) 
            movement -= Vector3.up;
        
        if (movement.magnitude > 0f)
        {
            transform.position += movement.normalized * speed * Time.deltaTime;
        }
    }

    private void HandleRotation()
    {
        if (!isCursorLocked) return;
        
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;
        
        yaw += mouseX;
        pitch -= mouseY;
        
        pitch = Mathf.Clamp(pitch, -90f, 90f);
        
        transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
    }
}