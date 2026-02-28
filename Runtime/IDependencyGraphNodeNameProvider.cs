namespace RonJames.DependencyGraphTool
{
    /// <summary>
    /// Optional contract for providing custom display names in the scene dependency graph.
    /// </summary>
    public interface IDependencyGraphNodeNameProvider
    {
        /// <summary>
        /// Custom label used for the node representing this object.
        /// </summary>
        string DependencyGraphNodeName { get; }
    }
}
