using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

public static class VoxelExporter
{
    public static int ExportOBJ(VoxelWorld world, Color32[] palette, string objPath)
    {
        var voxels = new List<VoxelWorld.Voxel>(world.FilledCount());
        world.GetAllVoxels(voxels);
        if (voxels.Count == 0) return 0;

        int minX = int.MaxValue, minY = int.MaxValue, minZ = int.MaxValue;
        foreach (var v in voxels)
        {
            if (v.x < minX) minX = v.x;
            if (v.y < minY) minY = v.y;
            if (v.z < minZ) minZ = v.z;
        }

        string dir = Path.GetDirectoryName(objPath);
        string baseName = Path.GetFileNameWithoutExtension(objPath);
        string mtlPath = Path.Combine(dir, baseName + ".mtl");
        string pngName = baseName + "_palette.png";
        string pngPath = Path.Combine(dir, pngName);

        WritePaletteTexture(palette, pngPath);

        var sb = new StringBuilder(voxels.Count * 400);
        sb.AppendLine("# Exported from Unity Voxel Editor");
        sb.AppendLine($"mtllib {baseName}.mtl");
        sb.AppendLine("o VoxelModel");
        sb.AppendLine("usemtl VoxelPalette");

        int vi = 1, ti = 1, ni = 1;
        int faces = 0;

        foreach (var v in voxels)
        {
            Color32 c = palette[Mathf.Min(v.color, palette.Length - 1)];
            string cs = FormattableString.Invariant(
                $"{c.r / 255f:F4} {c.g / 255f:F4} {c.b / 255f:F4}");

            for (int f = 0; f < 6; f++)
            {
                Vector3Int n = VoxelMesher.Normals[f];
                if (world.IsSolid(v.x + n.x, v.y + n.y, v.z + n.z)) continue;

                int vStart = vi;
                Vector3[] corners = VoxelMesher.Corners[f];
                for (int k = 0; k < 4; k++)
                {
                    float px = v.x - minX + corners[k].x;
                    float py = v.y - minY + corners[k].y;
                    float pz = v.z - minZ + corners[k].z;
                    sb.AppendLine(FormattableString.Invariant($"v {px:F0} {py:F0} {pz:F0} {cs}"));
                    vi++;
                }

                float u = (Mathf.Clamp(v.color, 0, 255) + 0.5f) / 256f;
                sb.AppendLine(FormattableString.Invariant($"vt {u:F6} 0.5"));
                int t = ti++;

                sb.AppendLine($"vn {n.x} {n.y} {n.z}");
                int nn = ni++;

                sb.AppendLine($"f {vStart}/{t}/{nn} {vStart + 1}/{t}/{nn} {vStart + 2}/{t}/{nn}");
                sb.AppendLine($"f {vStart}/{t}/{nn} {vStart + 2}/{t}/{nn} {vStart + 3}/{t}/{nn}");
                faces++;
            }
        }

        File.WriteAllText(objPath, sb.ToString());

        var mtl = new StringBuilder();
        mtl.AppendLine("# Voxel Editor material");
        mtl.AppendLine("newmtl VoxelPalette");
        mtl.AppendLine("Ka 1 1 1");
        mtl.AppendLine("Kd 1 1 1");
        mtl.AppendLine("Ks 0 0 0");
        mtl.AppendLine("d 1");
        mtl.AppendLine("illum 1");
        mtl.AppendLine($"map_Kd {pngName}");
        File.WriteAllText(mtlPath, mtl.ToString());

        return faces;
    }

    static void WritePaletteTexture(Color32[] palette, string pngPath)
    {
        var tex = new Texture2D(256, 1, TextureFormat.RGBA32, false);
        for (int i = 0; i < 256; i++)
            tex.SetPixel(i, 0, i < palette.Length ? (Color)palette[i] : Color.black);
        tex.Apply(false, false);

        byte[] png = tex.EncodeToPNG();
        UnityEngine.Object.Destroy(tex);
        File.WriteAllBytes(pngPath, png);
    }

    public static int ExportVOX(VoxelWorld world, Color32[] palette, string filePath)
    {
        var voxels = new List<VoxelWorld.Voxel>(world.FilledCount());
        world.GetAllVoxels(voxels);
        if (voxels.Count == 0) return 0;

        int minX = int.MaxValue, minY = int.MaxValue, minZ = int.MaxValue;
        int maxX = int.MinValue, maxY = int.MinValue, maxZ = int.MinValue;
        foreach (var v in voxels)
        {
            if (v.x < minX) minX = v.x; if (v.x > maxX) maxX = v.x;
            if (v.y < minY) minY = v.y; if (v.y > maxY) maxY = v.y;
            if (v.z < minZ) minZ = v.z; if (v.z > maxZ) maxZ = v.z;
        }

        int sizeX = maxX - minX + 1;
        int sizeY = maxZ - minZ + 1;
        int sizeZ = maxY - minY + 1;

        if (sizeX > 256 || sizeY > 256 || sizeZ > 256)
            throw new System.Exception(
                $"модель {sizeX}x{sizeY}x{sizeZ} не помещается в формат VOX");

        var sizeContent = new byte[12];
        System.Buffer.BlockCopy(new[] { sizeX, sizeY, sizeZ }, 0, sizeContent, 0, 12);

        var xyzi = new byte[4 + voxels.Count * 4];
        System.Buffer.BlockCopy(new[] { voxels.Count }, 0, xyzi, 0, 4);
        for (int i = 0; i < voxels.Count; i++)
        {
            var v = voxels[i];
            int o = 4 + i * 4;
            xyzi[o]     = (byte)(v.x - minX);
            xyzi[o + 1] = (byte)(v.z - minZ);
            xyzi[o + 2] = (byte)(v.y - minY);
            xyzi[o + 3] = (byte)Mathf.Clamp(v.color + 1, 1, 255);
        }

        var rgba = new byte[1024];
        for (int i = 0; i < 256; i++)
        {
            Color32 c = i < palette.Length ? palette[i] : new Color32(0, 0, 0, 255);
            rgba[i * 4]     = c.r;
            rgba[i * 4 + 1] = c.g;
            rgba[i * 4 + 2] = c.b;
            rgba[i * 4 + 3] = 255;
        }

        int childrenSize = (12 + sizeContent.Length) + (12 + xyzi.Length) + (12 + rgba.Length);

        using (var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write))
        using (var w = new BinaryWriter(fs))
        {
            w.Write(0x20584F56);
            w.Write(150);

            WriteChunk(w, "MAIN", new byte[0], childrenSize);
            WriteChunk(w, "SIZE", sizeContent);
            WriteChunk(w, "XYZI", xyzi);
            WriteChunk(w, "RGBA", rgba);
        }
        return voxels.Count;
    }

    static void WriteChunk(BinaryWriter w, string id, byte[] content, int childrenSize = 0)
    {
        w.Write(Encoding.ASCII.GetBytes(id));
        w.Write(content.Length);
        w.Write(childrenSize);
        if (content.Length > 0) w.Write(content);
    }
}