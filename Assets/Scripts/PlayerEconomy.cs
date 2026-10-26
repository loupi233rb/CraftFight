using UnityEngine;

/// <summary>Session wallet, top-left HUD, and the player's single trading window.</summary>
[DefaultExecutionOrder(-100)]
[DisallowMultipleComponent]
[RequireComponent(typeof(SteveHealth), typeof(SteveFirstPersonController), typeof(SteveMeleeAttack))]
public sealed class PlayerEconomy : MonoBehaviour
{
    [SerializeField, Min(0)] private int coins;
    [SerializeField] private Font interfaceFont;
    private VillagerTrader currentTrader;
    private VillagerTrader nearbyTrader;
    private CursorLockMode previousCursorLock;
    private bool previousCursorVisible;
    private bool tradeOpen;
    private Font runtimeFont;
    private int closedOnFrame = -1;
    private string feedback = string.Empty;
    private GUIStyle titleStyle;
    private GUIStyle textStyle;
    private GUIStyle buttonStyle;
    private GUIStyle goldStyle;

    public int Coins => coins;
    public SteveHealth Health { get; private set; }
    public SteveFirstPersonController Movement { get; private set; }
    public SteveMeleeAttack Attack { get; private set; }
    public VillagerTrader CurrentTrader => currentTrader;
    public bool BlocksGameplayInput => tradeOpen || closedOnFrame == Time.frameCount;

    private void Awake()
    {
        coins = Mathf.Max(0, coins);
        Health = GetComponent<SteveHealth>();
        Movement = GetComponent<SteveFirstPersonController>();
        Attack = GetComponent<SteveMeleeAttack>();
    }

    public void AddCoins(int amount)
    {
        if (amount > 0)
            coins += amount;
    }

    public bool TrySpendCoins(int amount)
    {
        if (amount <= 0 || coins < amount || Health.IsDead)
            return false;
        coins -= amount;
        return true;
    }

    public bool OpenTrade(VillagerTrader trader)
    {
        if (trader == null || !trader.CanInteract(this))
            return false;
        if (currentTrader != null)
            CloseTrade();
        previousCursorLock = Cursor.lockState;
        previousCursorVisible = Cursor.visible;
        currentTrader = trader;
        tradeOpen = true;
        feedback = "每项可重复购买，升级立即生效";
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        return true;
    }

    public void CloseTrade()
    {
        bool wasOpen = tradeOpen;
        tradeOpen = false;
        currentTrader = null;
        if (!wasOpen)
            return;
        closedOnFrame = Time.frameCount;
        Cursor.lockState = Health.IsDead ? CursorLockMode.None : previousCursorLock;
        Cursor.visible = Health.IsDead || previousCursorVisible;
    }

    private void Update()
    {
        if (tradeOpen)
        {
            if (currentTrader == null || !currentTrader.CanInteract(this) || Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.F))
            {
                CloseTrade();
                return;
            }
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            if (Input.GetKeyDown(KeyCode.Alpha1)) Purchase(VillagerTrader.Upgrade.MoveSpeed);
            else if (Input.GetKeyDown(KeyCode.Alpha2)) Purchase(VillagerTrader.Upgrade.AttackDamage);
            else if (Input.GetKeyDown(KeyCode.Alpha3)) Purchase(VillagerTrader.Upgrade.MaxHealth);
            return;
        }

        nearbyTrader = null;
        if (Health.IsDead)
            return;
        float bestDistance = float.PositiveInfinity;
        foreach (VillagerTrader trader in VillagerTrader.ActiveTraders)
        {
            float distance = (trader.transform.position - transform.position).sqrMagnitude;
            if (distance >= bestDistance || !trader.CanInteract(this))
                continue;
            bestDistance = distance;
            nearbyTrader = trader;
        }
        if (nearbyTrader != null && Input.GetKeyDown(KeyCode.F))
            OpenTrade(nearbyTrader);
    }

    private void Purchase(VillagerTrader.Upgrade upgrade)
    {
        if (currentTrader == null)
            return;
        feedback = currentTrader.TryPurchase(this, upgrade)
            ? "交易成功：" + currentTrader.GetDescription(upgrade)
            : "金币不足或已离开交易范围";
    }

    private void CreateStyles()
    {
        if (textStyle != null)
            return;
        titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 26, fontStyle = FontStyle.Bold };
        textStyle = new GUIStyle(GUI.skin.label) { fontSize = 19, wordWrap = true };
        buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 20 };
        goldStyle = new GUIStyle(titleStyle);
        goldStyle.normal.textColor = new Color(1f, 0.82f, 0.23f);
        if (interfaceFont == null)
            runtimeFont = Font.CreateDynamicFontFromOSFont(new[] {
                "Microsoft YaHei", "Noto Sans CJK SC", "Droid Sans Fallback", "Arial" }, 20);
        Font font = interfaceFont != null ? interfaceFont : runtimeFont;
        if (font != null)
            titleStyle.font = textStyle.font = buttonStyle.font = goldStyle.font = font;
    }

    private void OnGUI()
    {
        CreateStyles();
        Matrix4x4 previousMatrix = GUI.matrix;
        bool previousEnabled = GUI.enabled;
        float scale = Mathf.Max(0.1f, Mathf.Min(Screen.width / 1280f, Screen.height / 720f));
        float width = Screen.width / scale;
        float height = Screen.height / scale;
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));

        DrawPanel(new Rect(18f, 18f, 220f, 58f));
        GUI.Label(new Rect(34f, 28f, 200f, 40f), "金币  " + coins, goldStyle);
        if (currentTrader != null)
        {
            float x = (width - 560f) * 0.5f;
            float y = (height - 450f) * 0.5f;
            DrawPanel(new Rect(x, y, 560f, 450f));
            GUI.Label(new Rect(x + 24f, y + 18f, 480f, 40f), "村民交易", titleStyle);
            GUI.Label(new Rect(x + 24f, y + 64f, 520f, 34f), "金币：" + coins + "    移速：" + Movement.MoveSpeed.ToString("0.##") +
                "    攻击：" + Attack.AttackDamage, textStyle);
            GUI.Label(new Rect(x + 24f, y + 98f, 520f, 32f), "血量：" + Health.CurrentHealth + " / " + Health.MaxHealth, textStyle);
            DrawUpgrade(x, y + 146f, VillagerTrader.Upgrade.MoveSpeed, "1");
            DrawUpgrade(x, y + 208f, VillagerTrader.Upgrade.AttackDamage, "2");
            DrawUpgrade(x, y + 270f, VillagerTrader.Upgrade.MaxHealth, "3");
            GUI.enabled = previousEnabled;
            GUI.Label(new Rect(x + 24f, y + 334f, 516f, 56f), feedback, textStyle);
            if (GUI.Button(new Rect(x + 180f, y + 394f, 200f, 38f), "关闭 [F / Esc]", buttonStyle))
                CloseTrade();
        }
        else if (nearbyTrader != null && !Health.IsDead)
        {
            DrawPanel(new Rect(width * 0.5f - 180f, height - 120f, 360f, 48f));
            GUI.Label(new Rect(width * 0.5f - 160f, height - 111f, 330f, 40f), "靠近村民，按 F 交易", textStyle);
        }
        GUI.enabled = previousEnabled;
        GUI.matrix = previousMatrix;
    }

    private void DrawUpgrade(float x, float y, VillagerTrader.Upgrade upgrade, string hotkey)
    {
        GUI.enabled = coins >= currentTrader.GetCost(upgrade) && !Health.IsDead;
        string label = "[" + hotkey + "] " + currentTrader.GetDescription(upgrade) + "   ·   " + currentTrader.GetCost(upgrade) + " 金币";
        if (GUI.Button(new Rect(x + 24f, y, 512f, 50f), label, buttonStyle))
            Purchase(upgrade);
    }

    private static void DrawPanel(Rect rect)
    {
        Color previousColor = GUI.color;
        GUI.color = new Color(0.055f, 0.065f, 0.075f, 0.94f);
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = previousColor;
        GUI.Box(rect, GUIContent.none);
    }

    private void OnDisable() => CloseTrade();

    private void OnDestroy()
    {
        if (runtimeFont != null)
            Destroy(runtimeFont);
    }
}
