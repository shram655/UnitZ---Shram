using UnityEngine;
using Photon.Pun;
using System.Collections;

public class MeleeWeapon : MonoBehaviourPun
{
    [Header("Настройки")]
    public string meleeName = "Топор";
    public int meleeId = 1;
    public int slotIndex = -1;
    public float damage = 35f;
    [Tooltip("Дальность удара")]
    public float range = 2.5f;
    [Tooltip("Пауза между ударами")]
    public float attackRate = 0.6f;

    [Header("Анимация удара")]
    [Tooltip("Угол маха ВПЕРЁД-ВНИЗ (больше = сильнее замах)")]
    [Range(30f, 90f)]
    public float swingAngle = 60f;
    public float swingDuration = 0.3f;

    [Header("Ссылки")]
    public Camera fpsCam;
    public PlayerInventory playerInventory;

    [Header("🎯 Viewmodel")]
    public bool attachToCamera = true;
    public Vector3 viewmodelPosition = new Vector3(0.35f, -0.3f, 0.5f);
    public Vector3 viewmodelRotation = new Vector3(0f, 0f, 0f);
    public bool useDrawAnimation = true;
    public Vector3 drawStartPosition = new Vector3(0.35f, -0.7f, 0.3f);
    public Vector3 drawStartRotation = new Vector3(40f, 0f, 0f);
    [Range(2f, 20f)]
    public float drawSpeed = 10f;

    [Header("🏃 При беге")]
    [Tooltip("ВКЛ = топор чуть опускается при беге (НО НЕ поворачивается!)")]
    public bool useSprintCarry = true;
    public Vector3 sprintCarryPosition = new Vector3(0.3f, -0.35f, 0.45f);
    [Range(2f, 20f)]
    public float sprintCarrySmooth = 8f;

    private float nextAttackTime = 0f;
    private float swingT = -1f;
    private bool isEquipped = false;

    private Vector3 currentPosition;
    private Quaternion currentRotation = Quaternion.identity;
    private bool viewmodelInitialized = false;
    private bool isDrawing = false;

    private WorldManager worldManager;
    private CubeWorldCharacter ownerCharacter;

    bool IsLocal() => photonView == null || photonView.IsMine;

    void Awake()
    {
        if (IsLocal()) gameObject.SetActive(false);
    }

    void Start()
    {
        isEquipped = false;
        worldManager = FindObjectOfType<WorldManager>();

        if (IsLocal())
        {
            ownerCharacter = GetComponentInParent<CubeWorldCharacter>();
            StartCoroutine(SelfEquipFallback());
        }
        else
        {
            StartCoroutine(AttachToOwner());
        }
    }

    IEnumerator SelfEquipFallback()
    {
        float waited = 0f;
        while (!isEquipped && waited < 2f)
        {
            if (fpsCam != null)
            {
                yield return null;
                if (!isEquipped) Equip();
                yield break;
            }
            waited += Time.deltaTime;
            yield return null;
        }
    }

    IEnumerator AttachToOwner()
    {
        for (int i = 0; i < 60; i++)
        {
            if (this == null) yield break;
            PlayerController[] pcs = FindObjectsOfType<PlayerController>();
            foreach (var pc in pcs)
            {
                if (pc.view != null && photonView != null && pc.view.OwnerActorNr == photonView.OwnerActorNr)
                {
                    CubeWorldCharacter cw = pc.GetComponent<CubeWorldCharacter>();
                    if (cw != null && cw.WeaponAnchor != null)
                    {
                        ownerCharacter = cw;
                        transform.SetParent(cw.WeaponAnchor);
                        transform.localPosition = Vector3.zero;
                        transform.localRotation = Quaternion.identity;
                    }
                    yield break;
                }
            }
            yield return new WaitForSeconds(0.5f);
        }
    }

    void OnDestroy() { ownerCharacter = null; }

    bool IsSelf(Transform t)
    {
        if (ownerCharacter == null) return false;
        return t == ownerCharacter.transform || t.IsChildOf(ownerCharacter.transform);
    }

    void Update()
    {
        if (photonView != null && !photonView.IsMine) return;
        if (!isEquipped || fpsCam == null) return;
        if (playerInventory != null && playerInventory.IsInventoryOpen) return;

        if (Input.GetMouseButtonDown(0) && Time.time >= nextAttackTime)
        {
            nextAttackTime = Time.time + attackRate;
            Swing();
        }
    }

    // ═════════════════════════════════════════════════════
    // УДАР: бьёт игроков И ломает блоки
    // ═════════════════════════════════════════════════════
    void Swing()
    {
        swingT = 0f;

        Vector3 origin = fpsCam.transform.position;
        Vector3 dir = fpsCam.transform.forward;

        RaycastHit[] hits = Physics.RaycastAll(origin, dir, range);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (var hit in hits)
        {
            if (IsSelf(hit.transform)) continue;

            // 1) Игрок
            PlayerHealth target = hit.transform.GetComponent<PlayerHealth>();
            if (target == null) target = hit.transform.GetComponentInParent<PlayerHealth>();
            if (target != null)
            {
                if (!(photonView != null && photonView.IsMine && target.photonView != null && target.photonView.IsMine))
                {
                    target.photonView.RPC("RPC_TakeDamage", target.photonView.Owner, damage);
                }
                break;
            }

            // 2) Блок (обычный или с лутом)
            GameObject blockRoot = hit.transform.gameObject;
            while (blockRoot.transform.parent != null && blockRoot.transform.parent.name != "WorldBlocks")
            {
                blockRoot = blockRoot.transform.parent.gameObject;
            }

            BlockDestroyer bd = blockRoot.GetComponent<BlockDestroyer>();
            if (bd != null)
            {
                bd.RequestDestroy(playerInventory);
                break;
            }

            PlacedBlock pb = blockRoot.GetComponent<PlacedBlock>();
            if (pb != null && playerInventory != null)
            {
                playerInventory.AddToInventory(pb.blockId);
                if (worldManager != null)
                    worldManager.DestroyBlock(blockRoot.transform.position, PhotonNetwork.NickName);
                if (PhotonNetwork.IsConnected)
                {
                    photonView.RPC("RPC_MeleeBlockDestroyed", RpcTarget.All,
                        Mathf.RoundToInt(blockRoot.transform.position.x * 100),
                        Mathf.RoundToInt(blockRoot.transform.position.y * 100),
                        Mathf.RoundToInt(blockRoot.transform.position.z * 100));
                }
                Destroy(blockRoot);
                break;
            }
        }
    }

    [PunRPC]
    void RPC_MeleeBlockDestroyed(int x, int y, int z)
    {
        Vector3 pos = new Vector3(x / 100f, y / 100f, z / 100f);
        if (worldManager != null) worldManager.HandleBlockDestroyed(pos);
    }

    void LateUpdate()
    {
        if (photonView != null && !photonView.IsMine) return;
        if (!isEquipped || fpsCam == null) return;
        if (!attachToCamera) return;

        bool sprinting = useSprintCarry && IsSprinting();

        if (!viewmodelInitialized)
        {
            currentPosition = viewmodelPosition;
            currentRotation = Quaternion.Euler(viewmodelRotation);
            viewmodelInitialized = true;
        }

        // 🆕 При беге меняется ТОЛЬКО позиция (чуть ниже), поворот ВСЕГДА как в viewmodelRotation
        Vector3 targetPos = sprinting ? sprintCarryPosition : viewmodelPosition;
        Quaternion targetRot = Quaternion.Euler(viewmodelRotation); // 🆕 НИКАКОГО поворота при беге!

        float speed = (isDrawing) ? drawSpeed : sprintCarrySmooth;
        float t = Time.deltaTime * speed;
        currentPosition = Vector3.Lerp(currentPosition, targetPos, t);
        currentRotation = Quaternion.Slerp(currentRotation, targetRot, t);

        if (isDrawing && Vector3.Distance(currentPosition, targetPos) < 0.01f) isDrawing = false;

        // 🪓 Анимация удара: мах ВПЕРЁД-ВНИЗ
        Vector3 swingOffset = Vector3.zero;
        Quaternion swingRot = Quaternion.identity;
        if (swingT >= 0f)
        {
            swingT += Time.deltaTime;
            float p = swingT / swingDuration;
            if (p >= 1f)
            {
                swingT = -1f;
            }
            else
            {
                float curve = Mathf.Sin(p * Mathf.PI); // 0 → 1 → 0
                swingRot = Quaternion.Euler(curve * swingAngle, 0f, -curve * swingAngle * 0.15f);
                swingOffset = new Vector3(0f, -curve * 0.08f, curve * 0.15f);
            }
        }

        transform.localPosition = currentPosition + swingOffset;
        transform.localRotation = currentRotation * swingRot;
    }

    bool IsSprinting()
    {
        if (playerInventory != null && playerInventory.IsInventoryOpen) return false;
        float move = Mathf.Abs(Input.GetAxis("Horizontal")) + Mathf.Abs(Input.GetAxis("Vertical"));
        return Input.GetKey(KeyCode.LeftShift) && move > 0.1f;
    }

    public void Equip()
    {
        if (photonView != null && !photonView.IsMine) return;

        bool firstEquip = !isEquipped;
        isEquipped = true;
        gameObject.SetActive(true);

        if (ownerCharacter != null) ownerCharacter.SetHasWeapon(true);

        if (firstEquip)
        {
            if (useDrawAnimation)
            {
                currentPosition = drawStartPosition;
                currentRotation = Quaternion.Euler(viewmodelRotation + drawStartRotation);
                isDrawing = true;
            }
            else
            {
                currentPosition = viewmodelPosition;
                currentRotation = Quaternion.Euler(viewmodelRotation);
                isDrawing = false;
            }
            viewmodelInitialized = true;
        }

        if (attachToCamera && fpsCam != null && transform.parent != fpsCam.transform)
        {
            transform.SetParent(fpsCam.transform);
            transform.localPosition = currentPosition;
            transform.localRotation = currentRotation;
            // НЕ трогаем scale — размер из префаба
        }
    }

    public void Unequip()
    {
        if (photonView != null && !photonView.IsMine) return;
        isEquipped = false;
        swingT = -1f;
        gameObject.SetActive(false);
    }
}