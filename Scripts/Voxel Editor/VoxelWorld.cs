using System.Collections.Generic;
using UnityEngine;

// Бесконечный воксельный мир на чанках 16^3.
// Чанки создаются по мере необходимости и удаляются, когда пусты.
// 0 — пусто, иначе индекс цвета в палитре + 1.
public class VoxelWorld
{
    public const int ChunkSize = 16;
    const int ChunkVolume = ChunkSize * ChunkSize * ChunkSize;
    const float RayEps = 1e-4f;

    // Один воксель мира (для сохранения/загрузки).
    public struct Voxel
    {
        public int x, y, z;
        public byte color; // индекс цвета в палитре (0-based)
    }

    readonly Dictionary<Vector3Int, byte[]> chunks = new Dictionary<Vector3Int, byte[]>();

    public int ChunkCount => chunks.Count;

    // ------------------------------------------------ координаты

    public static Vector3Int ChunkCoordOf(int x, int y, int z) => new Vector3Int(
        Mathf.FloorToInt(x / (float)ChunkSize),
        Mathf.FloorToInt(y / (float)ChunkSize),
        Mathf.FloorToInt(z / (float)ChunkSize));

    static int LocalIndex(int x, int y, int z, Vector3Int cc)
    {
        int lx = x - cc.x * ChunkSize;
        int ly = y - cc.y * ChunkSize;
        int lz = z - cc.z * ChunkSize;
        return (ly * ChunkSize + lz) * ChunkSize + lx;
    }

    // ------------------------------------------------ чтение / запись

    public byte Get(int x, int y, int z)
    {
        var cc = ChunkCoordOf(x, y, z);
        return chunks.TryGetValue(cc, out var data) ? data[LocalIndex(x, y, z, cc)] : (byte)0;
    }

    public bool IsSolid(int x, int y, int z) => Get(x, y, z) != 0;

    public void Set(int x, int y, int z, byte value)
    {
        var cc = ChunkCoordOf(x, y, z);
        if (!chunks.TryGetValue(cc, out var data))
        {
            if (value == 0) return; // пустой чанк не создаём
            data = new byte[ChunkVolume];
            chunks.Add(cc, data);
        }
        data[LocalIndex(x, y, z, cc)] = value;
    }

    public void SetColorIndex(int x, int y, int z, int colorIndex) =>
        Set(x, y, z, (byte)(colorIndex + 1));

    public bool HasChunk(Vector3Int cc) => chunks.ContainsKey(cc);

    public void Clear() => chunks.Clear();

    // Удаляет чанк из словаря, если в нём не осталось вокселей.
    public void RemoveChunkIfEmpty(Vector3Int cc)
    {
        if (!chunks.TryGetValue(cc, out var data)) return;
        for (int i = 0; i < data.Length; i++)
            if (data[i] != 0) return;
        chunks.Remove(cc);
    }

    public int FilledCount()
    {
        int n = 0;
        foreach (var data in chunks.Values)
            for (int i = 0; i < data.Length; i++)
                if (data[i] != 0) n++;
        return n;
    }

    // Собирает все непустые воксели мира (для сохранения).
    public void GetAllVoxels(List<Voxel> output)
    {
        output.Clear();
        foreach (var kv in chunks)
        {
            Vector3Int cc = kv.Key;
            byte[] data = kv.Value;
            int ox = cc.x * ChunkSize, oy = cc.y * ChunkSize, oz = cc.z * ChunkSize;

            for (int i = 0; i < data.Length; i++)
            {
                byte c = data[i];
                if (c == 0) continue;

                // Обратное преобразование локального индекса: index = ly*256 + lz*16 + lx
                int lx = i % ChunkSize;
                int lz = (i / ChunkSize) % ChunkSize;
                int ly = i / (ChunkSize * ChunkSize);

                output.Add(new Voxel
                {
                    x = ox + lx,
                    y = oy + ly,
                    z = oz + lz,
                    color = (byte)(c - 1)
                });
            }
        }
    }

    // Чанки, которые нужно пересобрать после изменения вокселя:
    // сам чанк + соседи, если воксель стоит на границе чанка
    // (от соседей зависит отсечение скрытых граней).
    public void CollectDirtyChunks(int x, int y, int z, HashSet<Vector3Int> output)
    {
        var cc = ChunkCoordOf(x, y, z);
        output.Add(cc);

        int lx = x - cc.x * ChunkSize;
        int ly = y - cc.y * ChunkSize;
        int lz = z - cc.z * ChunkSize;

        if (lx == 0) output.Add(cc + Vector3Int.left);
        if (lx == ChunkSize - 1) output.Add(cc + Vector3Int.right);
        if (ly == 0) output.Add(cc + Vector3Int.down);
        if (ly == ChunkSize - 1) output.Add(cc + Vector3Int.up);
        if (lz == 0) output.Add(cc + Vector3Int.back);
        if (lz == ChunkSize - 1) output.Add(cc + Vector3Int.forward);
    }

    // ------------------------------------------------ рейкаст

    // Луч сквозь бесконечную сетку. Пустые чанки перепрыгиваются целиком,
    // поэтому полёт луча сквозь пустоту почти бесплатен.
    public bool Raycast(Ray ray, out Vector3Int cell, out Vector3Int normal, float maxDistance = 4000f)
    {
        cell = default; normal = default;
        Vector3 dir = ray.direction;
        if (dir.sqrMagnitude < 1e-8f) return false;
        dir.Normalize();

        Vector3 p = ray.origin;
        var n = Vector3Int.zero;
        float travelled = 0f;

        for (int i = 0; i < 65536; i++)
        {
            int x = Mathf.FloorToInt(p.x);
            int y = Mathf.FloorToInt(p.y);
            int z = Mathf.FloorToInt(p.z);

            if (Get(x, y, z) != 0)
            {
                cell = new Vector3Int(x, y, z);
                normal = n;
                return true;
            }

            // Шаг: в соседнюю ячейку, а если весь чанк пуст — сразу к границе чанка.
            bool chunkEmpty = !HasChunk(ChunkCoordOf(x, y, z));
            float grid = chunkEmpty ? ChunkSize : 1f;

            float dx = AxisDist(p.x, dir.x, grid);
            float dy = AxisDist(p.y, dir.y, grid);
            float dz = AxisDist(p.z, dir.z, grid);

            float d;
            if (dx <= dy && dx <= dz) { d = dx; n = new Vector3Int(dir.x > 0 ? -1 : 1, 0, 0); }
            else if (dy <= dz)        { d = dy; n = new Vector3Int(0, dir.y > 0 ? -1 : 1, 0); }
            else                      { d = dz; n = new Vector3Int(0, 0, dir.z > 0 ? -1 : 1); }

            if (float.IsInfinity(d)) return false;

            travelled += d;
            if (travelled > maxDistance) return false;
            p += dir * (d + RayEps);
        }
        return false;
    }

    // Расстояние от координаты s до выхода из текущего интервала размера grid
    // (grid = 1 — ячейка, grid = ChunkSize — чанк).
    static float AxisDist(float s, float ds, float grid)
    {
        if (Mathf.Abs(ds) < 1e-8f) return float.PositiveInfinity;
        float min = Mathf.Floor(s / grid) * grid;
        float boundary = ds > 0f ? min + grid : min;
        return Mathf.Abs((boundary - s) / ds);
    }
}