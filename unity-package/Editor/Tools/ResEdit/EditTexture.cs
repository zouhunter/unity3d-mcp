using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityMcp.Models; // For Response class

namespace UnityMcp.Tools
{
    /// <summary>
    /// Handles texture import settings and modification operations.
    /// 对应方法名: manage_texture
    /// </summary>
    [ToolName("edit_texture")]
    public class EditTexture : StateMethodBase
    {
        /// <summary>
        /// 创建当前方法支持的参数键列表
        /// </summary>
        protected override MethodKey[] CreateKeys()
        {
            return new[]
            {
                new MethodKey("action", "操作类型：set_type, set_sprite_settings, get_settings", false),
                new MethodKey("texture_path", "纹理资源路径", false),
                new MethodKey("texture_type", "纹理类型：Default, NormalMap, EditorGUIAndLegacy, Sprite, Cursor, Cookie, Lightmap, HDR", true),
                new MethodKey("sprite_mode", "Sprite模式：Single, Multiple, Polygon", true),
                new MethodKey("pixels_per_unit", "每单位像素数", true),
                new MethodKey("sprite_pivot", "Sprite轴心：Center, TopLeft, TopCenter, TopRight, MiddleLeft, MiddleCenter, MiddleRight, BottomLeft, BottomCenter, BottomRight, Custom", true),
                new MethodKey("generate_physics_shape", "生成物理形状", true),
                new MethodKey("mesh_type", "网格类型：FullRect, Tight", true),
                new MethodKey("extrude_edges", "边缘挤出", true),
                new MethodKey("compression", "压缩格式：Uncompressed, LowQuality, NormalQuality, HighQuality", true),
                new MethodKey("max_texture_size", "最大纹理尺寸：32, 64, 128, 256, 512, 1024, 2048, 4096, 8192", true),
                new MethodKey("filter_mode", "过滤模式：Point, Bilinear, Trilinear", true),
                new MethodKey("wrap_mode", "包装模式：Repeat, Clamp, Mirror, MirrorOnce", true),
                new MethodKey("readable", "可读写", true),
                new MethodKey("generate_mip_maps", "生成Mip贴图", true),
                new MethodKey("srgb_texture", "sRGB纹理", true)
            };
        }

        protected override StateTree CreateStateTree()
        {
            return StateTreeBuilder
                .Create()
                .Key("action")
                    .Leaf("set_type", HandleSetTypeAction)
                    .Leaf("set_sprite_settings", HandleSetSpriteSettingsAction)
                    .Leaf("get_settings", HandleGetSettingsAction)
                .Build();
        }

        // --- State Tree Action Handlers ---

        /// <summary>
        /// 处理设置纹理类型的操作
        /// </summary>
        private object HandleSetTypeAction(JObject args)
        {
            string texturePath = args["texture_path"]?.ToString();
            string textureType = args["texture_type"]?.ToString();

            if (string.IsNullOrEmpty(texturePath))
            {
                return Response.Error("'texture_path' parameter is required.");
            }

            if (string.IsNullOrEmpty(textureType))
            {
                return Response.Error("'texture_type' parameter is required.");
            }

            LogInfo($"[ManageTexture] Setting texture type to '{textureType}' for '{texturePath}'");
            return SetTextureType(texturePath, textureType);
        }

        /// <summary>
        /// 处理设置Sprite设置的操作
        /// </summary>
        private object HandleSetSpriteSettingsAction(JObject args)
        {
            string texturePath = args["texture_path"]?.ToString();

            if (string.IsNullOrEmpty(texturePath))
            {
                return Response.Error("'texture_path' parameter is required.");
            }

            LogInfo($"[ManageTexture] Setting sprite settings for '{texturePath}'");
            return SetSpriteSettings(args, texturePath);
        }

        /// <summary>
        /// 处理获取纹理设置的操作
        /// </summary>
        private object HandleGetSettingsAction(JObject args)
        {
            string texturePath = args["texture_path"]?.ToString();

            if (string.IsNullOrEmpty(texturePath))
            {
                return Response.Error("'texture_path' parameter is required.");
            }

            LogInfo($"[ManageTexture] Getting texture settings for '{texturePath}'");
            return GetTextureSettings(texturePath);
        }

        // --- Core Methods ---

        /// <summary>
        /// 设置纹理类型
        /// </summary>
        private object SetTextureType(string texturePath, string textureType)
        {
            try
            {
                // 获取纹理导入器
                TextureImporter textureImporter = GetTextureImporter(texturePath);
                if (textureImporter == null)
                {
                    return Response.Error($"Could not get TextureImporter for '{texturePath}'. Make sure the path is correct and points to a texture.");
                }

                // 解析纹理类型
                if (!Enum.TryParse<TextureImporterType>(textureType, true, out TextureImporterType type))
                {
                    return Response.Error($"Invalid texture type '{textureType}'. Valid types: {string.Join(", ", Enum.GetNames(typeof(TextureImporterType)))}");
                }

                // 设置纹理类型
                textureImporter.textureType = type;

                // 如果设置为Sprite，自动配置常用的Sprite设置
                if (type == TextureImporterType.Sprite)
                {
                    textureImporter.spriteImportMode = SpriteImportMode.Single;
                    textureImporter.spritePixelsPerUnit = 100f;
                    textureImporter.spritePivot = Vector2.one * 0.5f; // Center
                }

                // 应用设置并重新导入
                EditorUtility.SetDirty(textureImporter);
                textureImporter.SaveAndReimport();

                LogInfo($"[ManageTexture] Successfully set texture type to '{textureType}' for '{texturePath}'");
                return Response.Success($"Texture type set to '{textureType}' for '{texturePath}'.");
            }
            catch (Exception e)
            {
                LogInfo($"[ManageTexture] Error setting texture type: {e.Message}");
                return Response.Error($"Error setting texture type: {e.Message}");
            }
        }

        /// <summary>
        /// 设置Sprite设置
        /// </summary>
        private object SetSpriteSettings(JObject args, string texturePath)
        {
            try
            {
                // 获取纹理导入器
                TextureImporter textureImporter = GetTextureImporter(texturePath);
                if (textureImporter == null)
                {
                    return Response.Error($"Could not get TextureImporter for '{texturePath}'. Make sure the path is correct and points to a texture.");
                }

                // 确保是Sprite类型
                if (textureImporter.textureType != TextureImporterType.Sprite)
                {
                    textureImporter.textureType = TextureImporterType.Sprite;
                }

                // 设置Sprite模式
                string spriteMode = args["sprite_mode"]?.ToString();
                if (!string.IsNullOrEmpty(spriteMode))
                {
                    if (Enum.TryParse<SpriteImportMode>(spriteMode, true, out SpriteImportMode mode))
                    {
                        textureImporter.spriteImportMode = mode;
                    }
                }

                // 设置每单位像素数
                float pixelsPerUnit = args["pixels_per_unit"]?.ToObject<float>() ?? 100f;
                textureImporter.spritePixelsPerUnit = pixelsPerUnit;

                // 设置轴心点
                string spritePivot = args["sprite_pivot"]?.ToString();
                if (!string.IsNullOrEmpty(spritePivot))
                {
                    Vector2 pivot = GetPivotVector(spritePivot);
                    textureImporter.spritePivot = pivot;
                }

                // 注意：某些高级Sprite设置可能在不同Unity版本中不可用
                // 这些设置通常通过Unity Editor UI手动配置

                // 设置压缩和质量
                SetTextureCompressionSettings(textureImporter, args);

                // 设置其他通用设置
                SetGeneralTextureSettings(textureImporter, args);

                // 应用设置并重新导入
                EditorUtility.SetDirty(textureImporter);
                textureImporter.SaveAndReimport();

                LogInfo($"[ManageTexture] Successfully applied sprite settings to '{texturePath}'");
                return Response.Success($"Sprite settings applied to '{texturePath}'.");
            }
            catch (Exception e)
            {
                LogInfo($"[ManageTexture] Error setting sprite settings: {e.Message}");
                return Response.Error($"Error setting sprite settings: {e.Message}");
            }
        }

        /// <summary>
        /// 获取纹理设置
        /// </summary>
        private object GetTextureSettings(string texturePath)
        {
            try
            {
                TextureImporter textureImporter = GetTextureImporter(texturePath);
                if (textureImporter == null)
                {
                    return Response.Error($"Could not get TextureImporter for '{texturePath}'. Make sure the path is correct and points to a texture.");
                }

                var settings = new
                {
                    textureType = textureImporter.textureType.ToString(),
                    spriteImportMode = textureImporter.spriteImportMode.ToString(),
                    spritePixelsPerUnit = textureImporter.spritePixelsPerUnit,
                    spritePivot = textureImporter.spritePivot,
                    maxTextureSize = textureImporter.maxTextureSize,
                    textureCompression = textureImporter.textureCompression.ToString(),
                    filterMode = textureImporter.filterMode.ToString(),
                    wrapMode = textureImporter.wrapMode.ToString(),
                    isReadable = textureImporter.isReadable,
                    mipmapEnabled = textureImporter.mipmapEnabled,
                    sRGBTexture = textureImporter.sRGBTexture
                };

                return Response.Success($"Retrieved texture settings for '{texturePath}'.", settings);
            }
            catch (Exception e)
            {
                LogInfo($"[ManageTexture] Error getting texture settings: {e.Message}");
                return Response.Error($"Error getting texture settings: {e.Message}");
            }
        }

        // --- Helper Methods ---

        /// <summary>
        /// 获取纹理导入器
        /// </summary>
        private TextureImporter GetTextureImporter(string texturePath)
        {
            try
            {
                return AssetImporter.GetAtPath(texturePath) as TextureImporter;
            }
            catch (Exception e)
            {
                LogInfo($"[ManageTexture] Error getting TextureImporter: {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// 获取轴心点向量
        /// </summary>
        private Vector2 GetPivotVector(string pivotName)
        {
            switch (pivotName.ToLower())
            {
                case "center":
                case "middlecenter":
                    return new Vector2(0.5f, 0.5f);
                case "topleft":
                    return new Vector2(0f, 1f);
                case "topcenter":
                    return new Vector2(0.5f, 1f);
                case "topright":
                    return new Vector2(1f, 1f);
                case "middleleft":
                    return new Vector2(0f, 0.5f);
                case "middleright":
                    return new Vector2(1f, 0.5f);
                case "bottomleft":
                    return new Vector2(0f, 0f);
                case "bottomcenter":
                    return new Vector2(0.5f, 0f);
                case "bottomright":
                    return new Vector2(1f, 0f);
                default:
                    return new Vector2(0.5f, 0.5f); // Default to center
            }
        }

        /// <summary>
        /// 设置纹理压缩设置
        /// </summary>
        private void SetTextureCompressionSettings(TextureImporter textureImporter, JObject args)
        {
            // 设置压缩格式
            string compression = args["compression"]?.ToString();
            if (!string.IsNullOrEmpty(compression))
            {
                switch (compression.ToLower())
                {
                    case "uncompressed":
                        textureImporter.textureCompression = TextureImporterCompression.Uncompressed;
                        break;
                    case "lowquality":
                        textureImporter.textureCompression = TextureImporterCompression.CompressedLQ;
                        break;
                    case "normalquality":
                        textureImporter.textureCompression = TextureImporterCompression.Compressed;
                        break;
                    case "highquality":
                        textureImporter.textureCompression = TextureImporterCompression.CompressedHQ;
                        break;
                }
            }

            // 设置最大纹理尺寸
            int maxSize = args["max_texture_size"]?.ToObject<int>() ?? 2048;
            textureImporter.maxTextureSize = maxSize;
        }

        /// <summary>
        /// 设置通用纹理设置
        /// </summary>
        private void SetGeneralTextureSettings(TextureImporter textureImporter, JObject args)
        {
            // 设置过滤模式
            string filterMode = args["filter_mode"]?.ToString();
            if (!string.IsNullOrEmpty(filterMode))
            {
                if (Enum.TryParse<FilterMode>(filterMode, true, out FilterMode mode))
                {
                    textureImporter.filterMode = mode;
                }
            }

            // 设置包装模式
            string wrapMode = args["wrap_mode"]?.ToString();
            if (!string.IsNullOrEmpty(wrapMode))
            {
                if (Enum.TryParse<TextureWrapMode>(wrapMode, true, out TextureWrapMode mode))
                {
                    textureImporter.wrapMode = mode;
                }
            }

            // 设置是否可读
            bool readable = args["readable"]?.ToObject<bool>() ?? false;
            textureImporter.isReadable = readable;

            // 设置是否生成Mip贴图
            bool generateMipMaps = args["generate_mip_maps"]?.ToObject<bool>() ?? true;
            textureImporter.mipmapEnabled = generateMipMaps;

            // 设置sRGB纹理
            bool srgbTexture = args["srgb_texture"]?.ToObject<bool>() ?? true;
            textureImporter.sRGBTexture = srgbTexture;
        }
    }
}
