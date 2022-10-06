using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEditor.PackageManager.UI;
using System;
using UnityEngine.UIElements;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace Unity.PackageCreator.Editor
{
    public class PackageCreator : EditorWindow
    {
        [System.Serializable]
        public class AuthorData
        {
            public string name = "Name";
            public string email = "email";
            public string url = "https://www.unity3d.com";
        }

        [System.Serializable]
        public class RepositoryData
        {
            public string type = "git";
            public string url = "git@github.com:UserName/Repository.git";
        }

        [System.Serializable]
        public class SampleData : ISerializationCallbackReceiver
        {
            public string displayName;
            public string description;
            [HideInInspector] public string path;

            public void OnAfterDeserialize()
            {
                
            }

            public void OnBeforeSerialize()
            {
                path = $"Samples~/{displayName}";
            }
        }

        [System.Serializable]
        public class PackageData
        {
            public string name = "com.unity.custom-package";
            public string displayName = "Custom Package";
            public string version = "0.0.1-preview.1";
            public string unity = "2021.3";
            public string description = "";
            public AuthorData author;
            public List<string> keywords = new List<string>() { "unity", "editor" };
            public RepositoryData repository;
            public List<SampleData> samples = new List<SampleData>();
            [HideInInspector] public bool hideInEditor;
        }

        [System.Serializable]
        public class AsmDefData
        {
            public string name;
            public string rootNamespace;
            public List<string> references;
            public List<string> includePlatforms;
            public List<string> excludePlatforms;
            public bool allowUnsafeCode;
            public bool overrideReferences;
            public List<string> precompiledReferences;
            public bool autoReferenced = true;
            public List<string> defineConstraints;
            public List<string> versionDefines;
            public bool noEngineReferences;

            public AsmDefData(string name,
                List<string> references,
                List<string> includePlatforms)
            {
                this.name = this.rootNamespace = name;
                this.references = references;
                this.includePlatforms = includePlatforms;
            }
        }

        [SerializeField] PackageData packageData;
        SerializedObject serializedObject;
        SerializedProperty serializedProperty;

        bool addReadMe = true, addChangeLog = true, addRuntimeFolder = true, addEditorFolder = true, addDocumentationFolder = true;
        string asmDefBaseName = "Unity.CustomPackage";

        List<string> folders = new List<string>() { "Assets" };

        bool addToProject = true;

        static AddRequest packManAddRequest;

        [MenuItem("Tools/Package Creator")]
        public static void Init()
        {
            PackageCreator window = EditorWindow.GetWindow<PackageCreator>("Package Creator");
            window.titleContent = new GUIContent("Package Creator");
            window.Show();
        }

        private void OnEnable()
        {
            serializedObject = new SerializedObject(this);
            serializedProperty = serializedObject.FindProperty("packageData");
        }

        void OnGUI()
        {
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(serializedProperty);
            if (EditorGUI.EndChangeCheck())
                serializedObject.ApplyModifiedProperties();

            addReadMe = GUILayout.Toggle(addReadMe, "Add ReadMe");
            addChangeLog = GUILayout.Toggle(addChangeLog, "Add ChangeLog");
            addRuntimeFolder = GUILayout.Toggle(addRuntimeFolder, "Add Runtime Folder");
            addEditorFolder = GUILayout.Toggle(addEditorFolder, "Add Editor Folder");
            if (addRuntimeFolder || addEditorFolder)
                asmDefBaseName = GUILayout.TextField(asmDefBaseName);
            addDocumentationFolder = GUILayout.Toggle(addDocumentationFolder, "Add Documentation Folder");
            addToProject = GUILayout.Toggle(addToProject, "Add To Project");

            if (GUILayout.Button("Build"))
                BuildPackage();
        }

        void BuildPackage()
        {
            var path = EditorUtility.OpenFolderPanel("Select a location to save the package", "", "");
            if (path == string.Empty)
                return;

            var targetDir = Path.Combine(path, packageData.name);
            if (Directory.Exists(targetDir))
            {
                Debug.LogWarning($"{path} already contains a folder '{packageData.name}'");
                return;
            }

            Directory.CreateDirectory(targetDir);

            using (StreamWriter streamWriter = new StreamWriter(Path.Combine(targetDir, "package.json")))
            {
                streamWriter.Write(JsonUtility.ToJson(packageData, true));
            }

            if (addReadMe)
            {
                using (StreamWriter streamWriter = new StreamWriter(Path.Combine(targetDir, "README.md")))
                {
                    streamWriter.Write($"# {packageData.displayName}\n{packageData.description}");
                }
            }

            if (addChangeLog)
            {
                using (StreamWriter streamWriter = new StreamWriter(Path.Combine(targetDir, "CHANGELOG.md")))
                {
                    streamWriter.Write("# Changelog\n" +
                        "All notable changes to this package will be documented in this file.\n\n" +
                        "The format is based on [Keep a Changelog](http://keepachangelog.com/en/1.0.0/)\n" +
                        "and this project adheres to [Semantic Versioning](http://semver.org/spec/v2.0.0.html).\n\n" +
                        $"## [{packageData.version}] - {DateTime.Now.ToString("yyyy-MM-dd")}\n\n" +
                        $"### This is the first release of *Unity Package <{packageData.name}>*.\n\n" +
                        "*Short description of this release*");
                }
            }

            if (addRuntimeFolder)
            {
                var runtimeDir = Path.Combine(targetDir, "Runtime");
                Directory.CreateDirectory(runtimeDir);

                AsmDefData runtimeAsmDef = new AsmDefData(asmDefBaseName, new List<string>(), new List<string>());
                using (StreamWriter streamWriter = new StreamWriter(Path.Combine(runtimeDir, $"{asmDefBaseName}.asmdef")))
                {
                    streamWriter.Write(JsonUtility.ToJson(runtimeAsmDef, true));
                }
            }

            if (addEditorFolder)
            {
                var editorDir = Path.Combine(targetDir, "Editor");
                Directory.CreateDirectory(editorDir);

                AsmDefData editorAsmDef = new AsmDefData($"{asmDefBaseName}.Editor", addRuntimeFolder ? new List<string>() { $"{asmDefBaseName}" } : new List<string>(), new List<string>() { "Editor" });
                using (StreamWriter streamWriter = new StreamWriter(Path.Combine(editorDir, $"{asmDefBaseName}.Editor.asmdef")))
                {
                    streamWriter.Write(JsonUtility.ToJson(editorAsmDef, true));
                }
            }

            if (addDocumentationFolder)
                Directory.CreateDirectory(Path.Combine(targetDir, "Documentation~"));

            if (packageData.samples.Count > 0)
            {
                Directory.CreateDirectory(Path.Combine(targetDir, "Samples~"));

                foreach (var sample in packageData.samples)
                {
                    Directory.CreateDirectory(Path.Combine(targetDir, sample.path));
                }
            }

            foreach (var dir in folders)
                Directory.CreateDirectory(Path.Combine(targetDir, dir));

            EditorUtility.RevealInFinder(Path.Combine(targetDir, "package.json"));

            if (addToProject)
            {
                packManAddRequest = Client.Add($"file:{targetDir}");
                EditorApplication.update += Progress;
            }
        }

        static void Progress()
        {
            if (packManAddRequest.IsCompleted)
            {
                if (packManAddRequest.Status == StatusCode.Success)
                {
                    // UNDONE : too early, the package isn't visible yet in Project Window
                    //EditorUtility.FocusProjectWindow();
                    //var pkg = AssetDatabase.LoadMainAssetAtPath($"Packages/<{packManAddRequest.Result.displayName}>/package.json");
                    //EditorGUIUtility.PingObject(pkg);

                    Window.Open(packManAddRequest.Result.displayName);
                    //Selection.SetActiveObjectWithContext(pkg, pkg);
                }
                else if (packManAddRequest.Status >= StatusCode.Failure)
                {
                    Debug.LogWarning(packManAddRequest.Error.message);
                }

                EditorApplication.update -= Progress;
            }
        }
    }

    //public class PacManExtension : IPackageManagerExtension
    //{
    //    PackageInfo packageInfo;

    //    public VisualElement CreateExtensionUI()
    //    {
    //        var root = new VisualElement();

    //        return root;
    //    }

    //    public void OnPackageAddedOrUpdated(PackageInfo packageInfo)
    //    {
            
    //    }

    //    public void OnPackageRemoved(PackageInfo packageInfo)
    //    {
            
    //    }

    //    public void OnPackageSelectionChange(PackageInfo packageInfo)
    //    {
    //        this.packageInfo = packageInfo;
    //    }
    //}

    //public static class PacManExtensionInit
    //{
    //    [InitializeOnLoadMethod]
    //    public static void Init()
    //    {
    //        PacManExtension pacManExtension = new PacManExtension();
    //        PackageManagerExtensions.RegisterExtension(pacManExtension);
    //    }
    //}
}