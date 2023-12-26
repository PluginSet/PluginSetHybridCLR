using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HybridCLR;
using UnityEngine;

namespace PluginSet.HybridCLR
{
    public abstract class HybridCLRLoader
    {
        private static HybridCLRLoader _instance;

        public static HybridCLRLoader Instance
        {
            get
            {
                if (_instance == null)
                {
                    throw new Exception("HybridCLR instance has not been set");
                }

                return _instance;
            }
            
            set
            {
                if (_instance != null)
                {
                    Debug.LogError("HybridCLR instance has already been set");
                }

                _instance = value;
            }
        }
        
        protected readonly Dictionary<string, Assembly> Assemblies = new Dictionary<string, Assembly>();
        protected readonly Dictionary<string, IEntry> Entries = new Dictionary<string, IEntry>();

        public Assembly GetAssembly(string name)
        {
            return Assemblies.TryGetValue(name, out var assembly) ? assembly : null;
        }
        
        protected virtual void OnAssemblyLoad(string name, Assembly assembly, string entryFormat)
        {
            var entryType = assembly.GetType(string.Format(entryFormat, name));
            if (entryType != null)
            {
                var entry = (IEntry) Activator.CreateInstance(entryType);
                Entries[name] = entry;
                entry.OnLoad();
            }
        }
        
        public virtual void UnloadAll()
        {
            foreach (var entry in Entries.Values)
            {
                entry.OnFree();
            }
            
            Entries.Clear();
        }
        
        protected abstract IEnumerable<byte[]> LoadAOTMetadataBytes();
        
        protected abstract byte[] LoadAssemblyBytes(string name);
        
        public void LoadAOTMetadata()
        {
            HomologousImageMode mode = HomologousImageMode.SuperSet;
            foreach (var data in LoadAOTMetadataBytes())
            {
                // 加载assembly对应的dll，会自动为它hook。一旦aot泛型函数的native函数不存在，用解释器版本代码
                LoadImageErrorCode err = RuntimeApi.LoadMetadataForAOTAssembly(data, mode);
                if (err != LoadImageErrorCode.OK)
                    Debug.LogWarning($"LoadMetadataForAOTAssembly mode:{mode} ret:{err}");
                else
                    Debug.Log($"LoadMetadataForAOTAssembly mode:{mode} ret:{err}");
            }
        }

        public void LoadAssembly(string name, string entryFormat, bool inDomain = false)
        {
            var assembly = inDomain ? LoadAssemblyInDomain(name) : LoadAssemblyInternal(name);
            if (assembly != null)
            {
                Assemblies[name] = assembly;
                OnAssemblyLoad(name, assembly, entryFormat);
            }
        }
        
        protected virtual Assembly LoadAssemblyInternal(string name)
        {
#if UNITY_EDITOR
            return LoadAssemblyInDomain(name);
#endif
            var data = LoadAssemblyBytes(name);
            if (data == null)
                throw new Exception($"Assembly {name} not found");
            
            return Assembly.Load(data);
        }
        
        protected static Assembly LoadAssemblyInDomain(string name)
        {
            return AppDomain.CurrentDomain.GetAssemblies().First(assem => assem.GetName().Name == name);
        }
    }
}