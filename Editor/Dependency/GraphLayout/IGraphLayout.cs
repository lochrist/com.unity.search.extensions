namespace UnityEditor.Search
{
    interface IGraphLayout
    {
        bool Animated { get; }
        void Calculate(Graph graph, float deltaTime);
    }
}
