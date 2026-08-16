using System.Collections.Generic;
using UnityEngine;

public class VoxelManager : MonoBehaviour
{
    [Header("Настройки")]
    [Tooltip("Список префабов вокселей. Порядок важен!")]
    public List<GameObject> voxelPrefabs;
    
    [Header("Компоненты")]
    public MeshFilter meshFilter;
    public MeshRenderer meshRenderer;
    public MeshCollider meshCollider;

    [Header("Настройки стыковки")]
    [Tooltip("Множитель размера для устранения зазоров. 1.0 = идеальный размер, 1.05 = нахлест 5%")]
    [Range(1.0f, 1.1f)]
    public float overlapMultiplier = 1.05f;

    private Dictionary<Vector3Int, int> voxelData = new Dictionary<Vector3Int, int>();
    private Mesh generatedMesh;
    private Material[] generatedMaterials;
    private Mesh[] sourceMeshes;

    private void Awake()
    {
        if (meshFilter == null) meshFilter = GetComponent<MeshFilter>();
        if (meshRenderer == null) meshRenderer = GetComponent<MeshRenderer>();
        if (meshCollider == null) meshCollider = GetComponent<MeshCollider>();

        generatedMesh = new Mesh();
        generatedMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        meshFilter.mesh = generatedMesh;
        
        int prefabCount = voxelPrefabs.Count;
        generatedMaterials = new Material[prefabCount];
        sourceMeshes = new Mesh[prefabCount];

        for (int i = 0; i < prefabCount; i++)
        {
            if (voxelPrefabs[i] != null)
            {
                // Забираем материал из префаба
                var rend = voxelPrefabs[i].GetComponent<MeshRenderer>();
                if (rend != null && rend.sharedMaterial != null)
                    generatedMaterials[i] = rend.sharedMaterial;

                // Забираем меш и подготавливаем его
                var mf = voxelPrefabs[i].GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null)
                {
                    sourceMeshes[i] = PrepareMesh(mf.sharedMesh);
                }
            }
        }
        meshRenderer.materials = generatedMaterials;
    }

    /// <summary>
    /// Центрирует меш и масштабирует его до 1x1x1 с нахлестом для устранения зазоров.
    /// </summary>
    private Mesh PrepareMesh(Mesh original)
    {
        if (original == null) return null;
        
        Mesh newMesh = Instantiate(original);
        Vector3[] verts = newMesh.vertices;
        
        if (verts.Length == 0) return newMesh;

        // Находим границы меша
        Vector3 min = verts[0];
        Vector3 max = verts[0];
        for (int i = 1; i < verts.Length; i++)
        {
            min = Vector3.Min(min, verts[i]);
            max = Vector3.Max(max, verts[i]);
        }
        
        Vector3 center = (min + max) * 0.5f;
        Vector3 size = max - min;
        
        // Защита от деления на ноль
        if (size.x < 0.001f) size.x = 1f;
        if (size.y < 0.001f) size.y = 1f;
        if (size.z < 0.001f) size.z = 1f;
        
        // Масштабируем с нахлестом
        for (int i = 0; i < verts.Length; i++)
        {
            verts[i] = new Vector3(
                ((verts[i].x - center.x) / size.x) * overlapMultiplier,
                ((verts[i].y - center.y) / size.y) * overlapMultiplier,
                ((verts[i].z - center.z) / size.z) * overlapMultiplier
            );
        }
        
        newMesh.vertices = verts;
        newMesh.RecalculateBounds();
        
        return newMesh;
    }

    public void SetVoxel(Vector3Int pos, int prefabIndex)
    {
        if (prefabIndex < 0 || prefabIndex >= voxelPrefabs.Count) return;
        voxelData[pos] = prefabIndex;
        RebuildMesh();
    }

    public void RemoveVoxel(Vector3Int pos)
    {
        if (voxelData.Remove(pos))
        {
            RebuildMesh();
        }
    }

    public bool HasVoxel(Vector3Int pos)
    {
        return voxelData.ContainsKey(pos);
    }

    /// <summary>
    /// Возвращает все позиции вокселей (для математического рейкаста).
    /// </summary>
    public List<Vector3Int> GetAllVoxelPositions()
    {
        return new List<Vector3Int>(voxelData.Keys);
    }

    private void RebuildMesh()
    {
        VoxelMeshBuilder.BuildMesh(voxelData, sourceMeshes, generatedMesh);
        
        // MeshCollider больше не нужен для рейкаста, но оставим для совместимости
        if (meshCollider != null)
        {
            meshCollider.sharedMesh = null;
            if (generatedMesh.vertexCount > 0)
            {
                meshCollider.sharedMesh = generatedMesh;
            }
        }
    }
}