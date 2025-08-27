using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityMcp.Models;

namespace UnityMcp.Tools
{
    /// <summary>
    /// Handles CRUD operations for C# scripts within the Unity project.
    /// 对应方法名: manage_script
    /// </summary>
    public class ManageScript : StateMethodBase
    {
        protected override StateTree CreateStateTree()
        {
            return StateTreeBuilder
                .Create()
                .Key("action")
                    .Leaf("create", HandleCreateAction)
                    .Leaf("read", HandleReadAction)
                    .Leaf("update", HandleUpdateAction)
                    .Leaf("delete", HandleDeleteAction)
                .Build();
        }

        // --- State Tree Action Handlers ---

        /// <summary>
        /// 处理创建脚本的操作
        /// </summary>
        private object HandleCreateAction(JObject args)
        {
            try
            {
                var scriptInfo = ParseScriptArguments(args);
                if (scriptInfo.ErrorResponse != null)
                    return scriptInfo.ErrorResponse;

                // Ensure the target directory exists
                var dirResult = EnsureDirectoryExists(scriptInfo.FullPathDir);
                if (dirResult != null)
                    return dirResult;

                LogInfo($"[ManageScript] Creating script '{scriptInfo.Name}.cs' at '{scriptInfo.RelativePath}'");
                return CreateScript(scriptInfo.FullPath, scriptInfo.RelativePath, scriptInfo.Name, scriptInfo.Contents, scriptInfo.ScriptType, scriptInfo.NamespaceName);
            }
            catch (Exception e)
            {
                if (UnityMcp.EnableLog) Debug.LogError($"[ManageScript] Create action failed: {e}");
                return Response.Error($"Internal error processing create action: {e.Message}");
            }
        }

        /// <summary>
        /// 处理读取脚本的操作
        /// </summary>
        private object HandleReadAction(JObject args)
        {
            try
            {
                var scriptInfo = ParseScriptArguments(args);
                if (scriptInfo.ErrorResponse != null)
                    return scriptInfo.ErrorResponse;

                LogInfo($"[ManageScript] Reading script at '{scriptInfo.RelativePath}'");
                return ReadScript(scriptInfo.FullPath, scriptInfo.RelativePath);
            }
            catch (Exception e)
            {
                if (UnityMcp.EnableLog) Debug.LogError($"[ManageScript] Read action failed: {e}");
                return Response.Error($"Internal error processing read action: {e.Message}");
            }
        }

        /// <summary>
        /// 处理更新脚本的操作
        /// </summary>
        private object HandleUpdateAction(JObject args)
        {
            try
            {
                var scriptInfo = ParseScriptArguments(args);
                if (scriptInfo.ErrorResponse != null)
                    return scriptInfo.ErrorResponse;

                LogInfo($"[ManageScript] Updating script '{scriptInfo.Name}.cs' at '{scriptInfo.RelativePath}'");
                return UpdateScript(scriptInfo.FullPath, scriptInfo.RelativePath, scriptInfo.Name, scriptInfo.Contents);
            }
            catch (Exception e)
            {
                if (UnityMcp.EnableLog) Debug.LogError($"[ManageScript] Update action failed: {e}");
                return Response.Error($"Internal error processing update action: {e.Message}");
            }
        }

        /// <summary>
        /// 处理删除脚本的操作
        /// </summary>
        private object HandleDeleteAction(JObject args)
        {
            try
            {
                var scriptInfo = ParseScriptArguments(args);
                if (scriptInfo.ErrorResponse != null)
                    return scriptInfo.ErrorResponse;

                LogInfo($"[ManageScript] Deleting script at '{scriptInfo.RelativePath}'");
                return DeleteScript(scriptInfo.FullPath, scriptInfo.RelativePath);
            }
            catch (Exception e)
            {
                if (UnityMcp.EnableLog) Debug.LogError($"[ManageScript] Delete action failed: {e}");
                return Response.Error($"Internal error processing delete action: {e.Message}");
            }
        }

        // --- Internal Helper Methods ---

        /// <summary>
        /// 脚本信息结构
        /// </summary>
        private struct ScriptInfo
        {
            public string Name;
            public string Contents;
            public string ScriptType;
            public string NamespaceName;
            public string FullPath;
            public string RelativePath;
            public string FullPathDir;
            public object ErrorResponse;
        }

        /// <summary>
        /// 解析脚本相关参数
        /// </summary>
        private ScriptInfo ParseScriptArguments(JObject args)
        {
            var info = new ScriptInfo();

            // Extract basic args
            string name = args["name"]?.ToString();
            string path = args["path"]?.ToString(); // Relative to Assets/
            string contents = null;

            // Check if we have base64 encoded contents
            bool contentsEncoded = args["contentsEncoded"]?.ToObject<bool>() ?? false;
            if (contentsEncoded && args["encodedContents"] != null)
            {
                try
                {
                    contents = DecodeBase64(args["encodedContents"].ToString());
                }
                catch (Exception e)
                {
                    info.ErrorResponse = Response.Error($"Failed to decode script contents: {e.Message}");
                    return info;
                }
            }
            else
            {
                contents = args["contents"]?.ToString();
            }

            string scriptType = args["scriptType"]?.ToString();
            string namespaceName = args["namespace"]?.ToString();

            // Validate required args
            if (string.IsNullOrEmpty(name))
            {
                info.ErrorResponse = Response.Error("Name parameter is required.");
                return info;
            }

            // Basic name validation
            if (!Regex.IsMatch(name, @"^[a-zA-Z_][a-zA-Z0-9_]*$"))
            {
                info.ErrorResponse = Response.Error(
                    $"Invalid script name: '{name}'. Use only letters, numbers, underscores, and don't start with a number."
                );
                return info;
            }

            // Process path
            string relativeDir = path ?? "Scripts";
            if (!string.IsNullOrEmpty(relativeDir))
            {
                relativeDir = relativeDir.Replace('\\', '/').Trim('/');
                if (relativeDir.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
                {
                    relativeDir = relativeDir.Substring("Assets/".Length).TrimStart('/');
                }
            }
            if (string.IsNullOrEmpty(relativeDir))
            {
                relativeDir = "Scripts";
            }

            // Construct paths
            string scriptFileName = $"{name}.cs";
            string fullPathDir = Path.Combine(Application.dataPath, relativeDir);
            string fullPath = Path.Combine(fullPathDir, scriptFileName);
            string relativePath = Path.Combine("Assets", relativeDir, scriptFileName).Replace('\\', '/');

            // Populate info
            info.Name = name;
            info.Contents = contents;
            info.ScriptType = scriptType;
            info.NamespaceName = namespaceName;
            info.FullPath = fullPath;
            info.RelativePath = relativePath;
            info.FullPathDir = fullPathDir;

            return info;
        }

        /// <summary>
        /// 确保目录存在
        /// </summary>
        private object EnsureDirectoryExists(string fullPathDir)
        {
            try
            {
                Directory.CreateDirectory(fullPathDir);
                return null; // Success
            }
            catch (Exception e)
            {
                return Response.Error($"Could not create directory '{fullPathDir}': {e.Message}");
            }
        }

        // --- Action Implementations ---

        /// <summary>
        /// Decode base64 string to normal text
        /// </summary>
        private static string DecodeBase64(string encoded)
        {
            byte[] data = Convert.FromBase64String(encoded);
            return System.Text.Encoding.UTF8.GetString(data);
        }

        /// <summary>
        /// Encode text to base64 string
        /// </summary>
        private static string EncodeBase64(string text)
        {
            byte[] data = System.Text.Encoding.UTF8.GetBytes(text);
            return Convert.ToBase64String(data);
        }

        private object CreateScript(
            string fullPath,
            string relativePath,
            string name,
            string contents,
            string scriptType,
            string namespaceName
        )
        {
            // Check if script already exists
            if (File.Exists(fullPath))
            {
                return Response.Error(
                    $"Script already exists at '{relativePath}'. Use 'update' action to modify."
                );
            }

            // Generate default content if none provided
            if (string.IsNullOrEmpty(contents))
            {
                contents = GenerateDefaultScriptContent(name, scriptType, namespaceName);
            }

            // Validate syntax (basic check)
            if (!ValidateScriptSyntax(contents))
            {
                // Optionally return a specific error or warning about syntax
                // return Response.Error("Provided script content has potential syntax errors.");
                if (UnityMcp.EnableLog) Debug.LogWarning($"Potential syntax error in script being created: {name}");
            }

            try
            {
                File.WriteAllText(fullPath, contents);
                AssetDatabase.ImportAsset(relativePath);
                AssetDatabase.Refresh(); // Ensure Unity recognizes the new script
                return Response.Success(
                    $"Script '{name}.cs' created successfully at '{relativePath}'.",
                    new { path = relativePath }
                );
            }
            catch (Exception e)
            {
                return Response.Error($"Failed to create script '{relativePath}': {e.Message}");
            }
        }

        private object ReadScript(string fullPath, string relativePath)
        {
            if (!File.Exists(fullPath))
            {
                return Response.Error($"Script not found at '{relativePath}'.");
            }

            try
            {
                string contents = File.ReadAllText(fullPath);

                // Return both normal and encoded contents for larger files
                bool isLarge = contents.Length > 10000; // If content is large, include encoded version
                var responseData = new
                {
                    path = relativePath,
                    contents = contents,
                    // For large files, also include base64-encoded version
                    encodedContents = isLarge ? EncodeBase64(contents) : null,
                    contentsEncoded = isLarge,
                };

                return Response.Success(
                    $"Script '{Path.GetFileName(relativePath)}' read successfully.",
                    responseData
                );
            }
            catch (Exception e)
            {
                return Response.Error($"Failed to read script '{relativePath}': {e.Message}");
            }
        }

        private object UpdateScript(
            string fullPath,
            string relativePath,
            string name,
            string contents
        )
        {
            if (!File.Exists(fullPath))
            {
                return Response.Error(
                    $"Script not found at '{relativePath}'. Use 'create' action to add a new script."
                );
            }
            if (string.IsNullOrEmpty(contents))
            {
                return Response.Error("Content is required for the 'update' action.");
            }

            // Validate syntax (basic check)
            if (!ValidateScriptSyntax(contents))
            {
                if (UnityMcp.EnableLog) Debug.LogWarning($"Potential syntax error in script being updated: {name}");
                // Consider if this should be a hard error or just a warning
            }

            try
            {
                File.WriteAllText(fullPath, contents);
                AssetDatabase.ImportAsset(relativePath); // Re-import to reflect changes
                AssetDatabase.Refresh();
                return Response.Success(
                    $"Script '{name}.cs' updated successfully at '{relativePath}'.",
                    new { path = relativePath }
                );
            }
            catch (Exception e)
            {
                return Response.Error($"Failed to update script '{relativePath}': {e.Message}");
            }
        }

        private object DeleteScript(string fullPath, string relativePath)
        {
            if (!File.Exists(fullPath))
            {
                return Response.Error($"Script not found at '{relativePath}'. Cannot delete.");
            }

            try
            {
                // Use AssetDatabase.MoveAssetToTrash for safer deletion (allows undo)
                bool deleted = AssetDatabase.MoveAssetToTrash(relativePath);
                if (deleted)
                {
                    AssetDatabase.Refresh();
                    return Response.Success(
                        $"Script '{Path.GetFileName(relativePath)}' moved to trash successfully."
                    );
                }
                else
                {
                    // Fallback or error if MoveAssetToTrash fails
                    return Response.Error(
                        $"Failed to move script '{relativePath}' to trash. It might be locked or in use."
                    );
                }
            }
            catch (Exception e)
            {
                return Response.Error($"Error deleting script '{relativePath}': {e.Message}");
            }
        }

        /// <summary>
        /// Generates basic C# script content based on name and type.
        /// </summary>
        private static string GenerateDefaultScriptContent(
            string name,
            string scriptType,
            string namespaceName
        )
        {
            string usingStatements = "using UnityEngine;\nusing System.Collections;\n";
            string classDeclaration;
            string body =
                "\n    // Use this for initialization\n    void Start() {\n\n    }\n\n    // Update is called once per frame\n    void Update() {\n\n    }\n";

            string baseClass = "";
            if (!string.IsNullOrEmpty(scriptType))
            {
                if (scriptType.Equals("MonoBehaviour", StringComparison.OrdinalIgnoreCase))
                    baseClass = " : MonoBehaviour";
                else if (scriptType.Equals("ScriptableObject", StringComparison.OrdinalIgnoreCase))
                {
                    baseClass = " : ScriptableObject";
                    body = ""; // ScriptableObjects don't usually need Start/Update
                }
                else if (
                    scriptType.Equals("Editor", StringComparison.OrdinalIgnoreCase)
                    || scriptType.Equals("EditorWindow", StringComparison.OrdinalIgnoreCase)
                )
                {
                    usingStatements += "using UnityEditor;\n";
                    if (scriptType.Equals("Editor", StringComparison.OrdinalIgnoreCase))
                        baseClass = " : Editor";
                    else
                        baseClass = " : EditorWindow";
                    body = ""; // Editor scripts have different structures
                }
                // Add more types as needed
            }

            classDeclaration = $"public class {name}{baseClass}";

            string fullContent = $"{usingStatements}\n";
            bool useNamespace = !string.IsNullOrEmpty(namespaceName);

            if (useNamespace)
            {
                fullContent += $"namespace {namespaceName}\n{{\n";
                // Indent class and body if using namespace
                classDeclaration = "    " + classDeclaration;
                body = string.Join("\n", body.Split('\n').Select(line => "    " + line));
            }

            fullContent += $"{classDeclaration}\n{{\n{body}\n}}";

            if (useNamespace)
            {
                fullContent += "\n}"; // Close namespace
            }

            return fullContent.Trim() + "\n"; // Ensure a trailing newline
        }

        /// <summary>
        /// Performs a very basic syntax validation (checks for balanced braces).
        /// TODO: Implement more robust syntax checking if possible.
        /// </summary>
        private static bool ValidateScriptSyntax(string contents)
        {
            if (string.IsNullOrEmpty(contents))
                return true; // Empty is technically valid?

            int braceBalance = 0;
            foreach (char c in contents)
            {
                if (c == '{')
                    braceBalance++;
                else if (c == '}')
                    braceBalance--;
            }

            return braceBalance == 0;
            // This is extremely basic. A real C# parser/compiler check would be ideal
            // but is complex to implement directly here.
        }


    }
}

