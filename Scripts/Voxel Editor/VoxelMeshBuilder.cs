using System.Collections.Generic;
using UnityEngine;

public static class VoxelMeshBuilder
{
    private static readonly Vector3Int[] directions = {
        Vector3Int.right, Vector3Int.left,
        Vector3Int.up, Vector3Int.down,
        Vector3Int.forward, Vector3Int.back
    };

    public static void BuildMesh(Dictionary<Vector3Int, int> voxelData, Mesh[] sourceMeshes, Mesh mesh)
    {
        mesh.Clear();
        if (voxelData.Count == 0) return;

        int materialCount = sourceMeshes.Length;

        List<Vector3> allVertices = new List<Vector3>();
        List<Vector3> allNormals = new List<Vector3>();
        List<Vector2> allUVs = new List<Vector2>();

        List<List<int>> trianglesPerMat = new List<List<int>>();
        for (int i = 0; i < materialCount; i++)
        {
            trianglesPerMat.Add(new List<int>());
        }

        foreach (var kvp in voxelData)
        {
            Vector3Int pos = kvp.Key;
            int matIndex = kvp.Value;

            // Оптимизация: если воксель полностью окружен, не рисуем его
            bool isHidden = true;
            for (int i = 0; i < 6; i++)
            {
                if (!voxelData.ContainsKey(pos + directions[i]))
                {
                    isHidden = false;
                    break;
                }
            }
            if (isHidden) continue;

            Mesh sourceMesh = sourceMeshes[matIndex];
            if (sourceMesh == null) continue;

            Vector3[] srcVerts = sourceMesh.vertices;
            Vector3[] srcNorms = sourceMesh.normals;
            Vector2[] srcUvs = sourceMesh.uv;
            int[] srcTris = sourceMesh.triangles;

            int baseVertexIndex = allVertices.Count;

            // Позиция вокселя (каждый воксель = 1x1x1, центр в целых координатах)
            Vector3 worldPos = new Vector3(pos.x, pos.y, pos.z);

            for (int i = 0; i < srcVerts.Length; i++)
            {
                allVertices.Add(srcVerts[i] + worldPos);
                allNormals.Add(i < srcNorms.Length ? srcNorms[i] : Vector3.up);
                allUVs.Add(i < srcUvs.Length ? srcUvs[i] : Vector2.zero);
            }

            for (int i = 0; i < srcTris.Length; i++)
            {
                trianglesPerMat[matIndex].Add(srcTris[i] + baseVertexIndex);
            }
        }

        mesh.SetVertices(allVertices);
        mesh.SetNormals(allNormals);
        mesh.SetUVs(0, allUVs);

        mesh.subMeshCount = materialCount;
        for (int i = 0; i < materialCount; i++)
        {
            mesh.SetTriangles(trianglesPerMat[i], i);
        }

        mesh.RecalculateBounds();
    }
}