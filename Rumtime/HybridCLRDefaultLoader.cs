using System.Collections.Generic;
using System.Linq;
using PluginSet.Core;
using UnityEngine;

namespace PluginSet.HybridCLR
{
    public class HybridCLRDefaultLoader: HybridCLRLoader
    {
        public const string HybridCLRBundlePrefix = "hotfix_";
        public const string HybridAOTMetadataPathName = "HybridAOTMetadata";
        
        private static readonly List<byte[]> EmptyBytesList = new List<byte[]>();

        protected override IEnumerable<byte[]> LoadAOTMetadataBytes()
        {
            var bundle = ResourcesManager.Instance.LoadBundle(HybridAOTMetadataPathName);
            if (bundle == null)
                return EmptyBytesList;
            
            var assets = bundle.LoadAllAssets<TextAsset>();
            return assets.Select(asset => asset.bytes);
        }

        protected override byte[] LoadAssemblyBytes(string name)
        {
            var bundle = ResourcesManager.Instance.LoadBundle(HybridCLRBundlePrefix + name);
            if (bundle == null)
                return null;
            
            var asset = bundle.LoadAsset<TextAsset>(name);
            byte[] result = null;
            if (asset != null)
                result = asset.bytes;
            
            ResourcesManager.Instance.ReleaseBundle(bundle);
            return result;
        }
    }
}