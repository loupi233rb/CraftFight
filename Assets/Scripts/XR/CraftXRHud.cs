using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

/// <summary>World-space status, start and trade panels, usable with either simulated or physical rays.</summary>
public sealed class CraftXRHud : MonoBehaviour
{
    private CraftXRPlayer player;
    private PlayerEconomy economy;
    private SteveHealth health;
    private GameFlowTimer flow;
    private Font font;
    private Text status;
    private Text hint;
    private Text tradeInfo;
    private Text feedback;
    private GameObject startPanel;
    private GameObject tradePanel;
    private readonly UnityEngine.UI.Button[] upgrades = new UnityEngine.UI.Button[3];
    private readonly Text[] upgradeLabels = new Text[3];
    private bool tradeWasOpen;

    private void Start()
    {
        player = GetComponent<CraftXRPlayer>();
        economy = GetComponent<PlayerEconomy>();
        health = GetComponent<SteveHealth>();
        flow = FindObjectOfType<GameFlowTimer>();
        font = Font.CreateDynamicFontFromOSFont(new[] {
            "Microsoft YaHei", "Noto Sans CJK SC", "Droid Sans Fallback", "Arial" }, 32);
        if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        Transform head = player.HeadCamera.transform;
        var hud = Panel("XR Status", head, new Vector3(0f, 0.48f, 2f), new Vector2(1000f, 160f), false);
        status = Label("Status", hud.transform, new Vector2(0, 30), new Vector2(960, 90), 28);
        hint = Label("Hint", hud.transform, new Vector2(0, -42), new Vector2(960, 45), 23);
        startPanel = Panel("XR Start", head, new Vector3(0f, -0.05f, 2.2f), new Vector2(650, 260), true);
        Label("Title", startPanel.transform, new Vector2(0, 65), new Vector2(610, 65), 34).text = "CraftFight VR";
        MakeButton("Start", startPanel.transform, new Vector2(0, -25), new Vector2(450, 75), "开始游戏",
            () => { if (flow != null) flow.StartGame(); });
        tradePanel = Panel("XR Trade", null, Vector3.zero, new Vector2(820, 610), true);
        Label("Title", tradePanel.transform, new Vector2(0, 250), new Vector2(760, 65), 34).text = "村民交易";
        tradeInfo = Label("Player Stats", tradePanel.transform, new Vector2(0, 170), new Vector2(760, 90), 27);
        for (int i = 0; i < 3; i++)
        {
            int index = i;
            upgrades[i] = MakeButton("Upgrade " + i, tradePanel.transform, new Vector2(0, 60 - 85 * i),
                new Vector2(740, 68), "", () => economy.Purchase((VillagerTrader.Upgrade)index));
            upgradeLabels[i] = upgrades[i].GetComponentInChildren<Text>();
        }
        feedback = Label("Feedback", tradePanel.transform, new Vector2(0, -175), new Vector2(760, 70), 23);
        MakeButton("Close", tradePanel.transform, new Vector2(0, -250), new Vector2(330, 60),
            "关闭交易", () => economy.CloseTrade());
        tradePanel.SetActive(false);
    }

    private void Update()
    {
        if (status == null) return;
        string phase = flow != null ? flow.StatusMessage : "";
        if (flow != null && (flow.CurrentPhase == GameFlowTimer.Phase.Preparation ||
            flow.CurrentPhase == GameFlowTimer.Phase.Combat || flow.CurrentPhase == GameFlowTimer.Phase.Loot))
        {
            int seconds = Mathf.CeilToInt(flow.RemainingSeconds);
            phase = "第 " + flow.RoundNumber + " / " + flow.RoundCount + " 关  " + phase + "  " +
                (seconds / 60).ToString("00") + ":" + (seconds % 60).ToString("00");
            if (flow.CurrentPhase == GameFlowTimer.Phase.Combat)
                phase += "  存活 " + flow.Monsters.AliveCount;
        }
        status.text = phase + "\n" + (health.IsDead ? "你已死亡" : "生命 " + health.CurrentHealth + " / " + health.MaxHealth) +
            "    金币 " + economy.Coins;
        string context = "";
        foreach (var hand in player.GetComponentsInChildren<CraftXRHand>())
            if (!string.IsNullOrEmpty(hand.Hint)) context += hand.Hint + "  ";
        hint.text = string.IsNullOrEmpty(context)
            ? "扳机：交互 / 开枪    剑或空手：接触攻击    主按钮：换格    次按钮：丢弃"
            : "扳机：" + context;
        startPanel.SetActive(flow != null && flow.CurrentPhase == GameFlowTimer.Phase.Waiting);
        bool open = economy.CurrentTrader != null && !health.IsDead;
        if (open && !tradeWasOpen)
        {
            Transform head = player.HeadCamera.transform;
            Vector3 forward = Vector3.ProjectOnPlane(head.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < 0.1f) forward = transform.forward;
            float distance = Mathf.Clamp(Vector3.Distance(head.position,
                economy.CurrentTrader.transform.position + Vector3.up * 1.2f) - 0.65f, 0.65f, 1.15f);
            tradePanel.transform.localScale = Vector3.one * (0.0015f * distance / 1.15f);
            tradePanel.transform.SetPositionAndRotation(head.position + forward * distance,
                Quaternion.LookRotation(forward, Vector3.up));
        }
        status.transform.parent.gameObject.SetActive(!open);
        tradePanel.SetActive(open);
        tradeWasOpen = open;
        if (!open) return;
        tradeInfo.text = "金币 " + economy.Coins + "   移速 " + economy.Movement.MoveSpeed.ToString("0.##") +
            "   攻击 " + economy.Attack.AttackDamage + "\n生命 " + health.CurrentHealth + " / " + health.MaxHealth;
        for (int i = 0; i < 3; i++)
        {
            var upgrade = (VillagerTrader.Upgrade)i;
            int cost = economy.CurrentTrader.GetCost(upgrade);
            upgradeLabels[i].text = economy.CurrentTrader.GetDescription(upgrade) + "    " + cost + " 金币";
            upgrades[i].interactable = economy.Coins >= cost;
        }
        feedback.text = economy.Feedback;
    }

    private GameObject Panel(string name, Transform parent, Vector3 position, Vector2 size, bool interactive)
    {
        var root = new GameObject(name, typeof(RectTransform), typeof(Canvas));
        root.transform.SetParent(parent, false);
        root.transform.localPosition = position;
        root.transform.localScale = Vector3.one * 0.0015f;
        root.GetComponent<RectTransform>().sizeDelta = size;
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = player.HeadCamera;
        if (interactive) root.AddComponent<TrackedDeviceGraphicRaycaster>();
        var background = new GameObject("Background", typeof(RectTransform), typeof(Image));
        background.transform.SetParent(root.transform, false);
        RectTransform rect = background.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        background.GetComponent<Image>().color = new Color(0.025f, 0.045f, 0.065f, 0.94f);
        background.GetComponent<Image>().raycastTarget = interactive;
        return root;
    }

    private Text Label(string name, Transform parent, Vector2 position, Vector2 size, int fontSize)
    {
        var root = new GameObject(name, typeof(RectTransform), typeof(Text));
        root.transform.SetParent(parent, false);
        var rect = root.GetComponent<RectTransform>();
        rect.sizeDelta = size; rect.anchoredPosition = position;
        var text = root.GetComponent<Text>();
        text.font = font; text.fontSize = fontSize;
        text.color = Color.white; text.alignment = TextAnchor.MiddleCenter;
        text.raycastTarget = false;
        return text;
    }

    private UnityEngine.UI.Button MakeButton(string name, Transform parent, Vector2 position, Vector2 size,
        string title, UnityEngine.Events.UnityAction action)
    {
        var root = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(UnityEngine.UI.Button));
        root.transform.SetParent(parent, false);
        var rect = root.GetComponent<RectTransform>();
        rect.sizeDelta = size; rect.anchoredPosition = position;
        var image = root.GetComponent<Image>();
        image.color = new Color(0.12f, 0.34f, 0.42f, 1f);
        var button = root.GetComponent<UnityEngine.UI.Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(action);
        Label("Label", root.transform, Vector2.zero, size - new Vector2(20, 4), 27).text = title;
        return button;
    }

    private void OnDestroy()
    {
        if (tradePanel != null) Destroy(tradePanel);
        if (font != null) Destroy(font);
    }
}
