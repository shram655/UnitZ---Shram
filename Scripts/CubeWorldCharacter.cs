using UnityEngine;
using Photon.Pun;
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
public class CubeWorldCharacter : MonoBehaviourPun
{
    [Header("═══════ ЦВЕТА ═══════")]
    [Tooltip("Цвет кожи (голова, кисти)")]
    public Color headColor = new Color(1.0f, 0.9f, 0.7f);
    [Tooltip("Цвет волос")]
    public Color hairColor = new Color(0.9f, 0.7f, 0.3f);
    [Tooltip("Цвет рубашки (тело, верх рукавов)")]
    public Color bodyColor = new Color(0.3f, 0.6f, 0.9f);
    [Tooltip("Цвет штанов")]
    public Color legColor = new Color(0.8f, 0.3f, 0.2f);
    [Tooltip("Цвет ботинок")]
    public Color bootColor = new Color(0.4f, 0.3f, 0.2f);

    [Header("═══════ РАЗМЕРЫ ТЕЛА ═══════")]
    [Tooltip("Размер головы (куб)")]
    public float headScale = 0.6f;
    [Tooltip("Размер тела")]
    public float bodyScale = 0.35f;
    [Tooltip("Ширина рук")]
    public float armScale = 0.13f;
    [Tooltip("Ширина ног")]
    public float legScale = 0.15f;
    [Tooltip("Длина рук")]
    public float armLength = 0.30f;
    [Tooltip("Длина ног")]
    public float legLength = 0.40f;

    [Header("═══════ ПОЛОЖЕНИЕ РУК С ОРУЖИЕМ ═══════")]
    [Tooltip("Наклон плеча вперёд: 90 = рука горизонтально, 0 = вниз")]
    [Range(0f, 90f)]
    public float armRaiseForward = 90f;

    [Tooltip("Сгиб локтя: 0 = прямая рука")]
    [Range(0f, 120f)]
    public float armElbowStraight = 0f;

    [Tooltip("Поворот правой руки (сведение к центру)")]
    [Range(-30f, 30f)]
    public float armCenterRoll = -5f;

    [Tooltip("Наклон левой руки вперёд (опущена = 0)")]
    [Range(0f, 90f)]
    public float leftArmRaise = 0f;

    [Header("═══════ 🎯 ПРАВАЯ РУКА: СМЕЩЕНИЕ КАЖДОГО ОБЪЕКТА (только с оружием!) ═══════")]
    [Tooltip("ПЛЕЧО правое: X=лево/право, Y=вверх/вниз, Z=вперёд/назад")]
    public Vector3 offsetShoulderR = Vector3.zero;
    [Tooltip("ПЛЕЧЕВАЯ КОСТЬ правая (относительно плеча)")]
    public Vector3 offsetUpperArmR = Vector3.zero;
    [Tooltip("ПРЕДПЛЕЧЬЕ правое (относительно плечевой кости)")]
    public Vector3 offsetForearmR = Vector3.zero;
    [Tooltip("КИСТЬ правая (относительно предплечья)")]
    public Vector3 offsetHandR = Vector3.zero;

    [Header("═══════  ЛЕВАЯ РУКА: СМЕЩЕНИЕ КАЖДОГО ОБЪЕКТА (только с оружием!) ═══════")]
    [Tooltip("ПЛЕЧО левое: X=лево/право, Y=вверх/вниз, Z=вперёд/назад")]
    public Vector3 offsetShoulderL = Vector3.zero;
    [Tooltip("ПЛЕЧЕВАЯ КОСТЬ левая (относительно плеча)")]
    public Vector3 offsetUpperArmL = Vector3.zero;
    [Tooltip("ПРЕДПЛЕЧЬЕ левое (относительно плечевой кости)")]
    public Vector3 offsetForearmL = Vector3.zero;
    [Tooltip("КИСТЬ левая (относительно предплечья)")]
    public Vector3 offsetHandL = Vector3.zero;

    [Header("═══════ ПРИЦЕЛИВАНИЕ ЗА КАМЕРОЙ ═══════")]
    [Tooltip("ВКЛ = оружие и рука поднимаются/опускаются вместе с обзором")]
    public bool weaponFollowsCamera = true;

    [Tooltip("Плавность следования за камерой (больше = быстрее)")]
    public float aimSmoothSpeed = 12f;

    [Tooltip("Максимальный наклон оружия вверх/вниз (градусы)")]
    [Range(10f, 80f)]
    public float aimPitchLimit = 60f;

    [Header("═══════ ХВАТ (кисть в оружии) ═══════")]
    [Tooltip("Насколько сжимается кисть при хвате (чтобы не мерцало)")]
    [Range(0.5f, 1f)]
    public float gripHandShrink = 0.8f;

    [Header("═══════ ПОЛОЖЕНИЕ АВТОМАТА В КИСТИ ═══════")]
    [Tooltip("Якорь прикреплён К КИСТИ — оружие и рука = одно целое")]
    public Vector3 weaponLocalPosition = new Vector3(0f, 0f, 0.05f);

    [Tooltip("Вращение якоря (наклон/поворот/крен)")]
    public Vector3 weaponLocalRotation = new Vector3(0f, 0f, 0f);

    [Tooltip("Масштаб якоря")]
    public Vector3 weaponLocalScale = Vector3.one;

    [Header("═══════ ПРЕВЬЮ В РЕДАКТОРЕ ═══════")]
    [Tooltip("ВКЛ = руки в редакторе стоят как в игре с оружием")]
    public bool previewWeaponPose = true;

    [Header("═══════ АНИМАЦИЯ ХОДЬБЫ ═══════")]
    [Tooltip("Скорость анимации ходьбы")]
    public float walkSpeed = 8f;
    [Tooltip("Амплитуда движения ног (градусы)")]
    public float walkLegSwing = 45f;
    [Tooltip("Амплитуда движения рук (градусы)")]
    public float walkArmSwing = 35f;
    [Tooltip("Скорость плавного возврата ног в центр")]
    public float returnSpeed = 8f;

    [Header("═══════ ПОДСКАЗКИ В СЦЕНЕ ═══════")]
    [Tooltip("Показывать стрелку направления автомата")]
    public bool showGizmoArrow = true;
    [Tooltip("Цвет стрелки-подсказки")]
    public Color gizmoColor = Color.red;

    /// <summary>Якорь оружия (прикреплён к правой кисти)</summary>
    public Transform WeaponAnchor { get; private set; }

    private bool hasWeaponFlag = false;
    public void SetHasWeapon(bool value)
    {
        hasWeaponFlag = value;
        // 🆕 При убирании оружия — сразу опускаем руки
        if (!value)
        {
            ForceIdlePose();
        }
    }

    bool HasWeapon => hasWeaponFlag || (WeaponAnchor != null && WeaponAnchor.childCount > 0);

    // 🆕 Отслеживание перехода: было оружие → стало без
    private bool wasWeaponLastFrame = false;

    // ── КОСТИ ──
    private Transform root;
    private Transform hips, spine, chest, neck, head;
    private Transform shoulderR, upperArmR, forearmR, handR;
    private Transform shoulderL, upperArmL, forearmL, handL;
    private Transform thighR, shinR, footR;
    private Transform thighL, shinL, footL;

    // ── БАЗОВЫЕ ПОЗИЦИИ КОСТЕЙ ──
    private Vector3 baseShoulderR, baseUpperArmR, baseForearmR, baseHandR;
    private Vector3 baseShoulderL, baseUpperArmL, baseForearmL, baseHandL;
    private bool basePositionsSaved = false;

    // ── ВИЗУАЛ КИСТИ ──
    private Transform handRVis;
    private Vector3 handRBaseScale = Vector3.one;

    // ── КАМЕРА ──
    private Camera cachedCam;
    private float currentPitch = 0f;

    private static Shader cachedShader;

    private Vector3 lastPos;
    private float walkTime = 0f;
    private bool wasMoving = false;
    private bool isReturning = false;
    private float returnProgress = 0f;

    // ═══════════════════════════════════════════════════
    //  ШЕЙДЕР
    // ═════════════════════════════════════════════════════
    static Shader GetCharShader()
    {
        if (cachedShader != null) return cachedShader;
        string[] names = {
            "Unlit/Color",
            "Universal Render Pipeline/Unlit",
            "Custom/VoxelCharacter",
            "HDRP/Unlit",
            "Diffuse",
            "Standard"
        };
        foreach (string n in names)
        {
            Shader s = Shader.Find(n);
            if (s != null) { cachedShader = s; return s; }
        }
        cachedShader = Shader.Find("Sprites/Default");
        return cachedShader;
    }

    static void SetMatColor(Material mat, Color c)
    {
        if (mat == null) return;
        mat.color = c;
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", c);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
    }

#if UNITY_EDITOR
    Material GetOrCreatePartMaterial(string partName, Color defaultColor)
    {
        string dir = "Assets/CharacterMats";
        if (!AssetDatabase.IsValidFolder(dir))
        {
            System.IO.Directory.CreateDirectory(dir);
            AssetDatabase.Refresh();
        }

        string path = dir + "/" + partName + ".mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(GetCharShader());
            m.name = partName;
            SetMatColor(m, defaultColor);
            AssetDatabase.CreateAsset(m, path);
            EditorUtility.SetDirty(m);
        }
        return m;
    }
#endif

    // ═════════════════════════════════════════════════════
    //  РЕДАКТОР
    // ═════════════════════════════════════════════════════
    void OnEnable()
    {
        if (!Application.isPlaying)
        {
            if (transform.Find("CubeWorldRoot") == null) CreateCharacter();
            BindBones();
            ApplyEditorPose();
        }
    }

    void OnValidate()
    {
        if (!Application.isPlaying && transform.Find("CubeWorldRoot") != null)
        {
            BindBones();
            ApplyEditorPose();
        }
    }

    void ApplyEditorPose()
    {
        if (previewWeaponPose) SetArmPoseWeapon(0f);
        else SetArmPoseIdle();
    }

    // Поза с оружием — применяем повороты + смещения
    void SetArmPoseWeapon(float pitch)
    {
        if (upperArmR) upperArmR.localRotation = Quaternion.Euler(-armRaiseForward + pitch, 0, armCenterRoll);
        if (forearmR) forearmR.localRotation = Quaternion.Euler(-armElbowStraight, 0, 0);
        if (upperArmL) upperArmL.localRotation = Quaternion.Euler(-leftArmRaise + pitch * 0.5f, 0, -5f);
        if (forearmL) forearmL.localRotation = Quaternion.Euler(-10f, 0, 0);

        if (basePositionsSaved)
        {
            if (shoulderR) shoulderR.localPosition = baseShoulderR + offsetShoulderR;
            if (upperArmR) upperArmR.localPosition = baseUpperArmR + offsetUpperArmR;
            if (forearmR) forearmR.localPosition = baseForearmR + offsetForearmR;
            if (handR) handR.localPosition = baseHandR + offsetHandR;

            if (shoulderL) shoulderL.localPosition = baseShoulderL + offsetShoulderL;
            if (upperArmL) upperArmL.localPosition = baseUpperArmL + offsetUpperArmL;
            if (forearmL) forearmL.localPosition = baseForearmL + offsetForearmL;
            if (handL) handL.localPosition = baseHandL + offsetHandL;
        }
    }

    // Idle-поза: повороты + сброс позиций к базовым
    void SetArmPoseIdle()
    {
        if (upperArmR) upperArmR.localRotation = Quaternion.Euler(0, 0, 5f);
        if (upperArmL) upperArmL.localRotation = Quaternion.Euler(0, 0, -5f);
        if (forearmR) forearmR.localRotation = Quaternion.Euler(-10f, 0, 0);
        if (forearmL) forearmL.localRotation = Quaternion.Euler(-10f, 0, 0);

        // 🆕 Сброс позиций к базовым (только если сохранены корректно)
        if (basePositionsSaved)
        {
            if (shoulderR) shoulderR.localPosition = baseShoulderR;
            if (upperArmR) upperArmR.localPosition = baseUpperArmR;
            if (forearmR) forearmR.localPosition = baseForearmR;
            if (handR) handR.localPosition = baseHandR;

            if (shoulderL) shoulderL.localPosition = baseShoulderL;
            if (upperArmL) upperArmL.localPosition = baseUpperArmL;
            if (forearmL) forearmL.localPosition = baseForearmL;
            if (handL) handL.localPosition = baseHandL;
        }
    }

    //  Принудительный вызов idle (при убирании оружия)
    void ForceIdlePose()
    {
        SetArmPoseIdle();
        currentPitch = 0f;
        wasWeaponLastFrame = false;
    }

    public void ApplyWeaponAnchorSettings()
    {
        if (WeaponAnchor == null) return;
        WeaponAnchor.localPosition = weaponLocalPosition;
        WeaponAnchor.localRotation = Quaternion.Euler(weaponLocalRotation);
        WeaponAnchor.localScale = weaponLocalScale;
    }

    [ContextMenu("🔄 Сбросить положение оружия (дефолт)")]
    public void ResetWeaponAnchorToDefault()
    {
        weaponLocalPosition = new Vector3(0f, 0f, 0.05f);
        weaponLocalRotation = Vector3.zero;
        weaponLocalScale = Vector3.one;
        ApplyWeaponAnchorSettings();
    }

    [ContextMenu("🔄 Пересобрать персонажа (ВАЖНО! Создаст правильные кости рук)")]
    public void RebuildCharacter()
    {
        if (Application.isPlaying) return;
        Transform old = transform.Find("CubeWorldRoot");
        if (old) DestroyImmediate(old.gameObject);
        basePositionsSaved = false;
        CreateCharacter();
        BindBones();
    }

    [ContextMenu("🎨 Применить цвета из полей (перезапишет ручные!)")]
    public void ApplyColorsMenu()
    {
        ForceColor("Vis_Head", headColor);
        ForceColor("Vis_Hair", hairColor);
        ForceColor("Vis_Body", bodyColor);
        ForceColor("Vis_ArmR_Upper", bodyColor);
        ForceColor("Vis_ArmL_Upper", bodyColor);
        ForceColor("Vis_ArmR_Fore", headColor);
        ForceColor("Vis_ArmL_Fore", headColor);
        ForceColor("Vis_ArmR_Hand", headColor);
        ForceColor("Vis_ArmL_Hand", headColor);
        ForceColor("Vis_LegR_Upper", legColor);
        ForceColor("Vis_LegL_Upper", legColor);
        ForceColor("Vis_LegR_Shin", legColor);
        ForceColor("Vis_LegL_Shin", legColor);
        ForceColor("Vis_LegR_Foot", bootColor);
        ForceColor("Vis_LegL_Foot", bootColor);
#if UNITY_EDITOR
        AssetDatabase.SaveAssets();
#endif
    }

    void ForceColor(string name, Color color)
    {
        Transform t = FindDeep(root, name);
        if (t == null) return;
        Renderer r = t.GetComponent<Renderer>();
        if (r == null) return;
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            Material m = GetOrCreatePartMaterial(name, color);
            SetMatColor(m, color);
            EditorUtility.SetDirty(m);
            r.sharedMaterial = m;
            return;
        }
#endif
        SetMatColor(r.sharedMaterial, color);
    }

    void OnDrawGizmos()
    {
        if (!showGizmoArrow || WeaponAnchor == null) return;
        Gizmos.color = gizmoColor;
        Gizmos.DrawRay(WeaponAnchor.position, WeaponAnchor.forward * 0.5f);
        Gizmos.DrawWireSphere(WeaponAnchor.position, 0.05f);
    }

    void Start()
    {
        if (transform.Find("CubeWorldRoot") == null) CreateCharacter();
        BindBones();

        PlayerController pc = GetComponent<PlayerController>();
        cachedCam = (pc != null && pc.playerCamera != null) ? pc.playerCamera : Camera.main;

        lastPos = transform.position;
        wasMoving = false;
        isReturning = false;
        returnProgress = 0f;
        currentPitch = 0f;
        wasWeaponLastFrame = HasWeapon;
        IdlePose();
    }

    float GetCameraPitch()
    {
        if (cachedCam == null) return 0f;

        Quaternion rel = Quaternion.Inverse(transform.rotation) * cachedCam.transform.rotation;
        Vector3 eul = rel.eulerAngles;

        float pitch = eul.x;
        if (pitch > 180f) pitch -= 360f;

        return Mathf.Clamp(pitch, -aimPitchLimit, aimPitchLimit);
    }

    void BindBones()
    {
        root = transform.Find("CubeWorldRoot");
        if (root == null) return;

        hips = root.Find("Hips");
        if (hips == null) return;
        spine = hips.Find("Spine");
        chest = spine != null ? spine.Find("Chest") : null;
        neck = chest != null ? chest.Find("Neck") : null;
        head = neck != null ? neck.Find("Head") : null;

        shoulderR = chest != null ? chest.Find("ShoulderR") : null;
        upperArmR = shoulderR != null ? shoulderR.Find("UpperArmR") : null;
        forearmR = upperArmR != null ? upperArmR.Find("ForearmR") : null;
        handR = forearmR != null ? forearmR.Find("HandR") : null;

        shoulderL = chest != null ? chest.Find("ShoulderL") : null;
        upperArmL = shoulderL != null ? shoulderL.Find("UpperArmL") : null;
        forearmL = upperArmL != null ? upperArmL.Find("ForearmL") : null;
        handL = forearmL != null ? forearmL.Find("HandL") : null;

        thighR = hips.Find("ThighR");
        shinR = thighR != null ? thighR.Find("ShinR") : null;
        footR = shinR != null ? shinR.Find("FootR") : null;

        thighL = hips.Find("ThighL");
        shinL = thighL != null ? thighL.Find("ShinL") : null;
        footL = shinL != null ? shinL.Find("FootL") : null;

        handRVis = handR != null ? handR.Find("Vis_ArmR_Hand") : null;
        if (handRVis != null) handRBaseScale = handRVis.localScale;

        WeaponAnchor = handR != null ? handR.Find("WeaponAnchor") : null;
        if (WeaponAnchor == null)
        {
            Transform oldAnchor = root.Find("WeaponAnchor");
            if (oldAnchor != null && handR != null)
            {
                Vector3 worldPos = oldAnchor.position;
                Quaternion worldRot = oldAnchor.rotation;
                oldAnchor.SetParent(handR);
                oldAnchor.position = worldPos;
                oldAnchor.rotation = worldRot;
                WeaponAnchor = oldAnchor;

                weaponLocalPosition = oldAnchor.localPosition;
                weaponLocalRotation = oldAnchor.localEulerAngles;
                weaponLocalScale = oldAnchor.localScale;
            }
        }

        // Сохраняем базовые позиции только если кости существуют и позиции не нулевые
        if (!basePositionsSaved && shoulderR != null && shoulderL != null && upperArmR != null && upperArmL != null)
        {
            Vector3 sr = shoulderR.localPosition;
            Vector3 sl = shoulderL.localPosition;

            if (Mathf.Abs(sr.x) > 0.01f || Mathf.Abs(sl.x) > 0.01f)
            {
                baseShoulderR = sr;
                baseUpperArmR = upperArmR.localPosition;
                baseForearmR = forearmR != null ? forearmR.localPosition : Vector3.zero;
                baseHandR = handR != null ? handR.localPosition : Vector3.zero;

                baseShoulderL = sl;
                baseUpperArmL = upperArmL.localPosition;
                baseForearmL = forearmL != null ? forearmL.localPosition : Vector3.zero;
                baseHandL = handL != null ? handL.localPosition : Vector3.zero;

                basePositionsSaved = true;
            }
        }
    }

    Transform FindDeep(Transform parent, string name)
    {
        if (parent == null) return null;
        if (parent.name == name) return parent;
        foreach (Transform child in parent)
        {
            Transform found = FindDeep(child, name);
            if (found != null) return found;
        }
        return null;
    }

    void CreateCharacter()
    {
        root = CreateBone("CubeWorldRoot", transform, Vector3.zero);

        hips = CreateBone("Hips", root, new Vector3(0, legLength, 0));
        spine = CreateBone("Spine", hips, new Vector3(0, bodyScale * 0.4f, 0));
        chest = CreateBone("Chest", spine, new Vector3(0, bodyScale * 0.3f, 0));
        neck = CreateBone("Neck", chest, new Vector3(0, bodyScale * 0.3f, 0));
        head = CreateBone("Head", neck, new Vector3(0, headScale * 0.5f, 0));

        shoulderR = CreateBone("ShoulderR", chest, new Vector3(bodyScale * 0.5f, bodyScale * 0.2f, 0));
        shoulderL = CreateBone("ShoulderL", chest, new Vector3(-bodyScale * 0.5f, bodyScale * 0.2f, 0));

        upperArmR = CreateBone("UpperArmR", shoulderR, Vector3.zero);
        forearmR = CreateBone("ForearmR", upperArmR, new Vector3(0, -armLength * 0.5f, 0));
        handR = CreateBone("HandR", forearmR, new Vector3(0, -armLength * 0.5f, 0));

        upperArmL = CreateBone("UpperArmL", shoulderL, Vector3.zero);
        forearmL = CreateBone("ForearmL", upperArmL, new Vector3(0, -armLength * 0.5f, 0));
        handL = CreateBone("HandL", forearmL, new Vector3(0, -armLength * 0.5f, 0));

        thighR = CreateBone("ThighR", hips, new Vector3(bodyScale * 0.3f, 0, 0));
        shinR = CreateBone("ShinR", thighR, new Vector3(0, -legLength * 0.5f, 0));
        footR = CreateBone("FootR", shinR, new Vector3(0, -legLength * 0.5f, 0));

        thighL = CreateBone("ThighL", hips, new Vector3(-bodyScale * 0.3f, 0, 0));
        shinL = CreateBone("ShinL", thighL, new Vector3(0, -legLength * 0.5f, 0));
        footL = CreateBone("FootL", shinL, new Vector3(0, -legLength * 0.5f, 0));

        CreateCube("Vis_Head", new Vector3(headScale, headScale, headScale), headColor, head, Vector3.zero);
        CreateCube("Vis_Hair", new Vector3(headScale * 1.05f, headScale * 0.25f, headScale * 1.05f), hairColor, head, new Vector3(0, headScale * 0.45f, 0));
        CreateCube("Vis_Body", new Vector3(bodyScale, bodyScale, bodyScale * 0.7f), bodyColor, chest, Vector3.zero);

        CreateCube("Vis_ArmR_Upper", new Vector3(armScale, armLength * 0.5f, armScale), bodyColor, upperArmR, new Vector3(0, -armLength * 0.25f, 0));
        CreateCube("Vis_ArmR_Fore", new Vector3(armScale * 0.9f, armLength * 0.5f, armScale * 0.9f), headColor, forearmR, new Vector3(0, -armLength * 0.25f, 0));
        CreateCube("Vis_ArmR_Hand", new Vector3(armScale * 0.85f, armScale * 0.85f, armScale * 0.85f), headColor, handR, Vector3.zero);

        CreateCube("Vis_ArmL_Upper", new Vector3(armScale, armLength * 0.5f, armScale), bodyColor, upperArmL, new Vector3(0, -armLength * 0.25f, 0));
        CreateCube("Vis_ArmL_Fore", new Vector3(armScale * 0.9f, armLength * 0.5f, armScale * 0.9f), headColor, forearmL, new Vector3(0, -armLength * 0.25f, 0));
        CreateCube("Vis_ArmL_Hand", new Vector3(armScale * 0.85f, armScale * 0.85f, armScale * 0.85f), headColor, handL, Vector3.zero);

        CreateCube("Vis_LegR_Upper", new Vector3(legScale, legLength * 0.5f, legScale), legColor, thighR, new Vector3(0, -legLength * 0.25f, 0));
        CreateCube("Vis_LegR_Shin", new Vector3(legScale * 0.9f, legLength * 0.5f, legScale * 0.9f), legColor, shinR, new Vector3(0, -legLength * 0.25f, 0));
        CreateCube("Vis_LegR_Foot", new Vector3(legScale * 1.2f, legScale * 0.4f, legScale * 1.3f), bootColor, footR, new Vector3(0, -legScale * 0.2f, legScale * 0.15f));

        CreateCube("Vis_LegL_Upper", new Vector3(legScale, legLength * 0.5f, legScale), legColor, thighL, new Vector3(0, -legLength * 0.25f, 0));
        CreateCube("Vis_LegL_Shin", new Vector3(legScale * 0.9f, legLength * 0.5f, legScale * 0.9f), legColor, shinL, new Vector3(0, -legLength * 0.25f, 0));
        CreateCube("Vis_LegL_Foot", new Vector3(legScale * 1.2f, legScale * 0.4f, legScale * 1.3f), bootColor, footL, new Vector3(0, -legScale * 0.2f, legScale * 0.15f));

        GameObject anchor = new GameObject("WeaponAnchor");
        anchor.transform.SetParent(handR);
        anchor.transform.localPosition = weaponLocalPosition;
        anchor.transform.localRotation = Quaternion.Euler(weaponLocalRotation);
        anchor.transform.localScale = weaponLocalScale;
        WeaponAnchor = anchor.transform;

        handRVis = handR.Find("Vis_ArmR_Hand");
        if (handRVis != null) handRBaseScale = handRVis.localScale;

        basePositionsSaved = false;
    }

    Transform CreateBone(string name, Transform parent, Vector3 pos)
    {
        GameObject bone = new GameObject(name);
        bone.transform.SetParent(parent);
        bone.transform.localPosition = pos;
        bone.transform.localRotation = Quaternion.identity;
        return bone.transform;
    }

    Transform CreateCube(string name, Vector3 size, Color color, Transform parent, Vector3 localPos)
    {
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = name;
        cube.transform.SetParent(parent);
        cube.transform.localPosition = localPos;
        cube.transform.localRotation = Quaternion.identity;
        cube.transform.localScale = size;
        Destroy(cube.GetComponent<Collider>());

        Renderer r = cube.GetComponent<Renderer>();
#if UNITY_EDITOR
        if (!Application.isPlaying)
            r.sharedMaterial = GetOrCreatePartMaterial(name, color);
        else
#endif
        {
            Material mat = new Material(GetCharShader());
            mat.name = "VoxelCharMat_" + name;
            SetMatColor(mat, color);
            r.sharedMaterial = mat;
        }
        return cube.transform;
    }

    void Update()
    {
        if (!Application.isPlaying) return;

        float targetPitch = (weaponFollowsCamera && HasWeapon) ? GetCameraPitch() : 0f;
        currentPitch = Mathf.Lerp(currentPitch, targetPitch, Time.deltaTime * aimSmoothSpeed);

        if (handRVis != null)
        {
            Vector3 target = HasWeapon ? handRBaseScale * gripHandShrink : handRBaseScale;
            handRVis.localScale = Vector3.Lerp(handRVis.localScale, target, Time.deltaTime * 10f);
        }

        //  Отслеживание перехода: было оружие → стало без
        bool nowHasWeapon = HasWeapon;
        if (wasWeaponLastFrame && !nowHasWeapon)
        {
            // Оружие только что убрали — сразу опускаем руки
            ForceIdlePose();
        }
        wasWeaponLastFrame = nowHasWeapon;

        float speed = (transform.position - lastPos).magnitude / Mathf.Max(Time.deltaTime, 1e-4f);
        lastPos = transform.position;

        bool isMoving = speed > 0.5f;

        if (isMoving)
        {
            isReturning = false;
            returnProgress = 0f;
            walkTime += Time.deltaTime * walkSpeed;
            AnimateWalk();
            wasMoving = true;
        }
        else
        {
            walkTime = 0f;

            if (wasMoving && !isReturning)
            {
                isReturning = true;
                returnProgress = 0f;
            }

            if (isReturning)
            {
                returnProgress += Time.deltaTime * returnSpeed;
                float t = Mathf.Clamp01(returnProgress);
                t = 1f - Mathf.Pow(1f - t, 3f);
                SmoothReturnLegsToNeutral(t);
                if (t >= 1f)
                {
                    isReturning = false;
                    wasMoving = false;
                }
            }

            if (HasWeapon) WeaponPose();
            else if (!isReturning) IdlePose();
        }
    }

    void SmoothReturnLegsToNeutral(float t)
    {
        Quaternion neutral = Quaternion.identity;
        if (thighR) thighR.localRotation = Quaternion.Slerp(thighR.localRotation, neutral, t);
        if (thighL) thighL.localRotation = Quaternion.Slerp(thighL.localRotation, neutral, t);
        if (shinR) shinR.localRotation = Quaternion.Slerp(shinR.localRotation, neutral, t);
        if (shinL) shinL.localRotation = Quaternion.Slerp(shinL.localRotation, neutral, t);
        if (footR) footR.localRotation = Quaternion.Slerp(footR.localRotation, neutral, t);
        if (footL) footL.localRotation = Quaternion.Slerp(footL.localRotation, neutral, t);
    }

    void IdlePose()
    {
        SetArmPoseIdle();
    }

    void AnimateWalk()
    {
        float swing = Mathf.Sin(walkTime) * walkLegSwing;
        if (thighR) thighR.localRotation = Quaternion.Euler(swing, 0, 0);
        if (thighL) thighL.localRotation = Quaternion.Euler(-swing, 0, 0);
        if (shinR) shinR.localRotation = Quaternion.Euler(Mathf.Max(0, -swing) * 0.5f, 0, 0);
        if (shinL) shinL.localRotation = Quaternion.Euler(Mathf.Max(0, swing) * 0.5f, 0, 0);

        if (HasWeapon) WeaponPose();
        else
        {
            float armSwing = Mathf.Sin(walkTime) * walkArmSwing;
            if (upperArmR) upperArmR.localRotation = Quaternion.Euler(-armSwing, 0, 0);
            if (upperArmL) upperArmL.localRotation = Quaternion.Euler(armSwing, 0, 0);
        }
    }

    void WeaponPose()
    {
        SetArmPoseWeapon(currentPitch);
    }
}