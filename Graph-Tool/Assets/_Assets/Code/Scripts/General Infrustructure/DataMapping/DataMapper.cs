using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;
using UnityEngine.Pool;

public interface IDataMap
{
    void BuildMap();
    void BuildMap(IEnumerable dataSet);
}

public interface IObjectPool<T>
{
    T Get();
    void Release(T obj);
}





public class GameObjectDataMap : IDataMap
{
    [OdinSerialize] private List<IEnumerable> dataSources = new();
    [OdinSerialize] IObjectPool<GameObject> objectPool;
    [OdinSerialize] List<IFilter> filters = new();

    [Button]
    public void BuildMap()
    {
        List<object> combinedData = new();
        foreach (var dataSource in dataSources)
        {
            combinedData.AddRange(dataSource.Cast<object>());
        }
        BuildMap(combinedData);
    }

    [Button]
    protected virtual bool IsValid(object data)
    {
        foreach(var filter in filters)
        {
            if (!filter.IsValid(data))
                return false;
        }
        return true;
    }
    public void BuildMap(IEnumerable dataSet)
    {
        int count = 0;
        foreach(object data in dataSet)
        {
            if(!IsValid(data)) continue;
            //Check filters
            GameObject obj = objectPool.Get();
            // Assume obj has a component that implements IDataBinding
            var components = obj.GetComponents<IDataBinding>();
            foreach (var component in components)
            {
                component.BindData(data, count);
                count++;
            }
        }
    }
}
