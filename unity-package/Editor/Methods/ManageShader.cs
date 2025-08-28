using System;
using System.IO;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityMcp.Models;
using UnityMcp;

namespace UnityMcp.Tools
{
    /// <summary>
    /// Handles CRUD operations for shader files within the Unity project.
    /// 对应方法名: manage_shader
    /// </summary>
    [ToolName("manage_shader")]
    public class ManageShader : StateMethodBase
    {
        /// <summary>
        /// 创建当前方法支持的参数键列表
        /// </summary>
        protected override MethodKey[] CreateKeys()
        {
            return new[]
            {
                new MethodKey("action", "操作类型：create, read, update, delete", false),
                new MethodKey("name", "Shader名称（不含.shader扩展名）", false),
                new MethodKey("path", "资产路径（相对于Assets），默认为Shaders", true),
                new MethodKey("contents", "Shader代码内容", true),
                new MethodKey("contentsEncoded", "内容是否为base64编码，默认false", true),
                new MethodKey("encodedContents", "base64编码的Shader内容", true)
            };
        }

        protected override StateTree CreateStateTree()
        {
            return StateTreeBuilder
                .Create()
                .Key("action")
                    .Leaf("create", HandleCreateShader)
                    .Leaf("read", HandleReadShader)
                    .Leaf("update", HandleUpdateShader)
                    .Leaf("delete", HandleDeleteShader)
                .Build();
        }

        /// <summary>
        /// 处理创建Shader操作
        /// </summary>
        private object HandleCreateShader(JObject args)
        {
            LogInfo("[ManageShader] Creating shader");
            var validationResult = ValidateParameters(args);
            if (!validationResult.IsValid)
            {
                return Response.Error(validationResult.ErrorMessage);
            }

            var pathInfo = GetPathInfo(args);
            string contents = GetShaderContents(args);

            // Check if shader already exists
            if (File.Exists(pathInfo.FullPath))
            {
                return Response.Error(
                    $"Shader already exists at '{pathInfo.RelativePath}'. Use 'update' action to modify."
                );
            }

            // Add validation for shader name conflicts in Unity
            if (Shader.Find(validationResult.Name) != null)
            {
                return Response.Error(
                    $"A shader with name '{validationResult.Name}' already exists in the project. Choose a different name."
                );
            }

            // Generate default content if none provided
            if (string.IsNullOrEmpty(contents))
            {
                contents = GenerateDefaultShaderContent(validationResult.Name);
            }

            return CreateShaderFile(pathInfo, validationResult.Name, contents);
        }

        /// <summary>
        /// 处理读取Shader操作
        /// </summary>
        private object HandleReadShader(JObject args)
        {
            LogInfo("[ManageShader] Reading shader");
            var validationResult = ValidateParameters(args);
            if (!validationResult.IsValid)
            {
                return Response.Error(validationResult.ErrorMessage);
            }

            var pathInfo = GetPathInfo(args);

            if (!File.Exists(pathInfo.FullPath))
            {
                return Response.Error($"Shader not found at '{pathInfo.RelativePath}'.");
            }

            try
            {
                string contents = File.ReadAllText(pathInfo.FullPath);

                // Return both normal and encoded contents for larger files
                bool isLarge = contents.Length > 10000; // If content is large, include encoded version
                var responseData = new
                {
                    path = pathInfo.RelativePath,
                    name = validationResult.Name,
                    contents = contents,
                    // For large files, also include base64-encoded version
                    encodedContents = isLarge ? EncodeBase64(contents) : null,
                    contentsEncoded = isLarge,
                    fileSize = contents.Length
                };

                return Response.Success(
                    $"Shader '{Path.GetFileName(pathInfo.RelativePath)}' read successfully.",
                    responseData
                );
            }
            catch (Exception e)
            {
                LogError($"[ManageShader] Failed to read shader: {e.Message}");
                return Response.Error($"Failed to read shader '{pathInfo.RelativePath}': {e.Message}");
            }
        }

        /// <summary>
        /// 处理更新Shader操作
        /// </summary>
        private object HandleUpdateShader(JObject args)
        {
            LogInfo("[ManageShader] Updating shader");
            var validationResult = ValidateParameters(args);
            if (!validationResult.IsValid)
            {
                return Response.Error(validationResult.ErrorMessage);
            }

            var pathInfo = GetPathInfo(args);
            string contents = GetShaderContents(args);

            if (!File.Exists(pathInfo.FullPath))
            {
                return Response.Error(
                    $"Shader not found at '{pathInfo.RelativePath}'. Use 'create' action to add a new shader."
                );
            }

            if (string.IsNullOrEmpty(contents))
            {
                return Response.Error("Content is required for the 'update' action.");
            }

            return UpdateShaderFile(pathInfo, validationResult.Name, contents);
        }

        /// <summary>
        /// 处理删除Shader操作
        /// </summary>
        private object HandleDeleteShader(JObject args)
        {
            LogInfo("[ManageShader] Deleting shader");
            var validationResult = ValidateParameters(args);
            if (!validationResult.IsValid)
            {
                return Response.Error(validationResult.ErrorMessage);
            }

            var pathInfo = GetPathInfo(args);

            if (!File.Exists(pathInfo.FullPath))
            {
                return Response.Error($"Shader not found at '{pathInfo.RelativePath}'.");
            }

            try
            {
                // Delete the asset through Unity's AssetDatabase first
                bool success = AssetDatabase.DeleteAsset(pathInfo.RelativePath);
                if (!success)
                {
                    return Response.Error($"Failed to delete shader through Unity's AssetDatabase: '{pathInfo.RelativePath}'");
                }

                // If the file still exists (rare case), try direct deletion
                if (File.Exists(pathInfo.FullPath))
                {
                    File.Delete(pathInfo.FullPath);
                }

                return Response.Success($"Shader '{Path.GetFileName(pathInfo.RelativePath)}' deleted successfully.");
            }
            catch (Exception e)
            {
                LogError($"[ManageShader] Failed to delete shader: {e.Message}");
                return Response.Error($"Failed to delete shader '{pathInfo.RelativePath}': {e.Message}");
            }
        }

        // --- Helper Methods ---

        /// <summary>
        /// 验证参数的结果
        /// </summary>
        private class ValidationResult
        {
            public bool IsValid { get; set; }
            public string ErrorMessage { get; set; }
            public string Name { get; set; }
        }

        /// <summary>
        /// 路径信息
        /// </summary>
        private class PathInfo
        {
            public string FullPath { get; set; }
            public string RelativePath { get; set; }
            public string FullPathDir { get; set; }
        }

        /// <summary>
        /// 验证基础参数
        /// </summary>
        private ValidationResult ValidateParameters(JObject args)
        {
            string name = args["name"]?.ToString();

            if (string.IsNullOrEmpty(name))
            {
                return new ValidationResult
                {
                    IsValid = false,
                    ErrorMessage = "Name parameter is required."
                };
            }

            // Basic name validation (alphanumeric, underscores, cannot start with number)
            if (!Regex.IsMatch(name, @"^[a-zA-Z_][a-zA-Z0-9_]*$"))
            {
                return new ValidationResult
                {
                    IsValid = false,
                    ErrorMessage = $"Invalid shader name: '{name}'. Use only letters, numbers, underscores, and don't start with a number."
                };
            }

            return new ValidationResult
            {
                IsValid = true,
                Name = name
            };
        }

        /// <summary>
        /// 获取路径信息
        /// </summary>
        private PathInfo GetPathInfo(JObject args)
        {
            string name = args["name"]?.ToString();
            string path = args["path"]?.ToString();

            // Ensure path is relative to Assets/, removing any leading "Assets/"
            // Set default directory to "Shaders" if path is not provided
            string relativeDir = path ?? "Shaders"; // Default to "Shaders" if path is null
            if (!string.IsNullOrEmpty(relativeDir))
            {
                relativeDir = relativeDir.Replace('\\', '/').Trim('/');
                if (relativeDir.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
                {
                    relativeDir = relativeDir.Substring("Assets/".Length).TrimStart('/');
                }
            }
            // Handle empty string case explicitly after processing
            if (string.IsNullOrEmpty(relativeDir))
            {
                relativeDir = "Shaders"; // Ensure default if path was provided as "" or only "/" or "Assets/"
            }

            // Construct paths
            string shaderFileName = $"{name}.shader";
            string fullPathDir = Path.Combine(Application.dataPath, relativeDir);
            string fullPath = Path.Combine(fullPathDir, shaderFileName);
            string relativePath = Path.Combine("Assets", relativeDir, shaderFileName).Replace('\\', '/');

            return new PathInfo
            {
                FullPath = fullPath,
                RelativePath = relativePath,
                FullPathDir = fullPathDir
            };
        }

        /// <summary>
        /// 获取Shader内容
        /// </summary>
        private string GetShaderContents(JObject args)
        {
            // Check if we have base64 encoded contents
            bool contentsEncoded = args["contentsEncoded"]?.ToObject<bool>() ?? false;
            if (contentsEncoded && args["encodedContents"] != null)
            {
                try
                {
                    return DecodeBase64(args["encodedContents"].ToString());
                }
                catch (Exception e)
                {
                    LogError($"[ManageShader] Failed to decode shader contents: {e.Message}");
                    return null;
                }
            }
            else
            {
                return args["contents"]?.ToString();
            }
        }

        /// <summary>
        /// 创建Shader文件
        /// </summary>
        private object CreateShaderFile(PathInfo pathInfo, string name, string contents)
        {
            try
            {
                // Ensure the target directory exists
                if (!Directory.Exists(pathInfo.FullPathDir))
                {
                    Directory.CreateDirectory(pathInfo.FullPathDir);
                    // Refresh AssetDatabase to recognize new folders
                    AssetDatabase.Refresh();
                }

                File.WriteAllText(pathInfo.FullPath, contents, new System.Text.UTF8Encoding(false));
                AssetDatabase.ImportAsset(pathInfo.RelativePath);
                AssetDatabase.Refresh(); // Ensure Unity recognizes the new shader

                return Response.Success(
                    $"Shader '{name}.shader' created successfully at '{pathInfo.RelativePath}'.",
                    new
                    {
                        path = pathInfo.RelativePath,
                        name = name,
                        fileSize = contents.Length
                    }
                );
            }
            catch (Exception e)
            {
                LogError($"[ManageShader] Failed to create shader: {e.Message}");
                return Response.Error($"Failed to create shader '{pathInfo.RelativePath}': {e.Message}");
            }
        }

        /// <summary>
        /// 更新Shader文件
        /// </summary>
        private object UpdateShaderFile(PathInfo pathInfo, string name, string contents)
        {
            try
            {
                File.WriteAllText(pathInfo.FullPath, contents, new System.Text.UTF8Encoding(false));
                AssetDatabase.ImportAsset(pathInfo.RelativePath);
                AssetDatabase.Refresh();

                return Response.Success(
                    $"Shader '{Path.GetFileName(pathInfo.RelativePath)}' updated successfully.",
                    new
                    {
                        path = pathInfo.RelativePath,
                        name = name,
                        fileSize = contents.Length
                    }
                );
            }
            catch (Exception e)
            {
                LogError($"[ManageShader] Failed to update shader: {e.Message}");
                return Response.Error($"Failed to update shader '{pathInfo.RelativePath}': {e.Message}");
            }
        }

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

        /// <summary>
        /// Generate default shader content
        /// </summary>
        private static string GenerateDefaultShaderContent(string name)
        {
            return @"Shader """ + name + @"""
{
    Properties
    {
        _MainTex (""Texture"", 2D) = ""white"" {}
    }
    SubShader
    {
        Tags { ""RenderType""=""Opaque"" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include ""UnityCG.cginc""

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.uv);
                return col;
            }
            ENDCG
        }
    }
}";
        }
    }
}
