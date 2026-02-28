using System.Collections.Generic;
using Sirenix.Serialization;
using UnityEngine;

public class EqualityFilter<T> : IFilter<T>
{
    [SerializeField] private bool inverted = false;
    [OdinSerialize] protected T _value;
    private IFilter<T> _filterImplementation;

    public bool IsValid(T item)
    {
        return (EqualityComparer<T>.Default.Equals(_value, item) && !inverted) || 
               (!EqualityComparer<T>.Default.Equals(_value, item) && inverted);
    }
    
    
    public EqualityFilter(T value)
    {
        _value = value;
    }
    
    public EqualityFilter()
    {
        _value = default;
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