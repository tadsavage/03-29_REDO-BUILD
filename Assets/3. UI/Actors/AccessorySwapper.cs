using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AccessorySwapper : MonoBehaviour
{
    [Header("Setup References")]
    [Tooltip("Drag the Left Hand Transform bone from your character's rig hierarchy here.")]
    [SerializeField] private Transform targetHandBone;

    [Tooltip("Drag your low-poly accessory prefabs or scene objects into this list.")]
    [SerializeField] private List<GameObject> accessoryList = new List<GameObject>();

    [Header("Timer Settings")]
    [Tooltip("How many seconds the character holds an accessory before swapping to the next one.")]
    [SerializeField] private float swapIntervalSeconds = 5.0f;

    private int currentAccessoryIndex = 0;
    private float timer = 0.0f;
    private GameObject activeAccessoryInstance;

    private void Start()
    {
        // Safety check to ensure we have items to swap and a hand to attach them to
        if (accessoryList.Count == 0 || targetHandBone == null)
        {
            Debug.LogError($"[{gameObject.name}] Accessory Swapper missing references! Check Inspector.", this);
            enabled = false;
            return;
        }

        // Initialize the first item
        ActivateAccessory(currentAccessoryIndex);
    }

    private void Update()
    {
        timer += Time.deltaTime;

        if (timer >= swapIntervalSeconds)
        {
            timer = 0.0f; // Reset timer
            CycleToNextAccessory();
        }
    }

    private void CycleToNextAccessory()
    {
        // Calculate next index looping back to 0 cleanly using modulo
        currentAccessoryIndex = (currentAccessoryIndex + 1) % accessoryList.Count;
        ActivateAccessory(currentAccessoryIndex);
    }

    private void ActivateAccessory(int index)
    {
        // 1. Clean up the old active accessory if it exists
        if (activeAccessoryInstance != null)
        {
            Destroy(activeAccessoryInstance);
        }

        // 2. Instantiate the new accessory prefab
        GameObject prefabToSpawn = accessoryList[index];
        if (prefabToSpawn == null) return;

        // Spawn it directly at the hand position and orientation
        activeAccessoryInstance = Instantiate(prefabToSpawn, targetHandBone.position, targetHandBone.rotation);

        // 3. Parent it to the hand so it moves flawlessly with your walking animations
        activeAccessoryInstance.transform.SetParent(targetHandBone);

        // 4. Zero out local transforms so it aligns perfectly with the bone's pivot point
        activeAccessoryInstance.transform.localPosition = Vector3.zero;
        activeAccessoryInstance.transform.localRotation = Quaternion.identity;
    }
}
