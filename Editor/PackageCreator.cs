using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;

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
            public List<SampleData> samples;
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

        bool addReadMe = true, addRuntimeFolder = true, addEditorFolder = true, addDocumentationFolder = true;
        string asmDefBaseName = "Unity.CustomPackage";

        List<string> folders = new List<string>() { "Assets" };

        bool addToProject = true;

        static AddRequest packManAddRequest;

        [MenuItem("Tools/Package Creator")]
        static void Init()
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
                    Debug.Log("Installed: " + packManAddRequest.Result.packageId);

                    var pkg = AssetDatabase.LoadMainAssetAtPath($"Packages/<{packManAddRequest.Result.displayName}>/package.json");
                    EditorGUIUtility.PingObject(pkg);
                }
                else if (packManAddRequest.Status >= StatusCode.Failure)
                {
                    Debug.Log(packManAddRequest.Error.message);
                }

                EditorApplication.update -= Progress;
            }
        }
    }
}