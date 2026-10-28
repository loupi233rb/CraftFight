using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class Button : MonoBehaviour
{
    public CraftTable craftTable;
    [InspectorName("触发行程")] public float triggerY = 0.1f;
    [InspectorName("回弹时间")] public float restoreTime = 2.0f;
    [InspectorName("是否按下")] public bool isPressed = false;
    private float pressedTime = 0.0f;
    private AudioSource audioSource;

    void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        if (craftTable == null)
        {
            craftTable = GetComponentInParent<CraftTable>();
        }
    }
    void Start()
    {
        
    }

    void Update()
    {
        if (isPressed && Time.time - pressedTime >= restoreTime)
        {
            isPressed = false;
            transform.position += new Vector3(0, triggerY, 0);
            Debug.Log("按钮回弹");
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && !isPressed)
        {
            isPressed = true;
            if (craftTable != null)
            {
                craftTable.TryCraft();
            }
            pressedTime = Time.time;
            transform.position -= new Vector3(0, triggerY, 0);
            audioSource.Play();
            Debug.Log("玩家按了按钮");
        }
    }
}
