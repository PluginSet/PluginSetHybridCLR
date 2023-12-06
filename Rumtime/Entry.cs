namespace PluginSet.HybridCLR
{
    public interface IEntry
    {
        void OnLoad();

        void OnFree();
    }
}