using System.Collections;
using PluginSet.Core;

namespace PluginSet.HybridCLR
{
    [PluginRegister]
    public class PluginHybridCLR : PluginBase, IStartPlugin
    {
        public override string Name => "HybridCLR";
        public int StartOrder => PluginsStartOrder.Default - 8;
        public bool IsRunning { get; private set; }
        
        private string[] _hotFixAssemblies;
        private string[] _entryAssemblies;
        private string _entryFormat;

        protected override void Init(PluginSetConfig config)
        {
            var cfg = config.Get<PluginHybridCLRConfig>();
            _hotFixAssemblies = cfg.HotFixAssemblyNames;
            _entryAssemblies = cfg.EntryAssemblyNames;
            _entryFormat = cfg.EntryFormat;

            if (cfg.UseDefaultLoader)
                HybridCLRLoader.Instance = new HybridCLRDefaultLoader();
        }

        public IEnumerator StartPlugin()
        {
            if (IsRunning)
                yield break;
            
            var launcher = HybridCLRLoader.Instance;
            launcher.LoadAOTMetadata();
            
            for (int i = 0; i < _hotFixAssemblies.Length; i++)
            {
                launcher.LoadAssembly(_hotFixAssemblies[i], _entryFormat, false);
            }
            
            for (int i = 0; i < _entryAssemblies.Length; i++)
            {
                launcher.LoadAssembly(_entryAssemblies[i], _entryFormat, true);
            }

            IsRunning = true;
        }

        public void DisposePlugin(bool isAppQuit = false)
        {
            if (!IsRunning)
                return;
            
            HybridCLRLoader.Instance.UnloadAll();
            IsRunning = false;
        }
    }
}