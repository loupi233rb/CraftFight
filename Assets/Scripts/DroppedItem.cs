using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class DroppedItem : MonoBehaviour
{
    public string itemName = "oak_plank";
    public int itemId = -1;
    private ItemData itemData;
    private ItemDatabase itemDatabase;
    private Rigidbody rb;
    private float floatedY = 0.025f;
    private float floatSpeed = 0.5f;
    private float rotateSpeed = 30f;
    private GameObject model;
    private GameObject selectionBox;
    void Awake()
    {
        itemDatabase = Resources.Load<ItemDatabase>("Data/ItemDatabase");
        rb = GetComponent<Rigidbody>();
        // 生成时选中框不能显示
        selectionBox = transform.Find("selected").gameObject;
        selectionBox.SetActive(false);
    }
    void Start()
    {
        // 防止掉落物碰撞箱旋转
        // startAngle =Random.Range(0f, 360f);
        // transform.rotation = Quaternion.Euler(0, startAngle, 0);
        // 传入之前必须执行Init，否则默认物品为木板
        itemId = itemData.itemId;
        model = itemData.worldPrefab;
        model = Instantiate(model, transform);
    }

    void Update()
    {
        model.GetComponent<Transform>().position = new Vector3(transform.position.x, transform.position.y + Mathf.Sin(Time.time * floatSpeed) * floatedY, transform.position.z);
        model.GetComponent<Transform>().Rotate(0, rotateSpeed * Time.deltaTime, 0);
    }

    public void Init(Vector3 pos, Vector3 vel, string itemName)
    {
        transform.position = pos;
        rb.velocity = vel;
        this.itemName = itemName;
        itemData = itemDatabase.Get(itemName);
    }

    public void OnSelect()
    {
        selectionBox.SetActive(true);
    }

    public void OffSelect()
    {
        selectionBox.SetActive(false);
    }

}
