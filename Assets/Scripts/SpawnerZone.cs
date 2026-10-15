using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpawnerZone : MonoBehaviour
{
    private ItemSpawner spawner;

    private void Awake()
    {
        spawner = GetComponentInParent<ItemSpawner>();

        if (spawner == null)
        {
            Debug.LogError($"{name} 找不到父物体上的 ItemSpawner");
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (spawner != null)
        {
            spawner.TriggerEnter(other);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (spawner != null)
        {
            spawner.TriggerExit(other);
        }
    }
}
