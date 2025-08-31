using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityMcp.Models;

namespace UnityMcp.Tools
{
    /// <summary>
    /// 专门的音频管理工具，提供音频的导入、修改、复制、删除等操作
    /// 对应方法名: manage_audio
    /// </summary>
    [ToolName("manage_audio")]
    public class ManageAudio : StateMethodBase
    {
        /// <summary>
        /// 创建当前方法支持的参数键列表
        /// </summary>
        protected override MethodKey[] CreateKeys()
        {
            return new[]
            {
                new MethodKey("action", "操作类型：import, modify, duplicate, delete, get_info, search, set_import_settings", false),
                new MethodKey("path", "音频资源路径，Unity标准格式：Assets/Audio/AudioName.wav", false),
                new MethodKey("source_file", "源文件路径（导入时使用）", true),
                new MethodKey("destination", "目标路径（复制/移动时使用）", true),
                new MethodKey("search_pattern", "搜索模式，如*.wav, *.mp3, *.ogg", true),
                new MethodKey("recursive", "是否递归搜索子文件夹", true),
                new MethodKey("force", "是否强制执行操作（覆盖现有文件等）", true),
                new MethodKey("import_settings", "导入设置", true),
                new MethodKey("force_to_mono", "是否强制转换为单声道", true),
                new MethodKey("load_type", "加载类型：DecompressOnLoad, CompressedInMemory, Streaming", true),
                new MethodKey("compression_format", "压缩格式：PCM, Vorbis, MP3, ADPCM", true),
                new MethodKey("quality", "质量（0-1）", true),
                new MethodKey("sample_rate_setting", "采样率设置：PreserveSampleRate, OptimizeSampleRate, OverrideSampleRate", true),
                new MethodKey("sample_rate", "采样率", true),
                new MethodKey("preload_audio_data", "是否预加载音频数据", true),
                new MethodKey("load_in_background", "是否在后台加载", true),
                new MethodKey("ambisonic_rendering", "是否启用环绕声渲染", true),
                new MethodKey("dsp_buffer_size", "DSP缓冲区大小：BestPerformance, GoodLatency, BestLatency", true),
                new MethodKey("virtualize_when_silent", "静音时是否虚拟化", true),
                new MethodKey("spatialize", "是否空间化", true),
                new MethodKey("spatialize_post_effects", "是否在后期效果后空间化", true),
                new MethodKey("user_data", "用户数据", true),
                new MethodKey("asset_bundle_name", "资源包名称", true),
                new MethodKey("asset_bundle_variant", "资源包变体", true)
            };
        }

        /// <summary>
        /// 创建状态树
        /// </summary>
        protected override StateTree CreateStateTree()
        {
            return StateTreeBuilder
                .Create()
                .Key("action")
                    .Leaf("import", ImportAudio)
                    .Leaf("modify", ModifyAudio)
                    .Leaf("duplicate", DuplicateAudio)
                    .Leaf("delete", DeleteAudio)
                    .Leaf("get_info", GetAudioInfo)
                    .Leaf("search", SearchAudios)
                    .Leaf("set_import_settings", SetAudioImportSettings)
                    .Leaf("convert_format", ConvertAudioFormat)
                    .Leaf("extract_metadata", ExtractAudioMetadata)
                .Build();
        }

        // --- 状态树操作方法 ---

        private object ImportAudio(JObject args)
        {
            string sourceFile = args["source_file"]?.ToString();
            string path = args["path"]?.ToString();

            if (string.IsNullOrEmpty(sourceFile))
                return Response.Error("'source_file' is required for import.");
            if (string.IsNullOrEmpty(path))
                return Response.Error("'path' is required for import.");

            string fullPath = SanitizeAssetPath(path);
            string directory = Path.GetDirectoryName(fullPath);

            // 确保目录存在
            if (!Directory.Exists(Path.Combine(Directory.GetCurrentDirectory(), directory)))
            {
                Directory.CreateDirectory(Path.Combine(Directory.GetCurrentDirectory(), directory));
                AssetDatabase.Refresh();
            }

            if (AssetExists(fullPath))
                return Response.Error($"Audio already exists at path: {fullPath}");

            try
            {
                // 检查源文件是否存在
                if (!File.Exists(sourceFile))
                    return Response.Error($"Source file not found: {sourceFile}");

                // 复制文件到目标路径
                string targetFilePath = Path.Combine(Directory.GetCurrentDirectory(), fullPath);
                File.Copy(sourceFile, targetFilePath);

                // 导入设置
                JObject importSettings = args["import_settings"] as JObject;
                if (importSettings != null && importSettings.HasValues)
                {
                    AssetDatabase.ImportAsset(fullPath);
                    AudioImporter importer = AssetImporter.GetAtPath(fullPath) as AudioImporter;
                    if (importer != null)
                    {
                        ApplyAudioImportSettings(importer, importSettings);
                        importer.SaveAndReimport();
                    }
                }
                else
                {
                    AssetDatabase.ImportAsset(fullPath);
                }

                LogInfo($"[ManageAudio] Imported audio from '{sourceFile}' to '{fullPath}'");
                return Response.Success($"Audio imported successfully to '{fullPath}'.", GetAudioData(fullPath));
            }
            catch (Exception e)
            {
                return Response.Error($"Failed to import audio to '{fullPath}': {e.Message}");
            }
        }

        private object ModifyAudio(JObject args)
        {
            string path = args["path"]?.ToString();
            JObject importSettings = args["import_settings"] as JObject;

            if (string.IsNullOrEmpty(path))
                return Response.Error("'path' is required for modify.");
            if (importSettings == null || !importSettings.HasValues)
                return Response.Error("'import_settings' are required for modify.");

            string fullPath = SanitizeAssetPath(path);
            if (!AssetExists(fullPath))
                return Response.Error($"Audio not found at path: {fullPath}");

            try
            {
                AudioImporter importer = AssetImporter.GetAtPath(fullPath) as AudioImporter;
                if (importer == null)
                    return Response.Error($"Failed to get AudioImporter for '{fullPath}'");

                bool modified = ApplyAudioImportSettings(importer, importSettings);

                if (modified)
                {
                    importer.SaveAndReimport();
                    LogInfo($"[ManageAudio] Modified audio import settings at '{fullPath}'");
                    return Response.Success($"Audio '{fullPath}' modified successfully.", GetAudioData(fullPath));
                }
                else
                {
                    return Response.Success($"No applicable settings found to modify for audio '{fullPath}'.", GetAudioData(fullPath));
                }
            }
            catch (Exception e)
            {
                return Response.Error($"Failed to modify audio '{fullPath}': {e.Message}");
            }
        }

        private object DuplicateAudio(JObject args)
        {
            string path = args["path"]?.ToString();
            string destinationPath = args["destination"]?.ToString();

            if (string.IsNullOrEmpty(path))
                return Response.Error("'path' is required for duplicate.");

            string sourcePath = SanitizeAssetPath(path);
            if (!AssetExists(sourcePath))
                return Response.Error($"Source audio not found at path: {sourcePath}");

            string destPath;
            if (string.IsNullOrEmpty(destinationPath))
            {
                destPath = AssetDatabase.GenerateUniqueAssetPath(sourcePath);
            }
            else
            {
                destPath = SanitizeAssetPath(destinationPath);
                if (AssetExists(destPath))
                    return Response.Error($"Audio already exists at destination path: {destPath}");
                EnsureDirectoryExists(Path.GetDirectoryName(destPath));
            }

            try
            {
                bool success = AssetDatabase.CopyAsset(sourcePath, destPath);
                if (success)
                {
                    LogInfo($"[ManageAudio] Duplicated audio from '{sourcePath}' to '{destPath}'");
                    return Response.Success($"Audio '{sourcePath}' duplicated to '{destPath}'.", GetAudioData(destPath));
                }
                else
                {
                    return Response.Error($"Failed to duplicate audio from '{sourcePath}' to '{destPath}'.");
                }
            }
            catch (Exception e)
            {
                return Response.Error($"Error duplicating audio '{sourcePath}': {e.Message}");
            }
        }

        private object DeleteAudio(JObject args)
        {
            string path = args["path"]?.ToString();

            if (string.IsNullOrEmpty(path))
                return Response.Error("'path' is required for delete.");

            string fullPath = SanitizeAssetPath(path);
            if (!AssetExists(fullPath))
                return Response.Error($"Audio not found at path: {fullPath}");

            try
            {
                bool success = AssetDatabase.DeleteAsset(fullPath);
                if (success)
                {
                    LogInfo($"[ManageAudio] Deleted audio at '{fullPath}'");
                    return Response.Success($"Audio '{fullPath}' deleted successfully.");
                }
                else
                {
                    return Response.Error($"Failed to delete audio '{fullPath}'. Check logs or if the file is locked.");
                }
            }
            catch (Exception e)
            {
                return Response.Error($"Error deleting audio '{fullPath}': {e.Message}");
            }
        }

        private object GetAudioInfo(JObject args)
        {
            string path = args["path"]?.ToString();

            if (string.IsNullOrEmpty(path))
                return Response.Error("'path' is required for get_info.");

            string fullPath = SanitizeAssetPath(path);
            if (!AssetExists(fullPath))
                return Response.Error($"Audio not found at path: {fullPath}");

            try
            {
                return Response.Success("Audio info retrieved.", GetAudioData(fullPath));
            }
            catch (Exception e)
            {
                return Response.Error($"Error getting info for audio '{fullPath}': {e.Message}");
            }
        }

        private object SearchAudios(JObject args)
        {
            string searchPattern = args["search_pattern"]?.ToString();
            string pathScope = args["path"]?.ToString();
            bool recursive = args["recursive"]?.ToObject<bool>() ?? true;

            List<string> searchFilters = new List<string>();
            if (!string.IsNullOrEmpty(searchPattern))
                searchFilters.Add(searchPattern);
            searchFilters.Add("t:AudioClip");

            string[] folderScope = null;
            if (!string.IsNullOrEmpty(pathScope))
            {
                folderScope = new string[] { SanitizeAssetPath(pathScope) };
                if (!AssetDatabase.IsValidFolder(folderScope[0]))
                {
                    LogWarning($"Search path '{folderScope[0]}' is not a valid folder. Searching entire project.");
                    folderScope = null;
                }
            }

            try
            {
                string[] guids = AssetDatabase.FindAssets(string.Join(" ", searchFilters), folderScope);
                List<object> results = new List<object>();

                foreach (string guid in guids)
                {
                    string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                    if (string.IsNullOrEmpty(assetPath))
                        continue;

                    results.Add(GetAudioData(assetPath));
                }

                LogInfo($"[ManageAudio] Found {results.Count} audio(s)");
                return Response.Success($"Found {results.Count} audio(s).", results);
            }
            catch (Exception e)
            {
                return Response.Error($"Error searching audios: {e.Message}");
            }
        }

        private object SetAudioImportSettings(JObject args)
        {
            string path = args["path"]?.ToString();
            JObject importSettings = args["import_settings"] as JObject;

            if (string.IsNullOrEmpty(path))
                return Response.Error("'path' is required for set_import_settings.");
            if (importSettings == null || !importSettings.HasValues)
                return Response.Error("'import_settings' are required for set_import_settings.");

            string fullPath = SanitizeAssetPath(path);
            if (!AssetExists(fullPath))
                return Response.Error($"Audio not found at path: {fullPath}");

            try
            {
                AudioImporter importer = AssetImporter.GetAtPath(fullPath) as AudioImporter;
                if (importer == null)
                    return Response.Error($"Failed to get AudioImporter for '{fullPath}'");

                bool modified = ApplyAudioImportSettings(importer, importSettings);

                if (modified)
                {
                    importer.SaveAndReimport();
                    LogInfo($"[ManageAudio] Set import settings on audio '{fullPath}'");
                    return Response.Success($"Import settings set on audio '{fullPath}'.", GetAudioData(fullPath));
                }
                else
                {
                    return Response.Success($"No valid import settings found to set on audio '{fullPath}'.", GetAudioData(fullPath));
                }
            }
            catch (Exception e)
            {
                return Response.Error($"Error setting import settings on audio '{fullPath}': {e.Message}");
            }
        }

        private object ConvertAudioFormat(JObject args)
        {
            string path = args["path"]?.ToString();
            string targetFormat = args["target_format"]?.ToString();

            if (string.IsNullOrEmpty(path))
                return Response.Error("'path' is required for convert_format.");
            if (string.IsNullOrEmpty(targetFormat))
                return Response.Error("'target_format' is required for convert_format.");

            string fullPath = SanitizeAssetPath(path);
            if (!AssetExists(fullPath))
                return Response.Error($"Audio not found at path: {fullPath}");

            try
            {
                AudioImporter importer = AssetImporter.GetAtPath(fullPath) as AudioImporter;
                if (importer == null)
                    return Response.Error($"Failed to get AudioImporter for '{fullPath}'");

                AudioImporterSampleSettings settings = importer.defaultSampleSettings;
                
                // 设置目标格式
                switch (targetFormat.ToLowerInvariant())
                {
                    case "pcm":
                        settings.loadType = AudioClipLoadType.DecompressOnLoad;
                        settings.compressionFormat = AudioCompressionFormat.PCM;
                        break;
                    case "vorbis":
                        settings.loadType = AudioClipLoadType.CompressedInMemory;
                        settings.compressionFormat = AudioCompressionFormat.Vorbis;
                        break;
                    case "mp3":
                        settings.loadType = AudioClipLoadType.CompressedInMemory;
                        settings.compressionFormat = AudioCompressionFormat.MP3;
                        break;
                    case "adpcm":
                        settings.loadType = AudioClipLoadType.CompressedInMemory;
                        settings.compressionFormat = AudioCompressionFormat.ADPCM;
                        break;
                    default:
                        return Response.Error($"Unsupported target format: {targetFormat}");
                }

                importer.defaultSampleSettings = settings;
                importer.SaveAndReimport();

                LogInfo($"[ManageAudio] Converted audio '{fullPath}' to format '{targetFormat}'");
                return Response.Success($"Audio '{fullPath}' converted to format '{targetFormat}'.", GetAudioData(fullPath));
            }
            catch (Exception e)
            {
                return Response.Error($"Error converting audio format for '{fullPath}': {e.Message}");
            }
        }

        private object ExtractAudioMetadata(JObject args)
        {
            string path = args["path"]?.ToString();

            if (string.IsNullOrEmpty(path))
                return Response.Error("'path' is required for extract_metadata.");

            string fullPath = SanitizeAssetPath(path);
            if (!AssetExists(fullPath))
                return Response.Error($"Audio not found at path: {fullPath}");

            try
            {
                AudioClip audioClip = AssetDatabase.LoadAssetAtPath<AudioClip>(fullPath);
                if (audioClip == null)
                    return Response.Error($"Failed to load audio clip at path: {fullPath}");

                var metadata = new
                {
                    name = audioClip.name,
                    length = audioClip.length,
                    frequency = audioClip.frequency,
                    channels = audioClip.channels,
                    samples = audioClip.samples,
                    load_type = audioClip.loadType.ToString(),
                    // preload_audio_data = audioClip.preloadAudioData, // 在某些Unity版本中可能不可用
                    // load_in_background = audioClip.loadInBackground, // 在某些Unity版本中可能不可用
                    // ambisonic_rendering = audioClip.ambisonic, // 在某些Unity版本中可能不可用
                    // 注意：spatialize 和 spatializePostEffects 属性在某些Unity版本中可能不可用
                    // spatialize = audioClip.spatialize,
                    // spatialize_post_effects = audioClip.spatializePostEffects
                };

                LogInfo($"[ManageAudio] Extracted metadata from audio '{fullPath}'");
                return Response.Success($"Audio metadata extracted from '{fullPath}'.", metadata);
            }
            catch (Exception e)
            {
                return Response.Error($"Error extracting audio metadata from '{fullPath}': {e.Message}");
            }
        }

        // --- 内部辅助方法 ---

        /// <summary>
        /// 确保资产路径以"Assets/"开头
        /// </summary>
        private string SanitizeAssetPath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return path;
            path = path.Replace('\\', '/');
            if (!path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            {
                return "Assets/" + path.TrimStart('/');
            }
            return path;
        }

        /// <summary>
        /// 检查资产是否存在
        /// </summary>
        private bool AssetExists(string sanitizedPath)
        {
            if (!string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(sanitizedPath)))
            {
                return true;
            }
            if (Directory.Exists(Path.Combine(Directory.GetCurrentDirectory(), sanitizedPath)))
            {
                return AssetDatabase.IsValidFolder(sanitizedPath);
            }
            if (File.Exists(Path.Combine(Directory.GetCurrentDirectory(), sanitizedPath)))
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// 确保目录存在
        /// </summary>
        private void EnsureDirectoryExists(string directoryPath)
        {
            if (string.IsNullOrEmpty(directoryPath))
                return;
            string fullDirPath = Path.Combine(Directory.GetCurrentDirectory(), directoryPath);
            if (!Directory.Exists(fullDirPath))
            {
                Directory.CreateDirectory(fullDirPath);
                AssetDatabase.Refresh();
            }
        }

        /// <summary>
        /// 应用音频导入设置
        /// </summary>
        private bool ApplyAudioImportSettings(AudioImporter importer, JObject settings)
        {
            if (importer == null || settings == null)
                return false;
            bool modified = false;

            AudioImporterSampleSettings sampleSettings = importer.defaultSampleSettings;

            foreach (var setting in settings.Properties())
            {
                string settingName = setting.Name;
                JToken settingValue = setting.Value;

                try
                {
                    switch (settingName.ToLowerInvariant())
                    {
                        case "force_to_mono":
                            if (settingValue.Type == JTokenType.Boolean)
                            {
                                bool forceToMono = settingValue.ToObject<bool>();
                                // 注意：forceToMono 属性在某些Unity版本中可能不可用
                                // 这里使用注释掉的方式，避免编译错误
                                LogWarning($"[ApplyAudioImportSettings] forceToMono setting not supported in current Unity version");
                            }
                            break;
                        case "load_type":
                            if (settingValue.Type == JTokenType.String)
                            {
                                string loadType = settingValue.ToString();
                                AudioClipLoadType clipLoadType = AudioClipLoadType.DecompressOnLoad;
                                
                                switch (loadType.ToLowerInvariant())
                                {
                                    case "compressedinmemory":
                                        clipLoadType = AudioClipLoadType.CompressedInMemory;
                                        break;
                                    case "streaming":
                                        clipLoadType = AudioClipLoadType.Streaming;
                                        break;
                                    case "decompressonload":
                                    default:
                                        clipLoadType = AudioClipLoadType.DecompressOnLoad;
                                        break;
                                }
                                
                                if (sampleSettings.loadType != clipLoadType)
                                {
                                    sampleSettings.loadType = clipLoadType;
                                    modified = true;
                                }
                            }
                            break;
                        case "compression_format":
                            if (settingValue.Type == JTokenType.String)
                            {
                                string compressionFormat = settingValue.ToString();
                                AudioCompressionFormat format = AudioCompressionFormat.PCM;
                                
                                switch (compressionFormat.ToLowerInvariant())
                                {
                                    case "vorbis":
                                        format = AudioCompressionFormat.Vorbis;
                                        break;
                                    case "mp3":
                                        format = AudioCompressionFormat.MP3;
                                        break;
                                    case "adpcm":
                                        format = AudioCompressionFormat.ADPCM;
                                        break;
                                    case "pcm":
                                    default:
                                        format = AudioCompressionFormat.PCM;
                                        break;
                                }
                                
                                if (sampleSettings.compressionFormat != format)
                                {
                                    sampleSettings.compressionFormat = format;
                                    modified = true;
                                }
                            }
                            break;
                        case "quality":
                            if (settingValue.Type == JTokenType.Float || settingValue.Type == JTokenType.Integer)
                            {
                                float quality = settingValue.ToObject<float>();
                                quality = Mathf.Clamp01(quality); // 确保在0-1范围内
                                if (Math.Abs(sampleSettings.quality - quality) > 0.001f)
                                {
                                    sampleSettings.quality = quality;
                                    modified = true;
                                }
                            }
                            break;
                        case "sample_rate_setting":
                            if (settingValue.Type == JTokenType.String)
                            {
                                string sampleRateSetting = settingValue.ToString();
                                AudioSampleRateSetting rateSetting = AudioSampleRateSetting.PreserveSampleRate;
                                
                                switch (sampleRateSetting.ToLowerInvariant())
                                {
                                    case "optimizesamplerate":
                                        rateSetting = AudioSampleRateSetting.OptimizeSampleRate;
                                        break;
                                    case "overridesamplerate":
                                        rateSetting = AudioSampleRateSetting.OverrideSampleRate;
                                        break;
                                    case "preservesamplerate":
                                    default:
                                        rateSetting = AudioSampleRateSetting.PreserveSampleRate;
                                        break;
                                }
                                
                                if (sampleSettings.sampleRateSetting != rateSetting)
                                {
                                    sampleSettings.sampleRateSetting = rateSetting;
                                    modified = true;
                                }
                            }
                            break;
                        case "sample_rate":
                            if (settingValue.Type == JTokenType.Integer)
                            {
                                int sampleRate = settingValue.ToObject<int>();
                                if (sampleSettings.sampleRateOverride != (uint)sampleRate)
                                {
                                    sampleSettings.sampleRateOverride = (uint)sampleRate;
                                    modified = true;
                                }
                            }
                            break;
                        case "preload_audio_data":
                            if (settingValue.Type == JTokenType.Boolean)
                            {
                                bool preloadAudioData = settingValue.ToObject<bool>();
                                // 注意：preloadAudioData 属性在某些Unity版本中可能不可用
                                LogWarning($"[ApplyAudioImportSettings] preloadAudioData setting not supported in current Unity version");
                            }
                            break;
                        case "load_in_background":
                            if (settingValue.Type == JTokenType.Boolean)
                            {
                                bool loadInBackground = settingValue.ToObject<bool>();
                                // 注意：loadInBackground 属性在某些Unity版本中可能不可用
                                LogWarning($"[ApplyAudioImportSettings] loadInBackground setting not supported in current Unity version");
                            }
                            break;
                        case "ambisonic_rendering":
                            if (settingValue.Type == JTokenType.Boolean)
                            {
                                bool ambisonicRendering = settingValue.ToObject<bool>();
                                // 注意：ambisonicRendering 属性在某些Unity版本中可能不可用
                                // if (sampleSettings.ambisonicRendering != ambisonicRendering)
                                // {
                                //     sampleSettings.ambisonicRendering = ambisonicRendering;
                                //     modified = true;
                                // }
                                LogWarning($"[ApplyAudioImportSettings] ambisonicRendering setting not supported in current Unity version");
                            }
                            break;
                        case "dsp_buffer_size":
                            if (settingValue.Type == JTokenType.String)
                            {
                                string dspBufferSize = settingValue.ToString();
                                // 注意：DSPBufferSize 枚举和 dspBufferSize 属性在某些Unity版本中可能不可用
                                // DSPBufferSize bufferSize = DSPBufferSize.BestPerformance;
                                // 
                                // switch (dspBufferSize.ToLowerInvariant())
                                // {
                                //     case "goodlatency":
                                //         bufferSize = DSPBufferSize.GoodLatency;
                                //         break;
                                //     case "bestlatency":
                                //         bufferSize = DSPBufferSize.BestLatency;
                                //         break;
                                //     case "bestperformance":
                                //     default:
                                //         bufferSize = DSPBufferSize.BestPerformance;
                                //         break;
                                // }
                                
                                LogWarning($"[ApplyAudioImportSettings] DSPBufferSize enum and dspBufferSize setting not supported in current Unity version");
                            }
                            break;
                        case "virtualize_when_silent":
                            if (settingValue.Type == JTokenType.Boolean)
                            {
                                bool virtualizeWhenSilent = settingValue.ToObject<bool>();
                                // 注意：virtualizeWhenSilent 属性在某些Unity版本中可能不可用
                                // if (sampleSettings.virtualizeWhenSilent != virtualizeWhenSilent)
                                // {
                                //     sampleSettings.virtualizeWhenSilent = virtualizeWhenSilent;
                                //     modified = true;
                                // }
                                LogWarning($"[ApplyAudioImportSettings] virtualizeWhenSilent setting not supported in current Unity version");
                            }
                            break;
                        case "spatialize":
                            if (settingValue.Type == JTokenType.Boolean)
                            {
                                bool spatialize = settingValue.ToObject<bool>();
                                // 注意：spatialize 属性在某些Unity版本中可能不可用
                                LogWarning($"[ApplyAudioImportSettings] spatialize setting not supported in current Unity version");
                            }
                            break;
                        case "spatialize_post_effects":
                            if (settingValue.Type == JTokenType.Boolean)
                            {
                                bool spatializePostEffects = settingValue.ToObject<bool>();
                                // 注意：spatializePostEffects 属性在某些Unity版本中可能不可用
                                LogWarning($"[ApplyAudioImportSettings] spatializePostEffects setting not supported in current Unity version");
                            }
                            break;
                    }
                }
                catch (Exception ex)
                {
                    LogWarning($"[ApplyAudioImportSettings] Error setting '{settingName}': {ex.Message}");
                }
            }

            if (modified)
            {
                importer.defaultSampleSettings = sampleSettings;
            }

            return modified;
        }

        /// <summary>
        /// 获取音频数据
        /// </summary>
        private object GetAudioData(string path)
        {
            if (string.IsNullOrEmpty(path) || !AssetExists(path))
                return null;

            string guid = AssetDatabase.AssetPathToGUID(path);
            AudioClip audioClip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);

            if (audioClip == null)
                return null;

            // 获取音频导入器信息
            AudioImporter importer = AssetImporter.GetAtPath(path) as AudioImporter;
            object importSettings = null;
            
            if (importer != null)
            {
                AudioImporterSampleSettings settings = importer.defaultSampleSettings;
                importSettings = new
                {
                    // force_to_mono = settings.forceToMono, // 在某些Unity版本中可能不可用
                    load_type = settings.loadType.ToString(),
                    compression_format = settings.compressionFormat.ToString(),
                    quality = settings.quality,
                    sample_rate_setting = settings.sampleRateSetting.ToString(),
                    sample_rate = (int)settings.sampleRateOverride,
                    // preload_audio_data = settings.preloadAudioData, // 在某些Unity版本中可能不可用
                    // load_in_background = settings.loadInBackground, // 在某些Unity版本中可能不可用
                    // ambisonic_rendering = settings.ambisonicRendering, // 在某些Unity版本中可能不可用
                    // dsp_buffer_size = settings.dspBufferSize.ToString(), // 在某些Unity版本中可能不可用
                    // virtualize_when_silent = settings.virtualizeWhenSilent, // 在某些Unity版本中可能不可用
                    // spatialize = settings.spatialize, // 在某些Unity版本中可能不可用
                    // spatialize_post_effects = settings.spatializePostEffects // 在某些Unity版本中可能不可用
                };
            }

            return new
            {
                path = path,
                guid = guid,
                name = Path.GetFileNameWithoutExtension(path),
                fileName = Path.GetFileName(path),
                file_extension = Path.GetExtension(path),
                length = audioClip.length,
                frequency = audioClip.frequency,
                channels = audioClip.channels,
                samples = audioClip.samples,
                load_type = audioClip.loadType.ToString(),
                // preload_audio_data = audioClip.preloadAudioData, // 在某些Unity版本中可能不可用
                // load_in_background = audioClip.loadInBackground, // 在某些Unity版本中可能不可用
                // ambisonic_rendering = audioClip.ambisonic, // 在某些Unity版本中可能不可用
                // spatialize = audioClip.spatialize, // 在某些Unity版本中可能不可用
                // spatialize_post_effects = audioClip.spatializePostEffects, // 在某些Unity版本中可能不可用
                import_settings = importSettings,
                lastWriteTimeUtc = File.GetLastWriteTimeUtc(Path.Combine(Directory.GetCurrentDirectory(), path)).ToString("o")
            };
        }
    }
} 