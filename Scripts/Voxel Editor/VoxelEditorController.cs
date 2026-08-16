using UnityEngine;

[RequireComponent(typeof(Camera))]
public class VoxelEditorController : MonoBehaviour
{
    [Header("Ссылки")]
    public VoxelManager voxelManager;
    
    [Header("Окружение")]
    public Collider groundCollider; 
    
    [Header("Настройки")]
    public float maxRaycastDistance = 100f;
    
    private int currentPrefabIndex = 0; 
    private Camera cam;
    private PauseMenu pauseMenu;

    private void Start()
    {
        cam = GetComponent<Camera>();
        if (voxelManager == null) voxelManager = FindObjectOfType<VoxelManager>();
        pauseMenu = FindObjectOfType<PauseMenu>();
    }

    private void Update()
    {
        // Если меню открыто или курсор не захвачен — блокируем строительство
        if (pauseMenu != null && !pauseMenu.IsGameActive()) return;
        if (Cursor.lockState != CursorLockMode.Locked) return;
        
        HandleInput();
    }

    private void HandleInput()
    {
        if (voxelManager == null || voxelManager.voxelPrefabs.Count == 0) return;

        // Переключение блоков колесиком мыши
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        int totalPrefabs = voxelManager.voxelPrefabs.Count;
        if (scroll > 0f) { currentPrefabIndex = (currentPrefabIndex + 1) % totalPrefabs; }
        else if (scroll < 0f) { currentPrefabIndex = (currentPrefabIndex - 1 + totalPrefabs) % totalPrefabs; }

        // Рейкаст из ЦЕНТРА экрана
        Ray ray = cam.ScreenPointToRay(new Vector3(Screen.width / 2f, Screen.height / 2f, 0f));
        
        bool hitSomething = false;
        Vector3Int targetPos = Vector3Int.zero;
        bool isPlacing = false;

        // МАТЕМАТИЧЕСКИЙ РЕЙКАСТ по воксельным данным
        Vector3Int? hitVoxelPos = RaycastVoxels(ray);
        
        if (hitVoxelPos.HasValue)
        {
            hitSomething = true;
            
            if (Input.GetMouseButtonDown(0)) // ЛКМ - Поставить
            {
                Vector3Int normal = GetHitNormal(ray, hitVoxelPos.Value);
                targetPos = hitVoxelPos.Value + normal;
                isPlacing = true;
            }
            else if (Input.GetMouseButtonDown(1)) // ПКМ - Удалить
            {
                targetPos = hitVoxelPos.Value;
                isPlacing = false;
            }
        }
        // Если не попали в воксели, проверяем пол
        else if (groundCollider != null && groundCollider.Raycast(ray, out RaycastHit hit, maxRaycastDistance))
        {
            hitSomething = true;
            if (Input.GetMouseButtonDown(0))
            {
                targetPos = new Vector3Int(
                    Mathf.RoundToInt(hit.point.x),
                    0,
                    Mathf.RoundToInt(hit.point.z)
                );
                isPlacing = true;
            }
        }

        // Применяем действие
        if (hitSomething)
        {
            if (isPlacing)
            {
                if (!voxelManager.HasVoxel(targetPos))
                {
                    voxelManager.SetVoxel(targetPos, currentPrefabIndex);
                }
            }
            else
            {
                voxelManager.RemoveVoxel(targetPos);
            }
        }
    }

    private Vector3Int? RaycastVoxels(Ray ray)
    {
        var voxelPositions = voxelManager.GetAllVoxelPositions();
        
        float closestDistance = maxRaycastDistance;
        Vector3Int? closestVoxel = null;

        foreach (Vector3Int pos in voxelPositions)
        {
            Bounds voxelBounds = new Bounds(pos, Vector3.one);
            
            if (voxelBounds.IntersectRay(ray, out float distance))
            {
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestVoxel = pos;
                }
            }
        }

        return closestVoxel;
    }

    private Vector3Int GetHitNormal(Ray ray, Vector3Int voxelPos)
    {
        Bounds voxelBounds = new Bounds(voxelPos, Vector3.one);
        
        voxelBounds.IntersectRay(ray, out float distance);
        Vector3 hitPoint = ray.GetPoint(distance);
        
        Vector3 offset = hitPoint - voxelPos;
        
        float absX = Mathf.Abs(offset.x);
        float absY = Mathf.Abs(offset.y);
        float absZ = Mathf.Abs(offset.z);
        
        Vector3Int normal = Vector3Int.zero;
        
        if (absX > absY && absX > absZ)
        {
            normal.x = offset.x > 0 ? 1 : -1;
        }
        else if (absY > absX && absY > absZ)
        {
            normal.y = offset.y > 0 ? 1 : -1;
        }
        else
        {
            normal.z = offset.z > 0 ? 1 : -1;
        }
        
        return normal;
    }
}