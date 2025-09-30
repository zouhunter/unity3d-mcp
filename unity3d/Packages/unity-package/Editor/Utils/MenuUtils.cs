using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityMcp.Models;

namespace UnityMcp.Tools
{
    public class MenuUtils
    {
        /// <summary>
        /// UGUI控件菜单路径映射
        /// </summary>
        private static readonly Dictionary<string, string[]> UIMenuPaths = new Dictionary<string, string[]>
        {
            // 需要Legacy路径的控件
            { "gameobject/ui/text", new[] { "GameObject/UI/Text", "GameObject/UI/Legacy/Text" } },
            { "gameobject/ui/input_field", new[] { "GameObject/UI/Input Field", "GameObject/UI/Legacy/Input Field" } },
            { "gameobject/ui/inputfield", new[] { "GameObject/UI/Input Field", "GameObject/UI/Legacy/Input Field" } },
            { "gameobject/ui/dropdown", new[] { "GameObject/UI/Dropdown", "GameObject/UI/Legacy/Dropdown" } },
            { "gameobject/ui/button", new[] { "GameObject/UI/Button", "GameObject/UI/Legacy/Button" } },
            
            // 没有变化的控件
            { "gameobject/ui/toggle", new[] { "GameObject/UI/Toggle" } },
            { "gameobject/ui/slider", new[] { "GameObject/UI/Slider" } },
            { "gameobject/ui/scrollbar", new[] { "GameObject/UI/Scrollbar" } },
            { "gameobject/ui/scroll_view", new[] { "GameObject/UI/Scroll View" } },
            { "gameobject/ui/panel", new[] { "GameObject/UI/Panel" } },
            { "gameobject/ui/image", new[] { "GameObject/UI/Image" } },
            { "gameobject/ui/raw_image", new[] { "GameObject/UI/Raw Image" } },
            { "gameobject/ui/canvas", new[] { "GameObject/UI/Canvas" } },
            { "gameobject/ui/event_system", new[] { "GameObject/UI/Event System" } }
        };

        /// <summary>
        /// 检查是否为Unity 2021.2+（需要Legacy路径）
        /// </summary>
        private static bool IsLegacyVersion()
        {
            string version = Application.unityVersion;

            // 解析主版本号
            var parts = version.Split('.');
            if (parts.Length < 2) return true; // 安全起见，假设是新版本

            if (int.TryParse(parts[0], out int major))
            {
                // 2022年及以后的版本肯定需要Legacy
                if (major >= 2022) return true;

                // 2021年版本需要检查次版本号
                if (major == 2021 && int.TryParse(parts[1], out int minor))
                {
                    return minor >= 2; // 2021.2及以后需要Legacy
                }
            }

            return false; // 2020及更早版本不需要Legacy
        }

        /// <summary>
        /// 尝试执行菜单项，自动处理兼容性
        /// </summary>
        public static object TryExecuteMenuItem(string menuPath)
        {
            if (string.IsNullOrWhiteSpace(menuPath))
            {
                return Response.Error("Menu path is missing or empty.");
            }

            // 标准化菜单键名
            string normalizedKey = menuPath.ToLower().Replace(" ", "_").Replace("-", "_");
            bool isLegacy = IsLegacyVersion();

            // 获取可能的路径
            var tryPaths = new List<string>();

            if (UIMenuPaths.TryGetValue(normalizedKey, out string[] knownPaths))
            {
                // 如果是已知控件，按版本选择路径
                if (knownPaths.Length > 1)
                {
                    if (isLegacy)
                    {
                        tryPaths.Add(knownPaths[1]); // Legacy路径优先
                        tryPaths.Add(knownPaths[0]); // 备用路径
                    }
                    else
                    {
                        tryPaths.Add(knownPaths[0]); // 原始路径优先
                        tryPaths.Add(knownPaths[1]); // Legacy备用路径
                    }
                }
                else
                {
                    tryPaths.AddRange(knownPaths);
                }
            }
            else
            {
                tryPaths.Add(menuPath);
            }

            // 依次尝试执行
            foreach (string path in tryPaths)
            {
                if (EditorApplication.ExecuteMenuItem(path))
                {
                    return Response.Success($"Successfully executed menu item: '{path}' (Unity {Application.unityVersion})");
                }
            }

            return Response.Error($"Failed to execute menu item. Unity {Application.unityVersion}. Tried: [{string.Join(", ", tryPaths)}]");
        }

        /// <summary>
        /// 处理菜单执行命令
        /// </summary>
        public static object HandleExecuteMenu(JObject cmd)
        {
            string menuPath = cmd["menu_path"]?.ToString();

            if (string.IsNullOrWhiteSpace(menuPath))
            {
                return Response.Error("Required parameter 'menu_path' is missing or empty.");
            }

            return TryExecuteMenuItem(menuPath);
        }
    }
}
