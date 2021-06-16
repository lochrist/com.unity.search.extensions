namespace UnityEditor.Search
{
    interface IGraphLayout
    {
        bool Animated { get; }
        bool Calculate(Graph graph, float deltaTime);
    }
}
