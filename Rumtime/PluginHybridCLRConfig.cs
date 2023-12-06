using UnityEngine;

namespace PluginSet.HybridCLR
{
    public class PluginHybridCLRConfig : ScriptableObject
    {
        public string EntryFormat = "{0}Entry";
        
        public string[] HotFixAssemblyNames;
        
        public string[] EntryAssemblyNames;
        
        public bool UseDefaultLoader;
    }
}