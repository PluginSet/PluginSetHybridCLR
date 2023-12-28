using System;
using PluginSet.Core;
using PluginSet.Core.Editor;
using UnityEditorInternal;
using UnityEngine;

namespace PluginSet.HybridCLR.Editor
{
    [BuildChannelsParams("HybridCLR", "HybridCLR使用设置")]
    public class BuildHybridCLRParams: ScriptableObject
    {
        [Tooltip("是否启用HybridCLR(不开启时，后续增加热更需要开启后整包更新)")]
        public bool Enable;
        
        [Tooltip("是否使用全局安装的il2cpp")]
        public bool DefaultUseGlobalIl2cpp;
        
        [Tooltip("热更新程序集定义")]
        public AssemblyDefinitionAsset[] HotUpdateAssemblyDefinitions;
        
        [Tooltip("非热更新程序集定义，需要同热更新程序集同时加载")]
        public AssemblyDefinitionAsset[] EntryAssemblyDefinitions;

        [Tooltip("热更新DLLS")]
        public string[] HotUpdateAssemblies;
        
        [Tooltip("热更新入口格式")]
        public string EntryFormat = "{0}Entry";

        public bool Empty => (HotUpdateAssemblies == null || HotUpdateAssemblies.Length == 0) &&
                         (HotUpdateAssemblyDefinitions == null || HotUpdateAssemblyDefinitions.Length == 0);
        
        [Tooltip("是否使用默认的热更新加载器")]
        public bool UseDefaultLoader = true;

        [Tooltip("热更新文件保存路径")]
        [FolderDrag]
        [VisibleCaseBoolValue("UseDefaultLoader", true)]
        public string HotFixAssetsPath;

        [Tooltip("是否复制AOT数据")]
        [VisibleCaseBoolValue("UseDefaultLoader", true)]
        public bool CopyAOTDatas = true;
    }
}