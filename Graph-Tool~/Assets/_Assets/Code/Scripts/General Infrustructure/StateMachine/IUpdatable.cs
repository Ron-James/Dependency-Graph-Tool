public interface IUpdatable
{
    // Called every frame to update the implementing component.
    void Update();
}


public interface IFixedUpdatable
{
    // Called every fixed frame to update the implementing component.
    void FixedUpdate();
}