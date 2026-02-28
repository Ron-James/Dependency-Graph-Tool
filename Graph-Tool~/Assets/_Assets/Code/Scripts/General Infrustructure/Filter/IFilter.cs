using UnityEngine;

public interface IFilter<in T> : IFilter
{
    bool IsValid(T item);
}



public interface IFilter 
{
    bool IsValid(object item);
}