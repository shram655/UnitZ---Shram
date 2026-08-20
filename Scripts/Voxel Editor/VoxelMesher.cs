using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Сборка меша одного чанка: скрытые грани отсекаются (грань попадает в меш,
// только если сосед за ней пуст), цвета берутся из палитры и пишутся в вершины.
// Освещение полностью делается шейдером Custom/CubeWorldStyle.
public static class VoxelMesher
{
    public static readonly Vector3Int[] Normals =
    {
        new Vector3Int( 1, 0, 0), new Vector3Int(-1, 0, 0),
        new Vector3Int( 0, 1, 0), new Vector3Int( 0,-1, 0),
        new Vector3Int( 0, 0, 1), new Vector3Int( 0, 0,-1),
    };

    public static readonly Vector3[][] Corners =
    {
        new[] { V(1,0,1), V(1,0,0), V(1,1,0), V(1,1,1) }, // +X
        new[] { V(0,0,0), V(0,0,1), V(0,1,1), V(0,1,0) }, // -X
        new[] { V(0,1,1), V(1,1,1), V(1,1,0), V(0,1,0) }, // +Y
        new[] { V(0,0,0), V(1,0,0), V(1,0,1), V(0,0,1) }, // -Y
        new[] { V(0,0,1), V(1,0,1), V(1,1,1), V(0,1,1) }, // +Z
        new[] { V(1,0,0), V(0,0,0), V(0,1,0), V(1,1,0) }, // -Z
    };

    static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);

    public static void BuildChunk(VoxelWorld world, Color32[] palette, Mesh mesh, Vector3Int chunk)
    {
        mesh.Clear();

        var verts   = new List<Vector3>(1024);
        var normals = new List<Vector3>(1024);
        var colors  = new List<Color32>(1024);
        var tris    = new List<int>(1024);

        int ox = chunk.x * VoxelWorld.ChunkSize;
        int oy = chunk.y * VoxelWorld.ChunkSize;
        int oz = chunk.z * VoxelWorld.ChunkSize;

        for (int y = 0; y < VoxelWorld.ChunkSize; y++)
        for (int z = 0; z < VoxelWorld.ChunkSize; z++)
        for (int x = 0; x < VoxelWorld.ChunkSize; x++)
        {
            int wx = ox + x, wy = oy + y, wz = oz + z;
            byte v = world.Get(wx, wy, wz);
            if (v == 0) continue;

            Color32 c = palette[Mathf.Min(v - 1, palette.Length - 1)];

            for (int f = 0; f < 6; f++)
            {
                Vector3Int n = Normals[f];
                if (world.Get(wx + n.x, wy + n.y, wz + n.z) != 0) continue; // грань скрыта

                int start = verts.Count;
                Vector3 o = new Vector3(wx, wy, wz);

                for (int k = 0; k < 4; k++)
                {
                    verts.Add(o + Corners[f][k]);
                    normals.Add(n);
                    colors.Add(c); // чистый цвет палитры — освещение делает шейдер
                }

                tris.Add(start);     tris.Add(start + 1); tris.Add(start + 2);
                tris.Add(start);     tris.Add(start + 2); tris.Add(start + 3);
            }
        }

        mesh.indexFormat = IndexFormat.UInt32;
        mesh.SetVertices(verts);
        mesh.SetNormals(normals);
        mesh.SetColors(colors);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();
    }
}