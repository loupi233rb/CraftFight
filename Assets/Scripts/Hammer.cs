using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class Hammer : MonoBehaviour
{
    private Rigidbody rb;
    private Vector3 processPointPos;
    [InspectorName("铁砧")] public AnvilManager anvil;
    public float processVel = 0f;
    void Start()
    {
        if (anvil == null)
        {
            anvil = GameObject.Find("Anvil").GetComponent<AnvilManager>();
            if (anvil == null)
            {
                Debug.LogError("AnvilManager not found in the scene.");
            }
        }
        rb = GetComponent<Rigidbody>();
        processPointPos = transform.Find("point").transform.position;
    }

    void Update()
    {
        // 获取末端处理点速度
        processVel = rb.GetPointVelocity(transform.TransformPoint(processPointPos)).magnitude;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Anvil")) return;
        anvil.Process(processVel);
    }

}
