using System;
using UnityEngine;
using UnityEngine.Events;

public class InitComponent : MonoBehaviour
{
    [SerializeField] private UnityEvent onAwake;
    [SerializeField] private UnityEvent onStart;
    [SerializeField] private UnityEvent onEnable;
    [SerializeField] private UnityEvent onDisable;
    [SerializeField] private UnityEvent onDestroy;


    private void Awake()
    {
        onAwake?.Invoke();
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        onStart?.Invoke();
    }

    private void OnEnable()
    {
        onEnable?.Invoke();
    }
    
    private void OnDisable()
    {
        onDisable?.Invoke();
    }
    
    private void OnDestroy()
    {
        onDestroy?.Invoke();
    }
}
