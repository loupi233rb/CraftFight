using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class Hammer : MonoBehaviour
{
    [InspectorName("铁砧")] public AnvilManager anvil;
    public float processVel;
    private Transform point;
    private Vector3 previousPoint;
    private float nextHit;
    private void Start()
    {
        point = transform.Find("point") ?? transform;
        previousPoint = point.position;
        if (anvil == null) anvil = FindObjectOfType<AnvilManager>();
    }
    private void FixedUpdate()
    {
        float distance = Vector3.Distance(previousPoint, point.position);
        processVel = distance < 1.5f ? distance / Mathf.Max(0.001f, Time.fixedDeltaTime) : 0f;
        previousPoint = point.position;
    }
    private void OnTriggerEnter(Collider other)
    {
        var target = other.GetComponentInParent<AnvilManager>();
        if (target == null || Time.time < nextHit || processVel < 0.1f) return;
        anvil = target;
        nextHit = Time.time + 0.15f;
        anvil.Process(processVel);
    }
}
