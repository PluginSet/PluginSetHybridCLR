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
#if UNITY_2021_1_OR_NEWER
                var targetLevel = ApiCompatibilityLevel.NET_Unity_4_8;
#else
                var targetLevel = ApiCompatibilityLevel.NET_4_6;
#endif
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

        [OnSyncEditorSetting(int.MaxValue)]
        public static void OnBuildPrepare(BuildProcessorContext context)
        {
            var buildParams = context.BuildChannels.Get<BuildHybridCLRParams>();
            var enable = buildParams.Enable;
            if (!enable)
                return;
            
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

        [OnBuildPatches(int.MinValue)]
        public static void PreparePatches(BuildProcessorContext context)
        {
            var buildParams = context.BuildChannels.Get<BuildHybridCLRParams>();
            if (!buildParams.Enable || !buildParams.UseDefaultLoader || !buildParams.CopyAOTDatas)
                return;
            
            // 生成裁剪后的aot dll
            StripAOTDllCommand.GenerateStripedAOTDlls(context.BuildTarget);
        }

        [OnBuildBundlesCompleted]
        public static void OnBuildBundlesCompleted(BuildProcessorContext context, string streamingPath,
            string streamingName, AssetBundleManifest manifest, bool patchBundle = false)
        {
            if (!patchBundle)
                return;
            
            var buildParams = context.BuildChannels.Get<BuildHybridCLRParams>();
            if (!buildParams.Enable || !buildParams.UseDefaultLoader || !buildParams.CopyAOTDatas)
                return;

            SaveAOTMetadataUnionBytes(context, streamingPath, true);
        }

        [BuildProjectCompleted]
        public static void OnProjectBuildCompleted(BuildProcessorContext context, string exportPath)
        {
            HybridWorkAfterProjectExport(context.BuildTarget, exportPath);
            
            var buildParams = context.BuildChannels.Get<BuildHybridCLRParams>(); 
            if (!buildParams.Enable || !buildParams.UseDefaultLoader || !buildParams.CopyAOTDatas)
                return;
            
            var assetPath = Global.GetProjectAssetsPath(context.BuildTarget, exportPath);
            if (string.IsNullOrEmpty(assetPath))
                return;
            
            SaveAOTMetadataUnionBytes(context, assetPath, false);
        }

        private static void HybridWorkAfterProjectExport(BuildTarget target, string exportPath)
        {
            Il2CppDefGeneratorCommand.GenerateIl2CppDef();
            // 桥接函数生成依赖于AOT dll，必须保证已经build过，生成AOT dll
            MethodBridgeGeneratorCommand.GenerateMethodBridge(target);
            ReversePInvokeWrapperGeneratorCommand.GenerateReversePInvokeWrapper(target);

            string projectIl2CPPPath;
            if (target == BuildTarget.Android)
            {
                projectIl2CPPPath = Path.Combine(exportPath, "unityLibrary", "src", "main", "Il2CppOutputProject", "IL2CPP", "libil2cpp");
            }
            else if (target == BuildTarget.iOS)
            {
                projectIl2CPPPath = Path.Combine(exportPath, "Libraries", "libil2cpp", "include");
            }
            else
            {
                return;
            }
            
            var srcPath = Path.Combine(SettingsUtil.LocalIl2CppDir, "libil2cpp");
            Directory.Delete(projectIl2CPPPath, true);
            Global.CopyFilesTo(projectIl2CPPPath, srcPath, "*.*");
        }

        private static void SaveAOTMetadataUnionBytes(BuildProcessorContext context, string streamingPath, bool enableWriteManifest = false)
        {
            var buildParams = context.BuildChannels.Get<BuildHybridCLRParams>();
            if (!buildParams.Enable || !buildParams.UseDefaultLoader)
                return;

            var fileName = HybridCLRDefaultLoader.HybridAOTMetadataPathName;
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
                var fileNames = new List<string>();
                foreach (var name in ReferenceModules(analyzer.AotGenericTypes, analyzer.AotGenericMethods))
                {
                    fileNames.Add(name);
                    bytes.Add(File.ReadAllBytes(Path.Combine(aotOutputPath, name)));
                }
                
                if (enableWriteManifest)
                    File.WriteAllLines(Path.Combine(streamingPath, fileName + ".manifest"), fileNames);
                
                var unionBytes = AOTMetaDataBytesHelper.UnionBytes(bytes.ToArray());
                var unionBytesPath = Path.Combine(streamingPath, fileName);
                File.WriteAllBytes(unionBytesPath, unionBytes);
            }
            
            context.ExtendStreamingFiles(fileName);
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
                Global.CheckAndCopyFile(dllPath, assetPath);
                
                context.AddBuildBundle(HybridCLRDefaultLoader.HybridCLRBundlePrefix + assemblyName, Global.GetAssetPath(assetPath));
            }
        }
    }
}