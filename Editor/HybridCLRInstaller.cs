using System;
using System.IO;
using HybridCLR.Editor;
using HybridCLR.Editor.Installer;
using PluginSet.Core;
using UnityEditor;
using Debug = UnityEngine.Debug;

namespace PluginSet.HybridCLR.Editor
{
    [InitializeOnLoad]
    public static class HybridCLRInstaller
    {
        static HybridCLRInstaller()
        {
            CheckPackageInstalled();

            var ctrl = new InstallerController();
            if (!ctrl.HasInstalledHybridCLR())
            {
                ctrl.InstallDefaultHybridCLR();
                CheckGlobalIl2CPPInstalled(SettingsUtil.HybridCLRSettings.enable && SettingsUtil.HybridCLRSettings.useGlobalIl2cpp);
            }
        }

        private static void CheckPackageInstalled()
        {
            var libName = "com.code-philosophy.hybridclr";
            var address = "https://github.com/focus-creative-games/hybridclr_unity.git";
            Global.CheckGitLibImported(libName, address);
        }

        public static void CheckGlobalIl2CPPInstalled(bool useGlobalIl2CPP)
        {
            var unityIl2CPPPath = Path.Combine(
#if UNITY_EDITOR_WIN
                EditorApplication.applicationContentsPath,
#else
                EditorApplication.applicationPath, "..",
#endif
                "il2cpp"
            );

            var unityLibil2CPPPath = Path.Combine(unityIl2CPPPath, "libil2cpp");
            var unityLibil2CPPBackPath = Path.Combine(unityIl2CPPPath, "libil2cpp_bak");
            
            if (useGlobalIl2CPP)
            {
                if (!IsDirectorySymbolicLink(unityLibil2CPPPath) && !Directory.Exists(unityLibil2CPPBackPath))
                    Directory.Move(unityLibil2CPPPath, unityLibil2CPPBackPath);
                
                if (Directory.Exists(unityLibil2CPPPath) || IsDirectorySymbolicLink(unityLibil2CPPPath))
                    Directory.Delete(unityLibil2CPPPath);
                
                LinkDirection(Path.Combine(SettingsUtil.LocalIl2CppDir, "libil2cpp"), unityLibil2CPPPath);
            }
            else
            {
                if (IsDirectorySymbolicLink(unityLibil2CPPPath))
                {
                    Directory.Delete(unityLibil2CPPPath);
                    LinkDirection(unityLibil2CPPBackPath, unityLibil2CPPPath);
                }
                else
                {
                    if (Directory.Exists(unityLibil2CPPBackPath))
                        LinkDirection(unityLibil2CPPBackPath, unityLibil2CPPPath);
                }
            }
        }
        
        private static void LinkDirection(string src, string dst)
        {
            Debug.Log($"link {src} to {dst}");
#if UNITY_EDITOR_WIN
            Global.ExecuteCommand("cmd.exe", false, "/c", $"mklink /D \"{dst}\" \"{src}\"");
#else
            Global.ExecuteCommand("ln", false, $"-s \"{src}\" \"{dst}\"");
#endif
        }
    
        private static bool IsDirectorySymbolicLink(string path)
        {
            try
            {
                FileAttributes attributes = File.GetAttributes(path);
            
                // 检查文件夹是否是软链接
                if ((attributes & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint)
                {
                    return true;
                }
            
                return false;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}