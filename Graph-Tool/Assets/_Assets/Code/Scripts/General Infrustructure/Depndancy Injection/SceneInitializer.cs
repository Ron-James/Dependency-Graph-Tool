using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;

public class SceneInitializer : SerializedMonoBehaviour
{
    [OdinSerialize] private List<IInit> _initializables = new();


    private async void OnEnable()
    {
        int count = 0;
        foreach (var item in _initializables)
        {
            try
            {
                await item.Init();
                count++;

            }
            catch (Exception e)
            {
                Debug.LogError($"Initialization failed for (index is {count.ToString()}) {item.GetType().Name}: {e.Message} at {e.StackTrace}");
            }
            

        }
    }
    
    private void OnDisable()
    {
        foreach (var item in _initializables)
        {
            
            try
            {
                item.Dispose();
            }
            catch (Exception e)
            {
                Debug.LogError($"Disposal failed for {item.GetType().Name}: {e.Message} at {e.StackTrace}");
            }
        }
    }
}