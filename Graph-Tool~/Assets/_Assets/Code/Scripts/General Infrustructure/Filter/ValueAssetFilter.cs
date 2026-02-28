using System.Collections.Generic;
using Sirenix.Serialization;
using UnityEngine;

public class ValueAssetFilter<T> : IFilter<T>
{
    [SerializeField] private bool inverted = false;
    [OdinSerialize] protected IValueAsset<T> _valueAsset;
    
    public bool IsValid(T item)
    {
        return (EqualityComparer<T>.Default.Equals(_valueAsset.Value, item) && !inverted) ||
               (!EqualityComparer<T>.Default.Equals(_valueAsset.Value, item) && inverted);
    }

    public bool IsValid(object item)
    {
        if (item is T tItem)
        {
            return IsValid(tItem);
        }
        return false;
    }
}

public class ValueAssetListFilter<T> : IFilter<T>
{
    [SerializeField] private bool inverted = false;
    [OdinSerialize] protected List<IValueAsset<T>> _valueAsset = new();
    
    public bool IsValid(T item)
    {
        return _valueAsset.Exists(va => EqualityComparer<T>.Default.Equals(va.Value, item)) && !inverted ||
               !_valueAsset.Exists(va => EqualityComparer<T>.Default.Equals(va.Value, item)) && inverted;
    }

    public bool IsValid(object item)
    {
        if (item is T tItem)
        {
            return IsValid(tItem);
        }
        return false;
    }
}