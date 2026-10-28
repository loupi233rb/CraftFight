using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class AnvilManager : MonoBehaviour
{
    private ItemDatabase itemDatabase;
    private GameObject ingot;
    private GameObject plate;
    private GameObject icon;
    [InspectorName("是否捕获铁锭")] public bool captured = false;
    [InspectorName("加工进度0-1")] public float process = 0f;
    [InspectorName("加工最大速度")] public readonly float maxSpeed = 20f;
    [InspectorName("最少加工次数")] public int minCount = 5;
    [InspectorName("敲打声音")] public AudioClip hitSound;
    [InspectorName("完成声音")] public AudioClip finishSound;
    private AudioSource audioSource;
    void Start()
    {
        audioSource = GetComponent<AudioSource>();
        itemDatabase = Resources.Load<ItemDatabase>("Data/ItemDatabase");
        ingot = itemDatabase.Get("iron_ingot").worldPrefab;
        plate = itemDatabase.Get("iron_plate").worldPrefab;
    }

    // Update is called once per frame
    void Update()
    {
        // for test
        if (Input.GetKeyDown(KeyCode.P))
        {
            Process(20f);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        
    }

    public void SetIcon()
    {
        if (captured)
        {
            if (process >= 1f)
            {
                
            }
        }
    }

    public void Process(float velocity)
    {
        if (velocity <= 0f) return;
        if (process >= 1f) return;
        float amount = velocity<maxSpeed ? velocity/maxSpeed : 1f;
        amount = amount < 1f/minCount ? amount : 1f/minCount;
        process += amount;
        Debug.Log($"Anvil process: {process}");
        
        if (process >= 1f)
        {
            process = 1f;
            audioSource.PlayOneShot(finishSound, 0.8f);
            SetIcon();
            Debug.Log("Anvil process completed");
        }
        else audioSource.PlayOneShot(hitSound, 0.4f);
    }
}
