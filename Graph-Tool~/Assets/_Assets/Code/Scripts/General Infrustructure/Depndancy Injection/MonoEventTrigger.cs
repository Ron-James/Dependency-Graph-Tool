using UnityEngine;
using UnityEngine.Events;

public class MonoEventTrigger : MonoBehaviour
{
    [SerializeField] private UnityEvent onAwake;
    [SerializeField] private UnityEvent onEnable;
    [SerializeField] private UnityEvent onDisable;
    [SerializeField] private UnityEvent onStart;
    
    
    // Awake is called when the script instance is being loaded
    void Awake()
    {
        onAwake?.Invoke();
    }
    
    // OnEnable is called when the object becomes enabled and active
    void OnEnable()
    {
        onEnable?.Invoke();
    }
    
    // OnDisable is called when the behaviour becomes disabled or inactive
    void OnDisable()
    {
        onDisable?.Invoke();
    }
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        onStart?.Invoke();
    }
    
}
