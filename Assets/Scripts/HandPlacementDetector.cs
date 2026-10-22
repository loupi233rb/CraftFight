using UnityEngine;

/// <summary>Small palm trigger reserved for workbench slot detection.</summary>
[DefaultExecutionOrder(250)]
[DisallowMultipleComponent]
[RequireComponent(typeof(SphereCollider), typeof(Rigidbody))]
public sealed class HandPlacementDetector : MonoBehaviour
{
    [SerializeField] private HandInventory inventory;

    public HandInventory Inventory => inventory;

    private void Awake()
    {
        if (inventory == null) inventory = GetComponentInParent<HandInventory>();
        GetComponent<SphereCollider>().isTrigger = true;
        Rigidbody body = GetComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
        body.detectCollisions = true;
    }

    private void LateUpdate()
    {
        if (inventory != null) transform.position = inventory.PlacementPosition;
    }

    private void OnDrawGizmos()
    {
        SphereCollider area = GetComponent<SphereCollider>();
        if (area == null) return;
        Matrix4x4 previousMatrix = Gizmos.matrix;
        Color previousColor = Gizmos.color;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.8f);
        Gizmos.DrawWireSphere(area.center, area.radius);
        Gizmos.matrix = previousMatrix;
        Gizmos.color = previousColor;
    }
}
