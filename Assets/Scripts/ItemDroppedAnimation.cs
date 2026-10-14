using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ItemDroppedAnimation : MonoBehaviour
{
    // Start is called before the first frame update
    private float floatedY = 0.02f;
    private float floatSpeed = 0.5f;
    private float rotateSpeed = 30f;
    private float startAngle = 0f;
    private GameObject model;
    void Start()
    {
        startAngle = Random.Range(0f, 360f);
        transform.rotation = Quaternion.Euler(0, startAngle, 0);
        model = transform.GetChild(0).gameObject;
    }

    // Update is called once per frame
    void Update()
    {
        model.GetComponent<Transform>().position = new Vector3(transform.position.x, transform.position.y + Mathf.Sin(Time.time * floatSpeed) * floatedY, transform.position.z);
        model.GetComponent<Transform>().Rotate(0, rotateSpeed * Time.deltaTime, 0);
    }
}
