using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HybridCLR.Editor;
using HybridCLR.Editor.AOT;
using HybridCLR.Editor.Commands;
using HybridCLR.Editor.Meta;
using HybridCLR.Editor.Settings;
using PluginSet.Core;
using PluginSet.Core.Editor;
using UnityEditor;
using UnityEngine;

namespace PluginSet.HybridCLR.Editor
{
    [BuildTools]
    public static class BuildHybridCLRTools
    {
        [OnSyncEditorSetting]
        public static void OnSyncEditorSetting(BuildProcessorContext context)
        {
            var hybridClrSettings = SettingsUtil.HybridCLRSettings;
            var buildParams = context.BuildChannels.Get<BuildHybridCLRParams>();
            var enable = buildParams.Enable;
            hybridClrSettings.enable = enable;
            context.AddLinkAssembly("PluginSet.HybridCLR");
            
            if (enable)
            {
                var level = PlayerSettings.GetApiCompatibilityLevel(context.BuildTargetGroup);
                var targetLevel = ApiCompatibilityLevel.NET_Unity_4_8;
                if (level != targetLevel)
                {
                    Debug.LogWarning($"API Compatibility Level must be {targetLevel} for HybridCLR, but is {level}");
                    PlayerSettings.SetApiCompatibilityLevel(context.BuildTargetGroup, targetLevel);
                }
                
                hybridClrSettings.hotUpdateAssemblyDefinitions = buildParams.HotUpdateAssemblyDefinitions;
                hybridClrSettings.hotUpdateAssemblies = buildParams.HotUpdateAssemblies;

                var isGlobal = buildParams.DefaultUseGlobalIl2cpp;
                if (context.BuildTarget == BuildTarget.WebGL)
                {
                    // WEBGL平台必须使用全局安装方式
                    isGlobal = true;
                }
                
                hybridClrSettings.useGlobalIl2cpp = isGlobal;
                HybridCLRInstaller.CheckGlobalIl2CPPInstalled(isGlobal);
            }
            else
            {
                HybridCLRInstaller.CheckGlobalIl2CPPInstalled(false);
            }
            
            var list = new List<string>(buildParams.HotUpdateAssemblies);
            list.AddRange(buildParams.HotUpdateAssemblyDefinitions.Select(assemblyDefinition => assemblyDefinition.name));

            var listEntry = new List<string>();
            if (buildParams.EntryAssemblyDefinitions != null)
                listEntry.AddRange(buildParams.EntryAssemblyDefinitions.Select(assemblyDefinition => assemblyDefinition.name));
            
            var pluginConfig = context.Get<PluginSetConfig>("pluginsConfig");
            var config = pluginConfig.AddConfig<PluginHybridCLRConfig>("HybridCLR");
            config.HotFixAssemblyNames = list.ToArray();
            config.EntryAssemblyNames = listEntry.ToArray();
            config.EntryFormat = buildParams.EntryFormat;
            config.UseDefaultLoader = buildParams.UseDefaultLoader;

            HybridCLRSettings.Save();
            AssetDatabase.Refresh();
        }

        [BuildPrepareCallback]
        public static void OnBuildPrepare(BuildProcessorContext context, string exportPath)
        {
            CompileDllCommand.CompileDll(context.BuildTarget);
            LinkGeneratorCommand.GenerateLinkXml(context.BuildTarget);
        }
        
        [OnBuildBundles]
        [OnBuildPatches]
        public static void OnBuildBundles(BuildProcessorContext context)
        {
            var buildParams = context.BuildChannels.Get<BuildHybridCLRParams>();
            if (!buildParams.Enable || !buildParams.UseDefaultLoader)
                return;

            var assetPath = buildParams.HotFixAssetsPath;
            if (string.IsNullOrEmpty(buildParams.HotFixAssetsPath))
                assetPath = Path.Combine(Application.dataPath, "HybridCLRGenerate", "AssemblyDlls");

            var buildTarget = context.BuildTarget;
            CopyAssembliesDllsToAssets(context, buildTarget, assetPath, buildParams.HotUpdateAssemblies);
            CopyAssembliesDllsToAssets(context, buildTarget, assetPath,
                buildParams.HotUpdateAssemblyDefinitions.Select(assemblyDefinition => assemblyDefinition.name));
            
            AssetDatabase.Refresh();
        }

        // TODO
        public static void SaveAOTMetadataUnionBytes(BuildProcessorContext context)
        {
            var buildParams = context.BuildChannels.Get<BuildHybridCLRParams>();
            if (!buildParams.Enable || !buildParams.UseDefaultLoader)
                return;
            
            var assetPath = buildParams.HotFixAssetsPath; // TODO
            
            var gs = SettingsUtil.HybridCLRSettings;
            List<string> hotUpdateDllNames = SettingsUtil.HotUpdateAssemblyNamesExcludePreserved;

            var aotOutputPath = SettingsUtil.GetAssembliesPostIl2CppStripDir(context.BuildTarget);
            using (AssemblyReferenceDeepCollector collector = new AssemblyReferenceDeepCollector(MetaUtil.CreateHotUpdateAndAOTAssemblyResolver(context.BuildTarget, hotUpdateDllNames), hotUpdateDllNames))
            {
                var analyzer = new Analyzer(new Analyzer.Options
                {
                    MaxIterationCount = Math.Min(20, gs.maxGenericReferenceIteration),
                    Collector = collector,
                });

                analyzer.Run();

                var bytes = new List<byte[]>();
                foreach (var name in ReferenceModules(analyzer.AotGenericTypes, analyzer.AotGenericMethods))
                {
                    bytes.Add(File.ReadAllBytes(Path.Combine(aotOutputPath, name)));
                }
                
                var unionBytes = AOTMetaDataBytesHelper.UnionBytes(bytes.ToArray());
                var unionBytesPath = Path.Combine(assetPath, $"{HybridCLRDefaultLoader.HybridAOTMetadataPathName}.bytes");
                File.WriteAllBytes(unionBytesPath, unionBytes);
            }
        }

        private static IEnumerable<string> ReferenceModules(List<GenericClass> types, List<GenericMethod> methods)
        {
            List<dnlib.DotNet.ModuleDef> modules = new HashSet<dnlib.DotNet.ModuleDef>(
                types.Select(t => t.Type.Module).Concat(methods.Select(m => m.Method.Module))).ToList();
            return modules.Select(m => $"{m.Name}");
        }

        private static void CopyAssembliesDllsToAssets(BuildProcessorContext context, BuildTarget target, string hotfixSavePath, IEnumerable<string> assembliesNames)
        {
            var dllsPath = SettingsUtil.GetHotUpdateDllsOutputDirByTarget(target);
            foreach (var assemblyName in assembliesNames)
            {
                var dllPath = $"{dllsPath}/{assemblyName}.dll";
                var assetPath = $"{hotfixSavePath}/{assemblyName}.bytes";
                FileUtil.CopyFileOrDirectory(dllPath, assetPath);
                
                context.AddBuildBundle(HybridCLRDefaultLoader.HybridCLRBundlePrefix + assemblyName, Global.GetAssetPath(assetPath));
            }
        }
    }
}