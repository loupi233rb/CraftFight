using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class AnvilManager : MonoBehaviour
{
    private ItemDatabase itemDatabase;
    private GameObject ingot_icon;
    private GameObject plate_icon;
    [InspectorName("是否捕获铁锭")] public bool captured = false;
    [InspectorName("加工进度0-1")] public float process = 0f;
    [InspectorName("加工最大速度")] public float maxSpeed = 200f;
    [InspectorName("最少加工次数")] public int minCount = 5;
    [InspectorName("敲打声音")] public AudioClip hitSound;
    [InspectorName("完成声音")] public AudioClip finishSound;
    private AudioSource audioSource;
    void Start()
    {
        audioSource = GetComponent<AudioSource>();
        itemDatabase = Resources.Load<ItemDatabase>("Data/ItemDatabase");
        ingot_icon = Instantiate(itemDatabase.Get("iron_ingot").worldPrefab, transform.parent);
        plate_icon = Instantiate(itemDatabase.Get("iron_plate").worldPrefab, transform.parent);
        Vector3 pos = new Vector3(0, 0.1f, 0);
        Quaternion rot = Quaternion.Euler(new Vector3(-90, 90, 0));
        Vector3 sca = new Vector3(2, 2, 2);
        ingot_icon.transform.SetLocalPositionAndRotation(pos, rot);
        ingot_icon.transform.localScale = sca;
        plate_icon.transform.SetLocalPositionAndRotation(pos, rot);
        plate_icon.transform.localScale = sca;
        ingot_icon.SetActive(false);
        plate_icon.SetActive(false);
    }

    // Update is called once per frame
    void Update()
    {
        // for test
        if (CraftXRPlayer.Active != null) return;
        if (Input.GetKeyDown(KeyCode.P))
        {
            Process(20f);
        }
        if (Input.GetKeyDown(KeyCode.O))
        {
            SlotUpdate(true);
        }
    }

    public void SetIcon()
    {
        ingot_icon.SetActive(captured && process < 1f);
        plate_icon.SetActive(captured && process >= 1f);
    }

    public void SlotUpdate(bool captured)
    {
        this.captured = captured;
        process = 0f;
        SetIcon();
    }

    public void Process(float velocity)
    {
        if (!captured) return;
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
