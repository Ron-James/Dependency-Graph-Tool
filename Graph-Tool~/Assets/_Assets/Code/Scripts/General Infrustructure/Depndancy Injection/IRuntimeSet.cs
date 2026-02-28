using System.Collections;
using System.Collections.Generic;



public interface IRuntimeSet : IEnumerable
{
    void Add(object item);
    void Remove(object item);
    void Clear();
}
public interface IRuntimeSet<T> : IRuntimeSet, IEnumerable<T>
{
    void Add(T item);
    void Remove(T item);
    void Clear();
}