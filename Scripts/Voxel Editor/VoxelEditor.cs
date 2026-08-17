using System.Collections.Generic;
using System.IO;
using UnityEngine;

// Оркестратор редактора: ввод, инструменты, палитра-UI, превью-куб,
// сохранение, загрузка и экспорт мира.
public class VoxelEditor : MonoBehaviour
{
    public enum Tool { Build, Erase, Paint }

    class ChunkView
    {
        public GameObject go;
        public Mesh mesh;
    }

    const int GridSize = 64;
    const int FileMagic = 0x4C584F56;
    const int FileVersion = 1;

    [Header("Настройки")]
    public Material voxelMaterial;
    public int startColorIndex = 25;
    public string saveFileName = "world.voxl";

    [Header("Экспорт")]
    public string exportFileName = "model";

    VoxelWorld world;
    Color32[] palette;
    Camera cam;

    readonly Dictionary<Vector3Int, ChunkView> chunkViews = new Dictionary<Vector3Int, ChunkView>();
    readonly HashSet<Vector3Int> dirtyChunks = new HashSet<Vector3Int>();
    GameObject worldRoot;

    int colorIndex;
    Tool tool = Tool.Build;

    bool hasHover, hoverIsVoxel, overUI;
    Vector3Int hoverCell, hoverNormal;
    Vector3Int lastEdited = new Vector3Int(int.MinValue, 0, 0);
    Vector2 lastApplyPos = new Vector2(-1f, -1f);

    GameObject previewObj;
    Material previewMat;

    const int SwatchSize = 26, PaletteCols = 16;
    Rect paletteRect;
    static GUIStyle hudStyle;

    string statusMessage = "";
    float statusUntil = -1f;

    string SavePath => Path.Combine(Application.persistentDataPath, saveFileName);
    string ExportDir => Path.Combine(Application.dataPath, "VoxelExport");

    void Awake()
    {
        transform.position = Vector3.zero;
        transform.rotation = Quaternion.identity;

        palette = VoxelPalette.Default();
        colorIndex = Mathf.Clamp(startColorIndex, 0, palette.Length - 1);
        world = new VoxelWorld();

        worldRoot = new GameObject("VoxelWorld");
        worldRoot.transform.SetParent(transform);

        if (voxelMaterial == null)
        {
            // Приоритет: новый toon-шейдер. Если его нет — fallback на vertex-color или Standard.
            Shader sh = Shader.Find("Custom/CubeWorldStyle")
                     ?? Shader.Find("Voxel/VertexColor")
                     ?? Shader.Find("Universal Render Pipeline/Lit")
                     ?? Shader.Find("Standard");
            voxelMaterial = new Material(sh);

            // Шейдер ожидает _MainTex; даём ему белую 1x1, чтобы tex2D не давал артефактов.
            var white = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            white.SetPixel(0, 0, Color.white);
            white.Apply(false, false);
            voxelMaterial.SetTexture("_MainTex", white);
        }

        SetupCamera();
        SetupPreview();
        SetupGrid();
        SeedDemo();
        FlushDirty();

        paletteRect = PaletteWindowRect();
        Debug.Log($"VoxelEditor: шейдер — {voxelMaterial.shader.name}");
    }

    void Update()
    {
        HandleKeys();
        UpdateHover();
        HandleEditing();
        UpdatePreview();
    }

    void Place(int x, int y, int z, int color)
    {
        world.SetColorIndex(x, y, z, color);
        world.CollectDirtyChunks(x, y, z, dirtyChunks);
    }

    void Erase(int x, int y, int z)
    {
        world.Set(x, y, z, 0);
        world.CollectDirtyChunks(x, y, z, dirtyChunks);
    }

    void FlushDirty()
    {
        foreach (var cc in dirtyChunks)
        {
            world.RemoveChunkIfEmpty(cc);

            if (!world.HasChunk(cc))
            {
                if (chunkViews.TryGetValue(cc, out var dead))
                {
                    Destroy(dead.go);
                    chunkViews.Remove(cc);
                }
                continue;
            }

            if (!chunkViews.TryGetValue(cc, out var view))
                view = CreateChunkView(cc);

            VoxelMesher.BuildChunk(world, palette, view.mesh, cc);
        }
        dirtyChunks.Clear();
    }

    ChunkView CreateChunkView(Vector3Int cc)
    {
        var go = new GameObject($"Chunk {cc.x},{cc.y},{cc.z}");
        go.transform.SetParent(worldRoot.transform);
        var mf = go.AddComponent<MeshFilter>();
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = voxelMaterial;
        var m = new Mesh { name = go.name };
        mf.sharedMesh = m;

        var view = new ChunkView { go = go, mesh = m };
        chunkViews.Add(cc, view);
        return view;
    }

    public void ClearWorld()
    {
        world.Clear();
        foreach (var v in chunkViews.Values) Destroy(v.go);
        chunkViews.Clear();
        dirtyChunks.Clear();
        lastEdited = new Vector3Int(int.MinValue, 0, 0);
        lastApplyPos = new Vector2(-1f, -1f);
    }

    int TotalFaces()
    {
        int f = 0;
        foreach (var v in chunkViews.Values) f += v.mesh.vertexCount / 4;
        return f;
    }

    void SaveToFile()
    {
        try
        {
            var voxels = new List<VoxelWorld.Voxel>(world.FilledCount());
            world.GetAllVoxels(voxels);

            using (var fs = new FileStream(SavePath, FileMode.Create, FileAccess.Write))
            using (var w = new BinaryWriter(fs))
            {
                w.Write(FileMagic);
                w.Write(FileVersion);
                w.Write(voxels.Count);
                for (int i = 0; i < voxels.Count; i++)
                {
                    var v = voxels[i];
                    w.Write(v.x);
                    w.Write(v.y);
                    w.Write(v.z);
                    w.Write(v.color);
                }
            }

            SetStatus($"Сохранено вокселей: {voxels.Count}");
        }
        catch (System.Exception e)
        {
            SetStatus("ОШИБКА СОХРАНЕНИЯ (см. Console)");
            Debug.LogError($"VoxelEditor: ошибка сохранения — {e}");
        }
    }

    void LoadFromFile()
    {
        if (!File.Exists(SavePath))
        {
            SetStatus("Файл сохранения не найден");
            return;
        }

        try
        {
            var loaded = new List<VoxelWorld.Voxel>();

            using (var fs = new FileStream(SavePath, FileMode.Open, FileAccess.Read))
            using (var r = new BinaryReader(fs))
            {
                if (r.ReadInt32() != FileMagic)
                    throw new System.Exception("это не файл воксельного мира");
                int version = r.ReadInt32();
                if (version != FileVersion)
                    throw new System.Exception($"неподдерживаемая версия: {version}");

                int count = r.ReadInt32();
                if (count < 0 || count > 50_000_000)
                    throw new System.Exception($"подозрительное количество: {count}");

                for (int i = 0; i < count; i++)
                {
                    loaded.Add(new VoxelWorld.Voxel
                    {
                        x = r.ReadInt32(),
                        y = r.ReadInt32(),
                        z = r.ReadInt32(),
                        color = r.ReadByte()
                    });
                }
            }

            ClearWorld();
            foreach (var v in loaded)
            {
                world.SetColorIndex(v.x, v.y, v.z, v.color);
                world.CollectDirtyChunks(v.x, v.y, v.z, dirtyChunks);
            }
            FlushDirty();

            SetStatus($"Загружено вокселей: {loaded.Count}");
        }
        catch (System.Exception e)
        {
            SetStatus("ОШИБКА ЗАГРУЗКИ (см. Console)");
            Debug.LogError($"VoxelEditor: ошибка загрузки — {e}");
        }
    }

    void ExportVox()
    {
        try
        {
            Directory.CreateDirectory(ExportDir);
            string path = Path.Combine(ExportDir, exportFileName + ".vox");

            int count = VoxelExporter.ExportVOX(world, palette, path);
            if (count == 0) { SetStatus("Мир пуст — нечего экспортировать"); return; }

            SetStatus($"Экспорт .vox: {count} вокселей");
        }
        catch (System.Exception e)
        {
            SetStatus("ОШИБКА ЭКСПОРТА .VOX (см. Console)");
            Debug.LogError($"VoxelEditor: ошибка экспорта .vox — {e}");
        }
    }

    void ExportObj()
    {
        try
        {
            Directory.CreateDirectory(ExportDir);
            string path = Path.Combine(ExportDir, exportFileName + ".obj");

            int faces = VoxelExporter.ExportOBJ(world, palette, path);
            if (faces == 0) { SetStatus("Мир пуст — нечего экспортировать"); return; }

            SetStatus($"Экспорт .obj: {faces} граней (+ mtl, png)");
            Debug.Log("VoxelEditor: нажми Ctrl+R, чтобы Unity увидел файлы в Assets/VoxelExport");
        }
        catch (System.Exception e)
        {
            SetStatus("ОШИБКА ЭКСПОРТА .OBJ (см. Console)");
            Debug.LogError($"VoxelEditor: ошибка экспорта .obj — {e}");
        }
    }

    void SetStatus(string message)
    {
        statusMessage = message;
        statusUntil = Time.realtimeSinceStartup + 6f;
    }

    void HandleKeys()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1)) tool = Tool.Build;
        if (Input.GetKeyDown(KeyCode.Alpha2)) tool = Tool.Erase;
        if (Input.GetKeyDown(KeyCode.Alpha3)) tool = Tool.Paint;

        if (Input.GetKeyDown(KeyCode.F5)) SaveToFile();
        if (Input.GetKeyDown(KeyCode.F9)) LoadFromFile();
        if (Input.GetKeyDown(KeyCode.F2))
        {
            ClearWorld();
            SetStatus("Мир очищен");
        }
        if (Input.GetKeyDown(KeyCode.F6)) ExportVox();
        if (Input.GetKeyDown(KeyCode.F7)) ExportObj();

        float wheel = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(wheel) > 1e-4f && (Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt)))
            colorIndex = (colorIndex + (wheel > 0 ? 1 : -1) + palette.Length) % palette.Length;
    }

    void UpdateHover()
    {
        hasHover = false;
        overUI = paletteRect.Contains(new Vector2(Input.mousePosition.x,
                                                  Screen.height - Input.mousePosition.y));
        if (overUI || Input.GetMouseButton(1)) return;

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);

        if (world.Raycast(ray, out var cell, out var n))
        {
            hasHover = true; hoverIsVoxel = true;
            hoverCell = cell; hoverNormal = n;
        }
        else if (ray.direction.y < -1e-5f)
        {
            float t = -ray.origin.y / ray.direction.y;
            if (t > 0f)
            {
                Vector3 p = ray.GetPoint(t);
                int x = Mathf.FloorToInt(p.x), z = Mathf.FloorToInt(p.z);
                if (!world.IsSolid(x, 0, z))
                {
                    hasHover = true; hoverIsVoxel = false;
                    hoverCell = new Vector3Int(x, 0, z);
                    hoverNormal = Vector3Int.up;
                }
            }
        }
    }

    void HandleEditing()
    {
        if (!Input.GetMouseButton(0))
        {
            lastEdited = new Vector3Int(int.MinValue, 0, 0);
            lastApplyPos = new Vector2(-1f, -1f);
            return;
        }
        if (!hasHover || overUI || Input.GetMouseButton(1)) return;

        Vector2 mousePos = Input.mousePosition;
        if (mousePos == lastApplyPos) return;

        Vector3Int target;
        switch (tool)
        {
            case Tool.Build:
                target = hoverIsVoxel ? hoverCell + hoverNormal : hoverCell;
                if (world.IsSolid(target.x, target.y, target.z)) return;
                break;
            case Tool.Erase:
            case Tool.Paint:
                if (!hoverIsVoxel) return;
                target = hoverCell;
                break;
            default:
                return;
        }

        if (target == lastEdited) return;
        lastEdited = target;
        lastApplyPos = mousePos;

        if (tool == Tool.Erase) Erase(target.x, target.y, target.z);
        else Place(target.x, target.y, target.z, colorIndex);

        FlushDirty();
    }

    void UpdatePreview()
    {
        bool show = hasHover && !overUI && !Input.GetMouseButton(1);
        Vector3Int cell = hoverCell;

        if (show && tool == Tool.Build)
        {
            cell = hoverIsVoxel ? hoverCell + hoverNormal : hoverCell;
            show = !world.IsSolid(cell.x, cell.y, cell.z);
        }

        previewObj.SetActive(show);
        if (!show) return;

        previewObj.transform.position = new Vector3(cell.x + 0.5f, cell.y + 0.5f, cell.z + 0.5f);
        previewObj.transform.localScale = tool == Tool.Build ? Vector3.one : Vector3.one * 1.02f;

        Color c = tool == Tool.Erase ? new Color(1f, 0.3f, 0.25f, 0.5f) : palette[colorIndex];
        if (tool != Tool.Erase) c.a = 0.45f;
        previewMat.color = c;
    }

    void SetupCamera()
    {
        cam = Camera.main;
        bool created = false;
        if (cam == null)
        {
            var go = new GameObject("Voxel Camera");
            go.tag = "MainCamera";
            cam = go.AddComponent<Camera>();
            created = true;
        }
        if (cam.GetComponent<FlyCamera>() == null) cam.gameObject.AddComponent<FlyCamera>();

        if (created)
        {
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.16f, 0.17f, 0.19f);
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 5000f;
            cam.transform.position = new Vector3(20f, 15f, 20f);
            cam.transform.LookAt(new Vector3(0f, 1f, 0f));
        }
    }

    void SetupPreview()
    {
        var tmp = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Mesh cubeMesh = tmp.GetComponent<MeshFilter>().sharedMesh;
        Destroy(tmp);

        previewObj = new GameObject("VoxelPreview");
        var mf = previewObj.AddComponent<MeshFilter>();
        var mr = previewObj.AddComponent<MeshRenderer>();
        mf.sharedMesh = cubeMesh;

        Shader sh = Shader.Find("Unlit/Transparent Color") ?? Shader.Find("Sprites/Default");
        previewMat = new Material(sh) { renderQueue = 3000 };
        mr.sharedMaterial = previewMat;
    }

    void SetupGrid()
    {
        var tex = new Texture2D(64, 64);
        var px = new Color[64 * 64];
        for (int y = 0; y < 64; y++)
        for (int x = 0; x < 64; x++)
            px[y * 64 + x] = (x < 2 || y < 2) ? new Color(1, 1, 1, 0.30f)
                                               : new Color(1, 1, 1, 0.05f);
        tex.SetPixels(px);
        tex.wrapMode = TextureWrapMode.Repeat;
        tex.Apply();

        var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Destroy(quad.GetComponent<Collider>());
        quad.name = "VoxelGrid";
        quad.transform.SetParent(transform);
        quad.transform.localPosition = new Vector3(0f, -0.01f, 0f);
        quad.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
        quad.transform.localScale = new Vector3(GridSize, GridSize, 1f);

        Shader sh = Shader.Find("Unlit/Transparent") ?? Shader.Find("Sprites/Default");
        var mat = new Material(sh) { mainTexture = tex, renderQueue = 2999 };
        mat.mainTextureScale = new Vector2(GridSize, GridSize);
        quad.GetComponent<MeshRenderer>().sharedMaterial = mat;
    }

    void SeedDemo()
    {
        for (int x = -3; x <= 3; x++)
        for (int z = -3; z <= 3; z++)
            Place(x, 0, z, ((x ^ z) & 1) == 0 ? 55 : 51);

        Place(-1, 1, -1, 25);
        Place(-1, 2, -1, 25);
        Place(1, 1, 1, 31);
        Place(0, 1, 0, 16);
        Place(0, 2, 0, 16);
        Place(0, 3, 0, 16);
    }

    Rect PaletteWindowRect()
    {
        int rows = Mathf.CeilToInt(palette.Length / (float)PaletteCols);
        float w = PaletteCols * SwatchSize, h = rows * SwatchSize;
        return new Rect((Screen.width - w) / 2f, Screen.height - h - 16f, w, h);
    }

    void OnGUI()
    {
        paletteRect = PaletteWindowRect();
        DrawPalette();
        DrawHUD();
    }

    void DrawPalette()
    {
        GUI.color = new Color(0.10f, 0.10f, 0.11f, 0.9f);
        GUI.DrawTexture(new Rect(paletteRect.x - 8, paletteRect.y - 8,
                                 paletteRect.width + 16, paletteRect.height + 16),
                        Texture2D.whiteTexture);
        GUI.color = Color.white;

        for (int i = 0; i < palette.Length; i++)
        {
            int col = i % PaletteCols, row = i / PaletteCols;
            var r = new Rect(paletteRect.x + col * SwatchSize,
                             paletteRect.y + row * SwatchSize, SwatchSize, SwatchSize);

            if (i == colorIndex)
            {
                GUI.color = Color.white;
                GUI.DrawTexture(new Rect(r.x - 2, r.y - 2, r.width + 4, r.height + 4),
                                Texture2D.whiteTexture);
            }

            GUI.color = palette[i];
            GUI.DrawTexture(new Rect(r.x + 1, r.y + 1, r.width - 2, r.height - 2),
                            Texture2D.whiteTexture);
            GUI.color = Color.white;

            if (Event.current.type == EventType.MouseDown && Event.current.button == 0
                && r.Contains(Event.current.mousePosition))
            {
                colorIndex = i;
                Event.current.Use();
            }
        }
    }

    void DrawHUD()
    {
        if (hudStyle == null)
        {
            hudStyle = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.UpperLeft,
                fontSize = 13,
                richText = true,
                padding = new RectOffset(10, 10, 8, 8)
            };
        }

        string[] names = { "Построение", "Ластик", "Покраска" };
        string text =
            $"Инструмент: <b>{names[(int)tool]}</b>  [1 | 2 | 3]\n" +
            $"Цвет: {colorIndex} · Вокселей: {world.FilledCount()} · Чанков: {world.ChunkCount} · Граней: {TotalFaces()}\n\n" +
            "Мир бесконечный — стройте в любую сторону\n" +
            "ЛКМ — применить инструмент (можно вести)\n" +
            "ПКМ (зажать) — обзор камеры\n" +
            "WASD — полёт, Space / C — вверх / вниз, Shift — ускорение\n" +
            "Колесо — скорость полёта · Alt + колесо — выбор цвета\n" +
            "<b>F5</b> — сохранить · <b>F9</b> — загрузить · <b>F2</b> — очистить мир\n" +
            "<b>F6</b> — экспорт .vox (MagicaVoxel) · <b>F7</b> — экспорт .obj (Unity/Blender)";

        bool showStatus = statusMessage.Length > 0 && Time.realtimeSinceStartup < statusUntil;
        if (showStatus) text += $"\n\n<b>{statusMessage}</b>";

        float height = showStatus ? 265f : 240f;
        GUI.Box(new Rect(10, 10, 420, height), text, hudStyle);
    }
}