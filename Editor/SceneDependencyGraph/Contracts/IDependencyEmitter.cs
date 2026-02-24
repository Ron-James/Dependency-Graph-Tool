public interface IDependencyEmitter
{
    void EmitDependencies(IDependencyEmitContext context);
}

public interface IDependencyEmitContext
{
    void AddDependency(object target, string memberName, string dependencyKind = null, string details = null, bool isBroken = false);
}
