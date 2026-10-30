using UnityEngine;
using UnityEngine.UI;

/// <summary>A camera-relative world-space HUD, visible in both Game View and the headset.</summary>
[DisallowMultipleComponent]
public sealed class GameFlowHud : MonoBehaviour
{
    [SerializeField] private GameFlowTimer flow;
    [SerializeField] private Camera headCamera;
    [SerializeField] private Font interfaceFont;
    private Text label;
    private GameObject hudRoot;
    private Font runtimeFont;

    private void Start()
    {
        if (flow == null) flow = GetComponent<GameFlowTimer>();
        if (headCamera == null) headCamera = Camera.main;
        if (flow == null || headCamera == null) return;
        if (interfaceFont == null)
        {
            runtimeFont = Font.CreateDynamicFontFromOSFont(new[] {
                "Microsoft YaHei", "Noto Sans CJK SC", "Droid Sans Fallback", "Arial" }, 32);
            interfaceFont = runtimeFont != null ? runtimeFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }
        var root = new GameObject("RoundHUD", typeof(RectTransform), typeof(Canvas));
        hudRoot = root;
        root.transform.SetParent(headCamera.transform, false);
        root.transform.localPosition = new Vector3(0f, 0.45f, 2f);
        root.transform.localRotation = Quaternion.identity;
        root.transform.localScale = Vector3.one * 0.0015f;
        root.GetComponent<RectTransform>().sizeDelta = new Vector2(900f, 145f);
        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = headCamera;
        var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(root.transform, false);
        Stretch(panel.GetComponent<RectTransform>());
        panel.GetComponent<Image>().color = new Color(0.025f, 0.04f, 0.07f, 0.8f);
        panel.GetComponent<Image>().raycastTarget = false;
        var text = new GameObject("Status", typeof(RectTransform), typeof(Text));
        text.transform.SetParent(panel.transform, false);
        Stretch(text.GetComponent<RectTransform>());
        label = text.GetComponent<Text>();
        label.font = interfaceFont;
        label.fontSize = 32;
        label.color = Color.white;
        label.alignment = TextAnchor.MiddleCenter;
        label.raycastTarget = false;
        label.supportRichText = false;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(12f, 8f);
        rect.offsetMax = new Vector2(-12f, -8f);
    }

    private void Update()
    {
        if (label == null || flow == null) return;
        if (flow.CurrentPhase == GameFlowTimer.Phase.Preparation || flow.CurrentPhase == GameFlowTimer.Phase.Combat)
        {
            int seconds = Mathf.CeilToInt(flow.RemainingSeconds);
            string detail = flow.CurrentPhase == GameFlowTimer.Phase.Combat
                ? "存活 " + flow.Monsters.AliveCount + "   已生成 " + flow.Monsters.SpawnedCount + "/" + flow.Monsters.PlannedCount
                : "合成并整理装备，倒计时结束自动进入战斗";
            label.text = "第 " + flow.RoundNumber + " / " + flow.RoundCount + " 关   " + flow.StatusMessage +
                "   " + (seconds / 60).ToString("00") + ":" + (seconds % 60).ToString("00") + "\n" + detail;
        }
        else label.text = flow.StatusMessage;
    }

    private void OnGUI()
    {
        if (flow == null || flow.CurrentPhase != GameFlowTimer.Phase.Waiting) return;
        if (GUI.Button(new Rect(Screen.width * 0.5f - 100f, Screen.height * 0.65f, 200f, 50f), "START GAME"))
            flow.StartGame();
    }

    private void OnDestroy()
    {
        if (hudRoot != null) Destroy(hudRoot);
        if (runtimeFont != null) Destroy(runtimeFont);
    }
}
