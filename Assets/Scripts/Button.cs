using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class Button : MonoBehaviour
{
    public CraftTable craftTable;
    [InspectorName("触发行程")] public float triggerY = 0.1f;
    [InspectorName("回弹时间")] public float restoreTime = 2f;
    [InspectorName("是否按下")] public bool isPressed;
    private float pressedTime;
    private AudioSource audioSource;
    private Vector3 restPosition;
    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        if (craftTable == null) craftTable = GetComponentInParent<CraftTable>();
        restPosition = transform.localPosition;
    }
    private void Update()
    {
        if (isPressed && Time.time - pressedTime >= restoreTime)
        {
            isPressed = false;
            transform.localPosition = restPosition;
        }
    }
    public bool Press()
    {
        if (isPressed || craftTable == null) return false;
        if (CraftXRPlayer.Active != null && !CraftXRPlayer.Active.CanPlay) return false;
        isPressed = true;
        pressedTime = Time.time;
        transform.localPosition = restPosition - Vector3.up * triggerY;
        audioSource.Play();
        return craftTable.TryCraft();
    }
    private void OnTriggerEnter(Collider other)
    {
        if (other.GetComponentInParent<HandPlacementDetector>() != null ||
            (CraftXRPlayer.Active == null && other.CompareTag("Player"))) Press();
    }
}
