using System.Collections.Generic;
using Sirenix.Serialization;
using UnityEngine;

public class ListFilter<T> : IFilter<T>
{
    [SerializeField] private bool inverted = false;
    [OdinSerialize] List<T> _validItems = new List<T>();
    public bool IsValid(T item)
    {
        return (_validItems.Contains(item) && !inverted) || 
               (!_validItems.Contains(item) && inverted);
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