using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Sirenix.OdinInspector;
using UnityEngine;

public class PrefabInstanceInitializer : IInit
{
    [SerializeField] private GameObject _prefab;
    [SerializeField] private Transform _parent;
    [ShowInInspector, ReadOnly] public GameObject Instance { get; private set; }
    [ShowInInspector, ReadOnly] private IInit[] _initializables { get; set; }

    public void Dispose()
    {
        if (_initializables != null)
        {
            foreach (var item in _initializables)
            {
                item.Dispose();
            }
        }
        if (Instance != null)
        {
            GameObject.Destroy(Instance);
        }
    }

    public async Task Init()
    {
        List<IInit> inits = new List<IInit>();
        Instance = GameObject.Instantiate(_prefab);
        Instance.gameObject.name = _prefab.name;
        Instance.transform.SetParent(_parent, false);
        List<MonoBehaviour> components = Instance.GetComponentsInChildren<MonoBehaviour>().ToList();
        
        foreach (var script in components)
        {
            if(script is IInit initScript)
            {
                inits.Add(initScript);
                await initScript.Init();
            }
        }
        _initializables = inits.ToArray();
    }
}