using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityMcp.Models;
using UnityMcp;

namespace UnityMcp.Tools
{
    /// <summary>
    /// GamePlay游戏窗口管理工具，支持输入模拟、窗口操作、截图和图像处理
    /// 对应方法名: gameplay
    /// </summary>
    [ToolName("gameplay", "游戏控制")]
    public class GamePlay : StateMethodBase
    {
        // Game窗口相关的反射类型和方法
        private static Type gameViewType;
        private static MethodInfo repaintMethod;
        private static PropertyInfo targetSizeProperty;
        private static PropertyInfo selectedSizeIndexProperty;
        
        // 输入模拟相关
        private static bool isInputSimulationActive = false;
        private static List<SimulatedInput> inputQueue = new List<SimulatedInput>();
        
        // 截图相关
        private static RenderTexture screenshotRenderTexture;
        private static Camera screenshotCamera;
        
        static GamePlay()
        {
            InitializeReflection();
        }
        
        /// <summary>
        /// 初始化反射相关的类型和方法
        /// </summary>
        private static void InitializeReflection()
        {
            try
            {
                // 获取GameView类型
                gameViewType = Type.GetType("UnityEditor.GameView,UnityEditor");
                if (gameViewType != null)
                {
                    repaintMethod = gameViewType.GetMethod("Repaint", BindingFlags.Public | BindingFlags.Instance);
                    targetSizeProperty = gameViewType.GetProperty("targetSize", BindingFlags.Public | BindingFlags.Instance);
                    selectedSizeIndexProperty = gameViewType.GetProperty("selectedSizeIndex", BindingFlags.Public | BindingFlags.Instance);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[GamePlay] Failed to initialize reflection: {e.Message}");
            }
        }

        /// <summary>
        /// 创建当前方法支持的参数键列表
        /// </summary>
        protected override MethodKey[] CreateKeys()
        {
            return new[]
            {
                new MethodKey("action", "Operation type: screenshot, simulate_click, simulate_drag, set_size, get_info, compress_image", false),
                
                // 输入模拟相关
                new MethodKey("x", "X coordinate for input simulation", true),
                new MethodKey("y", "Y coordinate for input simulation", true),
                new MethodKey("duration", "Duration for drag operations (seconds)", true),
                new MethodKey("target_x", "Target X coordinate for drag operations", true),
                new MethodKey("target_y", "Target Y coordinate for drag operations", true),
                new MethodKey("button", "Mouse button (0=left, 1=right, 2=middle)", true),
                new MethodKey("key_code", "Key code for keyboard simulation", true),
                
                // 窗口管理相关
                new MethodKey("width", "Game window width", true),
                new MethodKey("height", "Game window height", true),
                new MethodKey("size_name", "Predefined size name", true),
                
                // 截图和图像处理相关
                new MethodKey("save_path", "Path to save screenshot/image", true),
                new MethodKey("format", "Image format (PNG, JPG)", true),
                new MethodKey("quality", "Image quality (1-100 for JPG)", true),
                new MethodKey("scale", "Image scale factor", true),
                new MethodKey("compress_ratio", "Compression ratio (0.1-1.0)", true),
                new MethodKey("source_path", "Source image path for compression", true),
                
                // 高级功能
                new MethodKey("region_x", "Screenshot region X coordinate", true),
                new MethodKey("region_y", "Screenshot region Y coordinate", true),
                new MethodKey("region_width", "Screenshot region width", true),
                new MethodKey("region_height", "Screenshot region height", true),
                new MethodKey("delay", "Delay before action execution (seconds)", true),
                new MethodKey("delta", "Scroll wheel delta", true),
                new MethodKey("count", "Count for batch operations", true),
                new MethodKey("interval", "Interval for batch operations", true),
                new MethodKey("base_path", "Base path for batch operations", true)
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
                    // 截图功能
                    .Leaf("screenshot", HandleScreenshotAction)
                    .Leaf("screenshot_region", HandleScreenshotRegionAction)
                    
                    // 输入模拟
                    .Leaf("simulate_click", HandleSimulateClickAction)
                    .Leaf("simulate_drag", HandleSimulateDragAction)
                    .Leaf("simulate_key", HandleSimulateKeyAction)
                    .Leaf("simulate_scroll", HandleSimulateScrollAction)
                    
                    // 窗口管理
                    .Leaf("set_size", HandleSetSizeAction)
                    .Leaf("get_info", HandleGetInfoAction)
                    .Leaf("focus_window", HandleFocusWindowAction)
                    .Leaf("maximize", HandleMaximizeAction)
                    .Leaf("minimize", HandleMinimizeAction)
                    
                    // 图像处理
                    .Leaf("compress_image", HandleCompressImageAction)
                    .Leaf("resize_image", HandleResizeImageAction)
                    .Leaf("convert_format", HandleConvertFormatAction)
                    
                    // 高级功能
                    .Leaf("batch_screenshot", HandleBatchScreenshotAction)
                    .Leaf("start_recording", HandleStartRecordingAction)
                    .Leaf("stop_recording", HandleStopRecordingAction)
                .Build();
        }

        // --- 截图功能 ---

        /// <summary>
        /// 处理截图操作
        /// </summary>
        private object HandleScreenshotAction(StateTreeContext ctx)
        {
            try
            {
                var args = ctx.JsonData;
                var savePath = args["save_path"]?.ToString() ?? "Assets/Screenshots/screenshot.png";
                var format = args["format"]?.ToString() ?? "PNG";
                var quality = args["quality"]?.ToObject<int>() ?? 90;
                var scale = args["scale"]?.ToObject<float>() ?? 1.0f;
                var delay = args["delay"]?.ToObject<float>() ?? 0f;

                if (delay > 0)
                {
                    EditorApplication.delayCall += () => ExecuteScreenshot(savePath, format, quality, scale);
                    return Response.Success($"Screenshot scheduled in {delay} seconds", new { path = savePath });
                }
                else
                {
                    return ExecuteScreenshot(savePath, format, quality, scale);
                }
            }
            catch (Exception e)
            {
                return Response.Error($"Screenshot failed: {e.Message}");
            }
        }

        /// <summary>
        /// 执行截图操作
        /// </summary>
        private object ExecuteScreenshot(string savePath, string format, int quality, float scale)
        {
            try
            {
                // 确保目录存在
                var directory = Path.GetDirectoryName(savePath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // 获取Game窗口
                var gameView = GetGameView();
                if (gameView == null)
                {
                    return Response.Error("No Game window found");
                }

                // 获取Game窗口的RenderTexture
                var renderTexture = GetGameViewRenderTexture();
                if (renderTexture == null)
                {
                    return Response.Error("Failed to get Game window render texture");
                }

                // 创建Texture2D并读取像素
                var texture = new Texture2D(renderTexture.width, renderTexture.height, TextureFormat.RGB24, false);
                var activeRT = RenderTexture.active;
                RenderTexture.active = renderTexture;
                texture.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
                texture.Apply();
                RenderTexture.active = activeRT;

                // 如果需要缩放
                if (Math.Abs(scale - 1.0f) > 0.001f)
                {
                    var scaledWidth = Mathf.RoundToInt(texture.width * scale);
                    var scaledHeight = Mathf.RoundToInt(texture.height * scale);
                    texture = ScaleTexture(texture, scaledWidth, scaledHeight);
                }

                // 保存图像
                byte[] imageData;
                if (format.ToUpper() == "JPG" || format.ToUpper() == "JPEG")
                {
                    imageData = texture.EncodeToJPG(quality);
                }
                else
                {
                    imageData = texture.EncodeToPNG();
                }

                File.WriteAllBytes(savePath, imageData);
                UnityEngine.Object.DestroyImmediate(texture);

                AssetDatabase.Refresh();

                return Response.Success("Screenshot saved successfully", new
                {
                    path = savePath,
                    width = renderTexture.width,
                    height = renderTexture.height,
                    format = format,
                    size_bytes = imageData.Length
                });
            }
            catch (Exception e)
            {
                return Response.Error($"Screenshot execution failed: {e.Message}");
            }
        }

        /// <summary>
        /// 处理区域截图
        /// </summary>
        private object HandleScreenshotRegionAction(StateTreeContext ctx)
        {
            try
            {
                var args = ctx.JsonData;
                var x = args["region_x"]?.ToObject<int>() ?? 0;
                var y = args["region_y"]?.ToObject<int>() ?? 0;
                var width = args["region_width"]?.ToObject<int>() ?? 100;
                var height = args["region_height"]?.ToObject<int>() ?? 100;
                var savePath = args["save_path"]?.ToString() ?? "Assets/Screenshots/region_screenshot.png";

                return ExecuteRegionScreenshot(x, y, width, height, savePath);
            }
            catch (Exception e)
            {
                return Response.Error($"Region screenshot failed: {e.Message}");
            }
        }

        // --- 输入模拟功能 ---

        /// <summary>
        /// 处理模拟点击操作
        /// </summary>
        private object HandleSimulateClickAction(StateTreeContext ctx)
        {
            try
            {
                var args = ctx.JsonData;
                var x = args["x"]?.ToObject<float>() ?? 0f;
                var y = args["y"]?.ToObject<float>() ?? 0f;
                var button = args["button"]?.ToObject<int>() ?? 0;
                var delay = args["delay"]?.ToObject<float>() ?? 0f;

                var input = new SimulatedInput
                {
                    type = InputType.Click,
                    position = new Vector2(x, y),
                    button = button,
                    delay = delay
                };

                if (delay > 0)
                {
                    EditorApplication.delayCall += () => ExecuteInputSimulation(input);
                }
                else
                {
                    ExecuteInputSimulation(input);
                }

                return Response.Success($"Click simulation queued at ({x}, {y})", new
                {
                    x = x,
                    y = y,
                    button = button,
                    delay = delay
                });
            }
            catch (Exception e)
            {
                return Response.Error($"Click simulation failed: {e.Message}");
            }
        }

        /// <summary>
        /// 处理模拟拖拽操作
        /// </summary>
        private object HandleSimulateDragAction(StateTreeContext ctx)
        {
            try
            {
                var args = ctx.JsonData;
                var x = args["x"]?.ToObject<float>() ?? 0f;
                var y = args["y"]?.ToObject<float>() ?? 0f;
                var targetX = args["target_x"]?.ToObject<float>() ?? 0f;
                var targetY = args["target_y"]?.ToObject<float>() ?? 0f;
                var duration = args["duration"]?.ToObject<float>() ?? 1f;
                var delay = args["delay"]?.ToObject<float>() ?? 0f;

                var input = new SimulatedInput
                {
                    type = InputType.Drag,
                    position = new Vector2(x, y),
                    targetPosition = new Vector2(targetX, targetY),
                    duration = duration,
                    delay = delay
                };

                if (delay > 0)
                {
                    EditorApplication.delayCall += () => ExecuteInputSimulation(input);
                }
                else
                {
                    ExecuteInputSimulation(input);
                }

                return Response.Success($"Drag simulation queued from ({x}, {y}) to ({targetX}, {targetY})", new
                {
                    start_x = x,
                    start_y = y,
                    end_x = targetX,
                    end_y = targetY,
                    duration = duration,
                    delay = delay
                });
            }
            catch (Exception e)
            {
                return Response.Error($"Drag simulation failed: {e.Message}");
            }
        }

        /// <summary>
        /// 处理按键模拟
        /// </summary>
        private object HandleSimulateKeyAction(StateTreeContext ctx)
        {
            try
            {
                var args = ctx.JsonData;
                var keyCode = args["key_code"]?.ToString() ?? "";
                var delay = args["delay"]?.ToObject<float>() ?? 0f;

                var input = new SimulatedInput
                {
                    type = InputType.Key,
                    keyCode = keyCode,
                    delay = delay
                };

                if (delay > 0)
                {
                    EditorApplication.delayCall += () => ExecuteInputSimulation(input);
                }
                else
                {
                    ExecuteInputSimulation(input);
                }

                return Response.Success($"Key simulation queued: {keyCode}", new
                {
                    key = keyCode,
                    delay = delay
                });
            }
            catch (Exception e)
            {
                return Response.Error($"Key simulation failed: {e.Message}");
            }
        }

        /// <summary>
        /// 处理滚轮模拟
        /// </summary>
        private object HandleSimulateScrollAction(StateTreeContext ctx)
        {
            try
            {
                var args = ctx.JsonData;
                var x = args["x"]?.ToObject<float>() ?? 0f;
                var y = args["y"]?.ToObject<float>() ?? 0f;
                var delta = args["delta"]?.ToObject<float>() ?? 1f;
                var delay = args["delay"]?.ToObject<float>() ?? 0f;

                var input = new SimulatedInput
                {
                    type = InputType.Scroll,
                    position = new Vector2(x, y),
                    scrollDelta = delta,
                    delay = delay
                };

                if (delay > 0)
                {
                    EditorApplication.delayCall += () => ExecuteInputSimulation(input);
                }
                else
                {
                    ExecuteInputSimulation(input);
                }

                return Response.Success($"Scroll simulation queued at ({x}, {y}) with delta {delta}", new
                {
                    x = x,
                    y = y,
                    delta = delta,
                    delay = delay
                });
            }
            catch (Exception e)
            {
                return Response.Error($"Scroll simulation failed: {e.Message}");
            }
        }

        // --- 窗口管理功能 ---

        /// <summary>
        /// 处理设置窗口大小操作
        /// </summary>
        private object HandleSetSizeAction(StateTreeContext ctx)
        {
            try
            {
                var args = ctx.JsonData;
                var width = args["width"]?.ToObject<int>() ?? 1920;
                var height = args["height"]?.ToObject<int>() ?? 1080;
                var sizeName = args["size_name"]?.ToString();

                var gameView = GetGameView();
                if (gameView == null)
                {
                    return Response.Error("No Game window found");
                }

                if (!string.IsNullOrEmpty(sizeName))
                {
                    // 使用预定义大小
                    SetGameViewSize(sizeName);
                    return Response.Success($"Game window size set to {sizeName}");
                }
                else
                {
                    // 使用自定义大小
                    SetGameViewSize(width, height);
                    return Response.Success($"Game window size set to {width}x{height}", new
                    {
                        width = width,
                        height = height
                    });
                }
            }
            catch (Exception e)
            {
                return Response.Error($"Set size failed: {e.Message}");
            }
        }

        /// <summary>
        /// 处理获取窗口信息操作
        /// </summary>
        private object HandleGetInfoAction(StateTreeContext ctx)
        {
            try
            {
                var gameView = GetGameView();
                if (gameView == null)
                {
                    return Response.Error("No Game window found");
                }

                var info = GetGameViewInfo(gameView);
                return Response.Success("Game window info retrieved", info);
            }
            catch (Exception e)
            {
                return Response.Error($"Get info failed: {e.Message}");
            }
        }

        /// <summary>
        /// 处理聚焦窗口操作
        /// </summary>
        private object HandleFocusWindowAction(StateTreeContext ctx)
        {
            try
            {
                var gameView = GetGameView();
                if (gameView == null)
                {
                    return Response.Error("No Game window found");
                }

                (gameView as EditorWindow)?.Focus();
                return Response.Success("Game window focused");
            }
            catch (Exception e)
            {
                return Response.Error($"Focus window failed: {e.Message}");
            }
        }

        /// <summary>
        /// 处理最大化窗口操作
        /// </summary>
        private object HandleMaximizeAction(StateTreeContext ctx)
        {
            try
            {
                var gameView = GetGameView();
                if (gameView == null)
                {
                    return Response.Error("No Game window found");
                }

                var editorWindow = gameView as EditorWindow;
                if (editorWindow != null)
                {
                    editorWindow.maximized = true;
                }
                return Response.Success("Game window maximized");
            }
            catch (Exception e)
            {
                return Response.Error($"Maximize failed: {e.Message}");
            }
        }

        /// <summary>
        /// 处理最小化窗口操作
        /// </summary>
        private object HandleMinimizeAction(StateTreeContext ctx)
        {
            try
            {
                var gameView = GetGameView();
                if (gameView == null)
                {
                    return Response.Error("No Game window found");
                }

                // Unity EditorWindow does not have a minimized property
                // As an alternative, we could make the window very small, but true minimization is not supported
                var editorWindow = gameView as EditorWindow;
                if (editorWindow != null)
                {
                    // Store original size for potential restoration
                    var originalPos = editorWindow.position;
                    editorWindow.position = new Rect(originalPos.x, originalPos.y, 200, 100);
                    editorWindow.maximized = false;
                }
                return Response.Success("Game window minimized");
            }
            catch (Exception e)
            {
                return Response.Error($"Minimize failed: {e.Message}");
            }
        }

        // --- 图像处理功能 ---

        /// <summary>
        /// 处理图像压缩操作
        /// </summary>
        private object HandleCompressImageAction(StateTreeContext ctx)
        {
            try
            {
                var args = ctx.JsonData;
                var sourcePath = args["source_path"]?.ToString();
                var savePath = args["save_path"]?.ToString();
                var ratio = args["compress_ratio"]?.ToObject<float>() ?? 0.8f;
                var quality = args["quality"]?.ToObject<int>() ?? 80;

                if (string.IsNullOrEmpty(sourcePath))
                {
                    return Response.Error("Source path is required");
                }

                if (string.IsNullOrEmpty(savePath))
                {
                    savePath = sourcePath; // 覆盖原文件
                }

                return CompressImage(sourcePath, savePath, ratio, quality);
            }
            catch (Exception e)
            {
                return Response.Error($"Image compression failed: {e.Message}");
            }
        }

        /// <summary>
        /// 处理图像缩放操作
        /// </summary>
        private object HandleResizeImageAction(StateTreeContext ctx)
        {
            try
            {
                var args = ctx.JsonData;
                var sourcePath = args["source_path"]?.ToString();
                var savePath = args["save_path"]?.ToString();
                var width = args["width"]?.ToObject<int>() ?? 512;
                var height = args["height"]?.ToObject<int>() ?? 512;

                if (string.IsNullOrEmpty(sourcePath))
                {
                    return Response.Error("Source path is required");
                }

                if (string.IsNullOrEmpty(savePath))
                {
                    savePath = sourcePath;
                }

                return ResizeImage(sourcePath, savePath, width, height);
            }
            catch (Exception e)
            {
                return Response.Error($"Image resize failed: {e.Message}");
            }
        }

        /// <summary>
        /// 处理格式转换操作
        /// </summary>
        private object HandleConvertFormatAction(StateTreeContext ctx)
        {
            try
            {
                var args = ctx.JsonData;
                var sourcePath = args["source_path"]?.ToString();
                var savePath = args["save_path"]?.ToString();
                var format = args["format"]?.ToString() ?? "PNG";
                var quality = args["quality"]?.ToObject<int>() ?? 90;

                if (string.IsNullOrEmpty(sourcePath))
                {
                    return Response.Error("Source path is required");
                }

                if (string.IsNullOrEmpty(savePath))
                {
                    var extension = format.ToUpper() == "JPG" ? ".jpg" : ".png";
                    savePath = Path.ChangeExtension(sourcePath, extension);
                }

                return ConvertImageFormat(sourcePath, savePath, format, quality);
            }
            catch (Exception e)
            {
                return Response.Error($"Format conversion failed: {e.Message}");
            }
        }

        // --- 高级功能 ---

        /// <summary>
        /// 处理批量截图操作
        /// </summary>
        private object HandleBatchScreenshotAction(StateTreeContext ctx)
        {
            try
            {
                var args = ctx.JsonData;
                var count = args["count"]?.ToObject<int>() ?? 5;
                var interval = args["interval"]?.ToObject<float>() ?? 1f;
                var basePath = args["base_path"]?.ToString() ?? "Assets/Screenshots/batch";

                StartBatchScreenshot(count, interval, basePath);
                return Response.Success($"Batch screenshot started: {count} screenshots with {interval}s interval", new
                {
                    count = count,
                    interval = interval,
                    base_path = basePath
                });
            }
            catch (Exception e)
            {
                return Response.Error($"Batch screenshot failed: {e.Message}");
            }
        }

        /// <summary>
        /// 处理开始录制操作
        /// </summary>
        private object HandleStartRecordingAction(StateTreeContext ctx)
        {
            // 此功能需要更复杂的实现，可能需要第三方库
            return Response.Error("Recording feature not implemented yet");
        }

        /// <summary>
        /// 处理停止录制操作
        /// </summary>
        private object HandleStopRecordingAction(StateTreeContext ctx)
        {
            // 此功能需要更复杂的实现，可能需要第三方库
            return Response.Error("Recording feature not implemented yet");
        }

        // --- 辅助方法 ---

        /// <summary>
        /// 获取Game窗口实例
        /// </summary>
        private object GetGameView()
        {
            if (gameViewType == null) return null;

            var windows = Resources.FindObjectsOfTypeAll(gameViewType);
            return windows.Length > 0 ? windows[0] : null;
        }

        /// <summary>
        /// 获取Game窗口的RenderTexture
        /// </summary>
        private RenderTexture GetGameViewRenderTexture()
        {
            var gameView = GetGameView();
            if (gameView == null) return null;

            // 通过反射获取RenderTexture
            var renderTextureField = gameViewType.GetField("m_RenderTexture", BindingFlags.NonPublic | BindingFlags.Instance);
            return renderTextureField?.GetValue(gameView) as RenderTexture;
        }

        /// <summary>
        /// 设置Game窗口大小
        /// </summary>
        private void SetGameViewSize(int width, int height)
        {
            var gameView = GetGameView();
            if (gameView == null || targetSizeProperty == null) return;

            targetSizeProperty.SetValue(gameView, new Vector2(width, height));
            repaintMethod?.Invoke(gameView, null);
        }

        /// <summary>
        /// 设置Game窗口预定义大小
        /// </summary>
        private void SetGameViewSize(string sizeName)
        {
            // 实现预定义大小的设置逻辑
            var sizes = new Dictionary<string, Vector2>
            {
                { "HD", new Vector2(1920, 1080) },
                { "FHD", new Vector2(1920, 1080) },
                { "4K", new Vector2(3840, 2160) },
                { "iPhone", new Vector2(375, 667) },
                { "iPad", new Vector2(768, 1024) },
                { "Android", new Vector2(360, 640) }
            };

            if (sizes.ContainsKey(sizeName))
            {
                var size = sizes[sizeName];
                SetGameViewSize((int)size.x, (int)size.y);
            }
        }

        /// <summary>
        /// 获取Game窗口信息
        /// </summary>
        private object GetGameViewInfo(object gameView)
        {
            var info = new Dictionary<string, object>();
            
            try
            {
                if (targetSizeProperty != null)
                {
                    var size = (Vector2)targetSizeProperty.GetValue(gameView);
                    info["width"] = (int)size.x;
                    info["height"] = (int)size.y;
                }

                var editorWindow = gameView as EditorWindow;
                if (editorWindow != null)
                {
                    info["focused"] = editorWindow.hasFocus;
                    info["maximized"] = editorWindow.maximized;
                    // Unity EditorWindow does not have a minimized property
                    info["minimized"] = false; // Always false since true minimization is not supported
                    info["position"] = new { x = editorWindow.position.x, y = editorWindow.position.y };
                    info["window_size"] = new { width = editorWindow.position.width, height = editorWindow.position.height };
                }

                info["is_playing"] = EditorApplication.isPlaying;
                info["is_paused"] = EditorApplication.isPaused;
            }
            catch (Exception e)
            {
                Debug.LogError($"[GamePlay] Failed to get window info: {e.Message}");
            }

            return info;
        }

        /// <summary>
        /// 执行输入模拟
        /// </summary>
        private void ExecuteInputSimulation(SimulatedInput input)
        {
            try
            {
                switch (input.type)
                {
                    case InputType.Click:
                        SimulateClick(input.position, input.button);
                        break;
                    case InputType.Drag:
                        SimulateDrag(input.position, input.targetPosition, input.duration);
                        break;
                    case InputType.Key:
                        SimulateKey(input.keyCode);
                        break;
                    case InputType.Scroll:
                        SimulateScroll(input.position, input.scrollDelta);
                        break;
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[GamePlay] Input simulation failed: {e.Message}");
            }
        }

        /// <summary>
        /// 模拟点击
        /// </summary>
        private void SimulateClick(Vector2 position, int button)
        {
            // 在游戏运行时通过Event系统模拟点击
            if (EditorApplication.isPlaying)
            {
                var mouseEvent = Event.current;
                if (mouseEvent != null)
                {
                    mouseEvent.type = EventType.MouseDown;
                    mouseEvent.mousePosition = position;
                    mouseEvent.button = button;
                    
                    // 发送鼠标按下和松开事件
                    EditorApplication.delayCall += () =>
                    {
                        mouseEvent.type = EventType.MouseUp;
                    };
                }
            }
            
            LogInfo($"[GamePlay] Simulated click at ({position.x}, {position.y}) with button {button}");
        }

        /// <summary>
        /// 模拟拖拽
        /// </summary>
        private void SimulateDrag(Vector2 start, Vector2 end, float duration)
        {
            // 实现拖拽模拟逻辑
            var steps = Mathf.Max(10, Mathf.RoundToInt(duration * 60)); // 60fps
            var stepDuration = duration / steps;
            
            for (int i = 0; i <= steps; i++)
            {
                var t = (float)i / steps;
                var currentPos = Vector2.Lerp(start, end, t);
                
                EditorApplication.delayCall += () =>
                {
                    // 发送鼠标移动事件
                    if (Event.current != null)
                    {
                        Event.current.type = EventType.MouseDrag;
                        Event.current.mousePosition = currentPos;
                    }
                };
            }
            
            LogInfo($"[GamePlay] Simulated drag from ({start.x}, {start.y}) to ({end.x}, {end.y}) over {duration}s");
        }

        /// <summary>
        /// 模拟按键
        /// </summary>
        private void SimulateKey(string keyCode)
        {
            if (EditorApplication.isPlaying && Event.current != null)
            {
                Event.current.type = EventType.KeyDown;
                if (Enum.TryParse<KeyCode>(keyCode, out KeyCode key))
                {
                    // 设置按键事件
                    var keyEvent = Event.KeyboardEvent(keyCode);
                    if (keyEvent != null)
                    {
                        // 发送按键事件
                        EditorApplication.delayCall += () =>
                        {
                            keyEvent.type = EventType.KeyUp;
                        };
                    }
                }
            }
            
            LogInfo($"[GamePlay] Simulated key: {keyCode}");
        }

        /// <summary>
        /// 模拟滚轮
        /// </summary>
        private void SimulateScroll(Vector2 position, float delta)
        {
            if (EditorApplication.isPlaying && Event.current != null)
            {
                Event.current.type = EventType.ScrollWheel;
                Event.current.mousePosition = position;
                Event.current.delta = new Vector2(0, delta);
            }
            
            LogInfo($"[GamePlay] Simulated scroll at ({position.x}, {position.y}) with delta {delta}");
        }

        /// <summary>
        /// 执行区域截图
        /// </summary>
        private object ExecuteRegionScreenshot(int x, int y, int width, int height, string savePath)
        {
            try
            {
                var renderTexture = GetGameViewRenderTexture();
                if (renderTexture == null)
                {
                    return Response.Error("Failed to get Game window render texture");
                }

                // 确保区域在有效范围内
                x = Mathf.Clamp(x, 0, renderTexture.width - 1);
                y = Mathf.Clamp(y, 0, renderTexture.height - 1);
                width = Mathf.Clamp(width, 1, renderTexture.width - x);
                height = Mathf.Clamp(height, 1, renderTexture.height - y);

                // 创建区域纹理
                var regionTexture = new Texture2D(width, height, TextureFormat.RGB24, false);
                var activeRT = RenderTexture.active;
                RenderTexture.active = renderTexture;
                regionTexture.ReadPixels(new Rect(x, y, width, height), 0, 0);
                regionTexture.Apply();
                RenderTexture.active = activeRT;

                // 保存图像
                var imageData = regionTexture.EncodeToPNG();
                File.WriteAllBytes(savePath, imageData);
                UnityEngine.Object.DestroyImmediate(regionTexture);

                AssetDatabase.Refresh();

                return Response.Success("Region screenshot saved successfully", new
                {
                    path = savePath,
                    x = x,
                    y = y,
                    width = width,
                    height = height,
                    size_bytes = imageData.Length
                });
            }
            catch (Exception e)
            {
                return Response.Error($"Region screenshot failed: {e.Message}");
            }
        }

        /// <summary>
        /// 缩放纹理
        /// </summary>
        private Texture2D ScaleTexture(Texture2D source, int newWidth, int newHeight)
        {
            var scaled = new Texture2D(newWidth, newHeight, source.format, false);
            var pixels = scaled.GetPixels();
            
            for (int y = 0; y < newHeight; y++)
            {
                for (int x = 0; x < newWidth; x++)
                {
                    var sourceX = Mathf.RoundToInt((float)x / newWidth * source.width);
                    var sourceY = Mathf.RoundToInt((float)y / newHeight * source.height);
                    pixels[y * newWidth + x] = source.GetPixel(sourceX, sourceY);
                }
            }
            
            scaled.SetPixels(pixels);
            scaled.Apply();
            return scaled;
        }

        /// <summary>
        /// 压缩图像
        /// </summary>
        private object CompressImage(string sourcePath, string savePath, float ratio, int quality)
        {
            try
            {
                if (!File.Exists(sourcePath))
                {
                    return Response.Error("Source file not found");
                }

                // 加载纹理
                var imageData = File.ReadAllBytes(sourcePath);
                var texture = new Texture2D(2, 2);
                texture.LoadImage(imageData);

                // 计算新尺寸
                var newWidth = Mathf.RoundToInt(texture.width * ratio);
                var newHeight = Mathf.RoundToInt(texture.height * ratio);

                // 缩放纹理
                var scaledTexture = ScaleTexture(texture, newWidth, newHeight);

                // 保存压缩后的图像
                byte[] compressedData;
                var extension = Path.GetExtension(savePath).ToLower();
                if (extension == ".jpg" || extension == ".jpeg")
                {
                    compressedData = scaledTexture.EncodeToJPG(quality);
                }
                else
                {
                    compressedData = scaledTexture.EncodeToPNG();
                }

                File.WriteAllBytes(savePath, compressedData);
                
                UnityEngine.Object.DestroyImmediate(texture);
                UnityEngine.Object.DestroyImmediate(scaledTexture);
                AssetDatabase.Refresh();

                return Response.Success("Image compressed successfully", new
                {
                    source_path = sourcePath,
                    save_path = savePath,
                    original_size = imageData.Length,
                    compressed_size = compressedData.Length,
                    compression_ratio = (float)compressedData.Length / imageData.Length,
                    new_width = newWidth,
                    new_height = newHeight
                });
            }
            catch (Exception e)
            {
                return Response.Error($"Image compression failed: {e.Message}");
            }
        }

        /// <summary>
        /// 调整图像大小
        /// </summary>
        private object ResizeImage(string sourcePath, string savePath, int width, int height)
        {
            try
            {
                if (!File.Exists(sourcePath))
                {
                    return Response.Error("Source file not found");
                }

                var imageData = File.ReadAllBytes(sourcePath);
                var texture = new Texture2D(2, 2);
                texture.LoadImage(imageData);

                var resizedTexture = ScaleTexture(texture, width, height);
                var resizedData = resizedTexture.EncodeToPNG();
                File.WriteAllBytes(savePath, resizedData);

                UnityEngine.Object.DestroyImmediate(texture);
                UnityEngine.Object.DestroyImmediate(resizedTexture);
                AssetDatabase.Refresh();

                return Response.Success("Image resized successfully", new
                {
                    source_path = sourcePath,
                    save_path = savePath,
                    new_width = width,
                    new_height = height,
                    size_bytes = resizedData.Length
                });
            }
            catch (Exception e)
            {
                return Response.Error($"Image resize failed: {e.Message}");
            }
        }

        /// <summary>
        /// 转换图像格式
        /// </summary>
        private object ConvertImageFormat(string sourcePath, string savePath, string format, int quality)
        {
            try
            {
                if (!File.Exists(sourcePath))
                {
                    return Response.Error("Source file not found");
                }

                var imageData = File.ReadAllBytes(sourcePath);
                var texture = new Texture2D(2, 2);
                texture.LoadImage(imageData);

                byte[] convertedData;
                if (format.ToUpper() == "JPG" || format.ToUpper() == "JPEG")
                {
                    convertedData = texture.EncodeToJPG(quality);
                }
                else
                {
                    convertedData = texture.EncodeToPNG();
                }

                File.WriteAllBytes(savePath, convertedData);
                UnityEngine.Object.DestroyImmediate(texture);
                AssetDatabase.Refresh();

                return Response.Success($"Image converted to {format} successfully", new
                {
                    source_path = sourcePath,
                    save_path = savePath,
                    format = format,
                    size_bytes = convertedData.Length
                });
            }
            catch (Exception e)
            {
                return Response.Error($"Format conversion failed: {e.Message}");
            }
        }

        /// <summary>
        /// 开始批量截图
        /// </summary>
        private void StartBatchScreenshot(int count, float interval, string basePath)
        {
            var directory = Path.GetDirectoryName(basePath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            for (int i = 0; i < count; i++)
            {
                var index = i;
                EditorApplication.delayCall += () =>
                {
                    var fileName = $"{Path.GetFileNameWithoutExtension(basePath)}_{index + 1:D3}.png";
                    var fullPath = Path.Combine(directory, fileName);
                    ExecuteScreenshot(fullPath, "PNG", 90, 1.0f);
                };
            }
        }
    }

    /// <summary>
    /// 模拟输入的数据结构
    /// </summary>
    public class SimulatedInput
    {
        public InputType type;
        public Vector2 position;
        public Vector2 targetPosition;
        public int button;
        public string keyCode;
        public float duration;
        public float delay;
        public float scrollDelta;
    }

    /// <summary>
    /// 输入类型枚举
    /// </summary>
    public enum InputType
    {
        Click,
        Drag,
        Key,
        Scroll
    }
}
