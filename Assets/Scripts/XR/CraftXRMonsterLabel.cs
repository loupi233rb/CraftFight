using UnityEngine;

/// <summary>Readable enemy health in world space, including the simulator and the headset.</summary>
public sealed class CraftXRMonsterLabel : MonoBehaviour
{
    private MonsterHealth health;
    private TextMesh label;
    private void Start()
    {
        health = GetComponent<MonsterHealth>();
        var root = new GameObject("XR Enemy Health");
        root.transform.SetParent(transform, false);
        label = root.AddComponent<TextMesh>();
        label.anchor = TextAnchor.MiddleCenter;
        label.fontSize = 48; label.characterSize = 0.035f; label.color = Color.white;
    }
    private void LateUpdate()
    {
        if (label == null || health == null) return;
        label.gameObject.SetActive(!health.IsDead);
        label.text = health.CurrentHealth + " / " + health.MaxHealth + " HP";
        label.transform.position = transform.position + Vector3.up * 2.25f;
        label.transform.localScale = new Vector3(1f / transform.lossyScale.x, 1f / transform.lossyScale.y,
            1f / transform.lossyScale.z);
        if (Camera.main != null) label.transform.rotation = Camera.main.transform.rotation;
    }
}
