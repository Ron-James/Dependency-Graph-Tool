using UnityEngine;

public interface INamedEntry
{
    string Name { get; set; }
}


public interface IDescribed
{
    string Description { get; }
}



public interface IValueAsset<T>
{
    T Value { get; set; }
}
public interface IContextual<T> : IContext<T> where T : class
{
    T Context { set; }
}

public interface IContext<T> where T : class
{
    T Context { get; }
}