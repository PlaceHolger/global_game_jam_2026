using System.Collections.Generic;
using UnityEngine;

public class RandomActivator : MonoBehaviour
{
    [SerializeField]
    private List<GameObject> objects = new();
    [SerializeField]
    private Vector2 betweenInterval = new(1f, 5f);
    [SerializeField]
    private Vector2 activeInterval = new(0.5f, 2f);

    private float timer = 0f;
    private float currentInterval;
    private float currentActiveDuration;
    private float activeTimer = 0f;
    private GameObject currentActive;

    void Start()
    {
        SetNewInterval();
        DeactivateAll();
    }
    
    void OnDisable()
    {
        DeactivateAll();
    }

    void Update()
    {
        timer += Time.deltaTime;

        if (currentActive)
        {
            activeTimer += Time.deltaTime;
            if (activeTimer >= currentActiveDuration)
            {
                currentActive.SetActive(false);
                currentActive = null;
                activeTimer = 0f;
            }
        }
        else if (timer >= currentInterval && objects.Count > 0)
        {
            ActivateRandomObject();
            timer = 0f;
            SetNewInterval();
        }
    }

    private void SetNewInterval()
    {
        currentInterval = Random.Range(betweenInterval.x, betweenInterval.y);
    }

    private void ActivateRandomObject()
    {
        currentActiveDuration = Random.Range(activeInterval.x, activeInterval.y);
        int randomIndex = Random.Range(0, objects.Count);
        currentActive = objects[randomIndex];
        currentActive.SetActive(true);
    }

    private void DeactivateAll()
    {
        foreach (var obj in objects)
        {
            obj.SetActive(false);
        }
    }
}