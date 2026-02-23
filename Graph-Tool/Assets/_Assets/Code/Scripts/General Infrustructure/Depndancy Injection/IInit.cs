using System.Collections;
using System.Threading.Tasks;
using UnityEngine;

public interface IInit : IDisposable
{
    Task Init();
}

public interface IDisposable
{
    void Dispose();
}


public interface IResettable
{
    void CaptureState();
    void Reset();
}