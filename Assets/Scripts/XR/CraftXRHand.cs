using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;

/// <summary>Semantic game actions use the same straight ray shown by the XR controller.</summary>
[DefaultExecutionOrder(300)]
public sealed class CraftXRHand : MonoBehaviour
{
    [SerializeField] private bool leftHand;
    [SerializeField] private HandInventory inventory;
    [SerializeField] private HandPlacementDetector detector;
    [SerializeField] private XRRayInteractor rayInteractor;
    [SerializeField, Min(0.1f)] private float interactionDistance = 5f;
    private InputActionMap actions;
    private InputAction trigger;
    private InputAction cycle;
    private InputAction drop;
    private CraftXRPlayer player;
    private PlayerEconomy economy;
    private GameFlowTimer flow;
    private bool triggerPending;
    private bool cyclePending;
    private bool dropPending;
    private bool firePending;
    private XRGrabInteractable heldHammer;
    private bool previousForceGrab;
    public bool IsHoldingHammer => heldHammer != null;
    private RaycastHit targetHit;
    private XRInteractorLineVisual lineVisual;
    private Gradient originalValid;
    private Gradient originalInvalid;
    private Gradient originalBlocked;
    private Gradient readyGradient;
    private Gradient unavailableGradient;
    private GameObject hitMarker;
    private Material markerMaterial;

    public string Hint { get; private set; } = string.Empty;
    public HandInventory Inventory => inventory;
    public HandPlacementDetector Detector => detector;
    public float InteractionDistance => interactionDistance;
    public bool HasRayTarget => detector != null && detector.selectedObject != null;
    public bool CanUseRayTarget { get; private set; }

    private void Awake()
    {
        player = GetComponentInParent<CraftXRPlayer>();
        economy = GetComponentInParent<PlayerEconomy>();
        flow = FindObjectOfType<GameFlowTimer>();
        string device = leftHand ? "<XRController>{LeftHand}" : "<XRController>{RightHand}";
        actions = new InputActionMap(leftHand ? "Craft Left Hand" : "Craft Right Hand");
        trigger = actions.AddAction("Interact or shoot", InputActionType.Button, device + "/triggerPressed");
        cycle = actions.AddAction("Next inventory slot", InputActionType.Button, device + "/primaryButton");
        drop = actions.AddAction("Drop selected item", InputActionType.Button, device + "/secondaryButton");
        trigger.performed += _ => triggerPending = true;
        cycle.performed += _ => cyclePending = true;
        drop.performed += _ => dropPending = true;
        if (rayInteractor != null)
        {
            rayInteractor.maxRaycastDistance = interactionDistance;
            // The template mask omits the project's item and ground physics layers.
            rayInteractor.raycastMask |= LayerMask.GetMask("Item", "Groud");
            lineVisual = rayInteractor.GetComponent<XRInteractorLineVisual>();
            if (lineVisual != null)
            {
                originalValid = lineVisual.validColorGradient;
                originalInvalid = lineVisual.invalidColorGradient;
                originalBlocked = lineVisual.blockedColorGradient;
                lineVisual.overrideInteractorLineLength = true;
                lineVisual.autoAdjustLineLength = false;
                lineVisual.lineLength = interactionDistance;
            }
        }
        readyGradient = MakeGradient(new Color(.2f, 1f, .4f));
        unavailableGradient = MakeGradient(new Color(1f, .65f, .12f));
        actions.Enable();
    }

    private void OnEnable() { if (actions != null) actions.Enable(); }
    private void OnDisable()
    {
        ReleaseHammer();
        if (actions != null) actions.Disable();
        triggerPending = cyclePending = dropPending = firePending = false;
        if (detector != null) detector.SetSelection(null);
        UpdateRayFeedback(null);
    }
    private void OnDestroy()
    {
        if (actions != null) actions.Dispose();
        if (hitMarker != null) Destroy(hitMarker);
        if (markerMaterial != null) Destroy(markerMaterial);
    }

    private void Update()
    {
        if (player == null || detector == null || inventory == null) return;
        bool triggerPressed = Consume(ref triggerPending);
        bool cyclePressed = Consume(ref cyclePending);
        bool dropPressed = Consume(ref dropPending);
        bool overUI = rayInteractor != null && rayInteractor.IsOverUIGameObject();
        if (heldHammer != null)
        {
            bool selected = rayInteractor != null && heldHammer.interactorsSelecting.Contains(rayInteractor);
            if (!selected || !player.CanPlay) ReleaseHammer();
            else
            {
                inventory.SetInventoryEnabled(false);
                detector.SetSelection(null);
                CanUseRayTarget = false;
                Hint = "持锤中：次按钮放下";
                UpdateRayFeedback(null);
                if (dropPressed) ReleaseHammer();
                return;
            }
        }
        bool grabbed = false;
        var group = GetComponent<XRInteractionGroup>();
        if (group != null && group.activeInteractor is IXRSelectInteractor selector)
        {
            foreach (var selected in selector.interactablesSelected)
                if (selected is XRGrabInteractable) grabbed = true;
        }
        if (player.CanPlay)
        {
            inventory.SetInventoryEnabled(!grabbed);
            if (!grabbed)
            {
                if (cyclePressed) inventory.Cycle(1);
                if (dropPressed) detector.TryDrop();
            }
        }
        GameObject target = player.CanPlay && !grabbed && !overUI && !inventory.IsFirearmSelected
            ? FindTarget() : null;
        detector.SetSelection(target);
        CanUseRayTarget = false;
        if (target != null) DescribeAvailability(target);
        else Hint = inventory.IsFirearmSelected && !overUI ? "射击" : string.Empty;
        UpdateRayFeedback(target);

        if (flow != null && flow.CurrentPhase == GameFlowTimer.Phase.Waiting)
        {
            if (triggerPressed && !overUI) flow.StartGame();
            return;
        }
        if (!player.CanPlay || grabbed) return;
        if (triggerPressed && !overUI)
        {
            if (inventory.IsFirearmSelected) firePending = true;
            else if (target != null)
            {
                var hammer = target.GetComponent<Hammer>();
                if (hammer != null) TryTakeHammer(hammer);
                else detector.TryInteract();
            }
        }
    }


    private bool TryTakeHammer(Hammer hammer)
    {
        if (hammer == null || rayInteractor == null || !player.CanPlay) return false;
        var grab = hammer.GetComponent<XRGrabInteractable>();
        if (grab == null || grab.isSelected) return false;
        previousForceGrab = rayInteractor.useForceGrab;
        rayInteractor.useForceGrab = true;
        rayInteractor.StartManualInteraction((IXRSelectInteractable)grab);
        if (!grab.interactorsSelecting.Contains(rayInteractor))
        {
            if (rayInteractor.isPerformingManualInteraction) rayInteractor.EndManualInteraction();
            rayInteractor.useForceGrab = previousForceGrab;
            return false;
        }
        heldHammer = grab;
        inventory.SetInventoryEnabled(false);
        detector.SetSelection(null);
        var audio = player.GetComponent<PlayerAudio>();
        if (audio != null) audio.PlayInteract();
        return true;
    }

    private void ReleaseHammer()
    {
        if (heldHammer == null) return;
        if (rayInteractor != null)
        {
            if (rayInteractor.isPerformingManualInteraction) rayInteractor.EndManualInteraction();
            rayInteractor.useForceGrab = previousForceGrab;
        }
        heldHammer = null;
        if (inventory != null) inventory.SetInventoryEnabled(player != null && player.CanPlay);
    }

    private void DescribeAvailability(GameObject target)
    {
        Hint = Describe(target, out bool available);
        CanUseRayTarget = available;
    }

    private void LateUpdate()
    {
        // Fire after the tracked Steve palms and held model have their current-frame pose.
        if (!Consume(ref firePending)) return;
        if (player != null && player.CanPlay && inventory != null &&
            (rayInteractor == null || !rayInteractor.IsOverUIGameObject())) inventory.TryFireSelectedGun();
    }

    private static bool Consume(ref bool pending)
    {
        bool pressed = pending; pending = false; return pressed;
    }

    private GameObject FindTarget()
    {
        Vector3 origin = transform.position, direction = transform.forward;
        int mask = Physics.DefaultRaycastLayers;
        if (rayInteractor != null)
        {
            rayInteractor.GetLineOriginAndDirection(out origin, out direction);
            mask = rayInteractor.raycastMask;
        }
        RaycastHit[] hits = Physics.RaycastAll(origin, direction, interactionDistance, mask,
            QueryTriggerInteraction.Collide);
        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (RaycastHit hit in hits)
        {
            if (hit.transform.IsChildOf(player.transform)) continue;
            GameObject candidate = ResolveTarget(hit.collider);
            if (candidate != null) { targetHit = hit; return candidate; }
            // Resource zones do not block the ray, while solid walls and furniture do.
            if (!hit.collider.isTrigger) break;
        }
        return null;
    }

    private static GameObject ResolveTarget(Collider collider)
    {
        var hammer = collider.GetComponentInParent<Hammer>();
        if (hammer != null) return hammer.gameObject;
        var item = collider.GetComponentInParent<DroppedItem>();
        if (item != null) return item.gameObject;
        var slot = collider.GetComponentInParent<CraftSlot>();
        if (slot != null) return slot.gameObject;
        var anvil = collider.GetComponentInParent<AnvilManager>();
        if (anvil != null) return anvil.gameObject;
        var button = collider.GetComponentInParent<global::Button>();
        if (button != null) return button.gameObject;
        var trader = collider.GetComponentInParent<VillagerTrader>();
        return trader != null ? trader.gameObject : null;
    }

    private string Describe(GameObject target, out bool available)
    {
        bool empty = string.IsNullOrEmpty(inventory.GetSelectedItemName());
        available = true;
        var item = target.GetComponent<DroppedItem>();
        if (item != null)
        {
            available = empty;
            return empty ? "拾取 " + ItemName(item.itemName) : "当前槽位已满：先丢弃或换到空格";
        }
        var slot = target.GetComponent<CraftSlot>();
        if (slot != null)
        {
            if (!string.IsNullOrEmpty(slot.capturedItemName))
            {
                available = empty;
                return empty ? "取出 " + ItemName(slot.capturedItemName) : "取出物品需要空槽";
            }
            available = !slot.isOutputSlot && !empty;
            return slot.isOutputSlot ? "输出槽暂无合成物" :
                (empty ? "先选中要放入的材料" : "放入 " + ItemName(inventory.GetSelectedItemName()));
        }
        var anvil = target.GetComponent<AnvilManager>();
        if (anvil != null)
        {
            available = anvil.captured ? empty : inventory.GetSelectedItemName() == "iron_ingot";
            return anvil.captured ? (empty ? "取出铁砧材料" : "取出物品需要空槽") :
                (available ? "放入铁锭" : "请先选中铁锭");
        }
        var button = target.GetComponent<global::Button>();
        if (button != null)
        {
            available = !button.isPressed;
            return available ? "按下合成按钮" : "合成按钮回弹中";
        }
        var hammer = target.GetComponent<Hammer>();
        if (hammer != null)
        {
            var grab = hammer.GetComponent<XRGrabInteractable>();
            available = grab != null && !grab.isSelected;
            return available ? "拿起锤子" : "锤子正被另一只手持有";
        }
        var trader = target.GetComponent<VillagerTrader>();
        available = trader != null && trader.CanInteract(economy);
        return available ? "与村民交易" : "请靠近村民后交易";
    }

    private static string ItemName(string name)
    {
        var database = Resources.Load<ItemDatabase>("Data/ItemDatabase");
        var item = database != null ? database.Get(name) : null;
        return item != null && !string.IsNullOrEmpty(item.displayName) ? item.displayName : name;
    }

    private static Gradient MakeGradient(Color color)
    {
        var gradient = new Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(color, 0f), new GradientColorKey(color, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(.75f, 1f) });
        return gradient;
    }

    private void UpdateRayFeedback(GameObject target)
    {
        bool selected = target != null;
        if (lineVisual != null)
        {
            lineVisual.validColorGradient = selected ? (CanUseRayTarget ? readyGradient : unavailableGradient) : originalValid;
            lineVisual.invalidColorGradient = selected ? (CanUseRayTarget ? readyGradient : unavailableGradient) : originalInvalid;
            lineVisual.blockedColorGradient = selected ? (CanUseRayTarget ? readyGradient : unavailableGradient) : originalBlocked;
            lineVisual.lineLength = selected ? targetHit.distance : interactionDistance;
        }
        if (!selected)
        {
            if (hitMarker != null) hitMarker.SetActive(false);
            return;
        }
        if (hitMarker == null)
        {
            hitMarker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            hitMarker.name = "Interaction Ray Hit " + inventory.GetHandLabel();
            hitMarker.layer = 2;
            var collider = hitMarker.GetComponent<Collider>();
            collider.enabled = false;
            Destroy(collider);
            markerMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            var renderer = hitMarker.GetComponent<Renderer>();
            renderer.sharedMaterial = markerMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }
        markerMaterial.SetColor("_BaseColor", CanUseRayTarget ? new Color(.2f, 1f, .4f) : new Color(1f, .65f, .12f));
        hitMarker.transform.position = targetHit.point + targetHit.normal * .008f;
        hitMarker.transform.localScale = Vector3.one * Mathf.Clamp(targetHit.distance * .008f, .012f, .035f);
        hitMarker.SetActive(true);
    }
}
