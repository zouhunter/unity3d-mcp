using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditorInternal;

namespace UnityMcp.Tools
{
    /// <summary>
    /// UI类型枚举
    /// </summary>
    [System.Serializable]
    public enum UIType
    {
        UGUI = 0,
        UIToolkit = 1,
        NGUI = 2,
        FairyGUI = 3,
        Custom = 4
    }

    /// <summary>
    /// UI类型数据
    /// </summary>
    [System.Serializable]
    public class UITypeData
    {
        public string typeName;
        public List<string> buildSteps;
        public List<string> buildEnvironments;

        public UITypeData(string name)
        {
            typeName = name;
            buildSteps = new List<string>();
            buildEnvironments = new List<string>();
        }
    }

    /// <summary>
    /// 可序列化的UI类型数据（用于Unity序列化）
    /// </summary>
    [System.Serializable]
    public class UITypeDataSerializable
    {
        public UIType uiType;
        public string typeName;
        public List<string> buildSteps;
        public List<string> buildEnvironments;

        public UITypeDataSerializable(UIType type, UITypeData data)
        {
            uiType = type;
            typeName = data.typeName;
            buildSteps = new List<string>(data.buildSteps);
            buildEnvironments = new List<string>(data.buildEnvironments);
        }

        public UITypeData ToUITypeData()
        {
            var data = new UITypeData(typeName);
            data.buildSteps = new List<string>(buildSteps);
            data.buildEnvironments = new List<string>(buildEnvironments);
            return data;
        }
    }

    /// <summary>
    /// MCP UI设置提供器，用于在Unity的ProjectSettings窗口中显示UI相关设置
    /// </summary>
    [System.Serializable]
    public class McpUISettingsProvider
    {
        private static Vector2 scrollPosition;
        private static ReorderableList buildStepsList;
        private static ReorderableList preferredComponentsList;
        private static UIType currentUIType = UIType.UGUI;

        /// <summary>
        /// 当前选择的UI类型
        /// </summary>
        public UIType selectedUIType
        {
            get { return _selectedUIType; }
            set { _selectedUIType = value; }
        }
        [SerializeField] private UIType _selectedUIType = UIType.UGUI;

        /// <summary>
        /// 所有UI类型的数据
        /// </summary>
        public Dictionary<UIType, UITypeData> uiTypeDataDict
        {
            get
            {
                if (_uiTypeDataDict == null)
                    InitializeUITypeData();
                return _uiTypeDataDict;
            }
        }
        [System.NonSerialized] private Dictionary<UIType, UITypeData> _uiTypeDataDict;

        /// <summary>
        /// 序列化的UI类型数据列表（用于Unity序列化）
        /// </summary>
        [SerializeField] private List<UITypeDataSerializable> _serializedUITypeData = new List<UITypeDataSerializable>();

        /// <summary>
        /// UI构建步骤（返回当前UI类型的步骤）
        /// </summary>
        public List<string> ui_build_steps
        {
            get
            {
                return GetCurrentUITypeData().buildSteps;
            }
            set
            {
                GetCurrentUITypeData().buildSteps = value;
            }
        }

        /// <summary>
        /// UI构建环境（返回当前UI类型的环境）
        /// </summary>
        public List<string> ui_build_enviroments
        {
            get
            {
                return GetCurrentUITypeData().buildEnvironments;
            }
            set
            {
                GetCurrentUITypeData().buildEnvironments = value;
            }
        }

        /// <summary>
        /// 初始化UI类型数据
        /// </summary>
        private void InitializeUITypeData()
        {
            _uiTypeDataDict = new Dictionary<UIType, UITypeData>();

            // 从序列化数据中恢复
            foreach (var serializedData in _serializedUITypeData)
            {
                _uiTypeDataDict[serializedData.uiType] = serializedData.ToUITypeData();
            }

            // 确保所有UI类型都有数据
            foreach (UIType uiType in System.Enum.GetValues(typeof(UIType)))
            {
                if (!_uiTypeDataDict.ContainsKey(uiType))
                {
                    _uiTypeDataDict[uiType] = CreateDefaultUITypeData(uiType);
                }
            }
        }

        /// <summary>
        /// 获取当前UI类型的数据
        /// </summary>
        private UITypeData GetCurrentUITypeData()
        {
            if (!uiTypeDataDict.ContainsKey(selectedUIType))
            {
                uiTypeDataDict[selectedUIType] = CreateDefaultUITypeData(selectedUIType);
            }
            return uiTypeDataDict[selectedUIType];
        }

        /// <summary>
        /// 序列化UI类型数据
        /// </summary>
        public void SerializeUITypeData()
        {
            _serializedUITypeData.Clear();
            if (_uiTypeDataDict != null)
            {
                foreach (var kvp in _uiTypeDataDict)
                {
                    _serializedUITypeData.Add(new UITypeDataSerializable(kvp.Key, kvp.Value));
                }
            }
        }

        /// <summary>
        /// 创建默认的UI类型数据
        /// </summary>
        private UITypeData CreateDefaultUITypeData(UIType uiType)
        {
            var data = new UITypeData(uiType.ToString());
            data.buildSteps = GetDefaultBuildSteps(uiType);
            data.buildEnvironments = GetDefaultBuildEnvironments(uiType);
            return data;
        }

        /// <summary>
        /// 获取默认的UI构建步骤
        /// </summary>
        public static List<string> GetDefaultBuildSteps()
        {
            return GetDefaultBuildSteps(UIType.UGUI);
        }

        /// <summary>
        /// 根据UI类型获取默认的UI构建步骤
        /// </summary>
        public static List<string> GetDefaultBuildSteps(UIType uiType)
        {
            switch (uiType)
            {
                case UIType.UGUI:
                    return new List<string>
                    {
                        "回顾unity-mcp工具使用方法",
                        "利用figma_manage下载并分析设计稿结构",
                        "创建Canvas和根容器并设置好尺寸",
                        "将Game尺寸和UI尺寸匹配",
                        "按照设计稿创建必要的UI组件",
                        "按理想的UI层级进行组件调整",
                        "记录创建的UI组件名称和原来的节点id到规则文件",
                        "配置组件属性",
                        "基于ugui_layout的mcp工具和设计稿信息，进行界面布局调整",
                        "优化屏幕适配",
                        "记录更改方式到规则文件",
                        "下载界面控件需要的图片资源",
                        "将图片信息记录到规则文件",
                        "将下载的图片，利用mcp加载到指定的UI组件上"
                    };

                case UIType.UIToolkit:
                    return new List<string>
                    {
                        "回顾unity-mcp工具使用方法",
                        "分析UI Toolkit设计需求",
                        "创建UI Document和根VisualElement",
                        "设计USS样式文件",
                        "创建UXML结构文件",
                        "配置UI Builder布局",
                        "绑定C#脚本逻辑",
                        "处理事件和交互",
                        "优化响应式布局",
                        "测试不同分辨率适配"
                    };

                case UIType.NGUI:
                    return new List<string>
                    {
                        "回顾unity-mcp工具使用方法",
                        "创建NGUI Root和Camera",
                        "设置UI Atlas纹理",
                        "创建NGUI面板和组件",
                        "配置锚点和布局",
                        "处理NGUI事件系统",
                        "优化Draw Call",
                        "配置字体和本地化"
                    };

                case UIType.FairyGUI:
                    return new List<string>
                    {
                        "回顾unity-mcp工具使用方法",
                        "导入FairyGUI编辑器资源",
                        "创建FairyGUI包和组件",
                        "设置UI适配规则",
                        "配置动画和过渡效果",
                        "绑定代码逻辑",
                        "优化性能和内存",
                        "测试多平台兼容性"
                    };

                case UIType.Custom:
                default:
                    return new List<string>
                    {
                        "分析自定义UI系统需求",
                        "设计UI架构",
                        "实现核心UI组件",
                        "配置渲染管线",
                        "处理输入和事件",
                        "优化性能",
                        "测试和调试"
                    };
            }
        }

        /// <summary>
        /// 获取默认的UI环境说明
        /// </summary>
        public static List<string> GetDefaultBuildEnvironments()
        {
            return GetDefaultBuildEnvironments(UIType.UGUI);
        }

        /// <summary>
        /// 根据UI类型获取默认的UI环境说明
        /// </summary>
        public static List<string> GetDefaultBuildEnvironments(UIType uiType)
        {
            switch (uiType)
            {
                case UIType.UGUI:
                    return new List<string>
                    {
                        "基于UGUI界面",
                        "支持TMP字体",
                        "文本相关组件必须使用TMP",
                        "设置稿的坐标系已统一为Unity坐标系，中心为原点",
                        "仅圆角的图片，可以不下载，直接将Image替换为ProceduralUIImage"
                    };

                case UIType.UIToolkit:
                    return new List<string>
                    {
                        "基于UI Toolkit系统",
                        "使用USS样式表",
                        "UXML文件定义结构",
                        "支持Flexbox布局",
                        "响应式设计优先",
                        "Vector图形支持",
                        "现代Web标准兼容"
                    };

                case UIType.NGUI:
                    return new List<string>
                    {
                        "基于NGUI系统",
                        "使用Atlas纹理管理",
                        "支持BMFont字体",
                        "Draw Call优化重要",
                        "锚点系统布局",
                        "事件系统独立",
                        "适合移动平台"
                    };

                case UIType.FairyGUI:
                    return new List<string>
                    {
                        "基于FairyGUI编辑器",
                        "可视化UI设计",
                        "组件化开发",
                        "丰富的动画支持",
                        "多分辨率适配",
                        "支持复杂交互",
                        "跨平台兼容"
                    };

                case UIType.Custom:
                default:
                    return new List<string>
                    {
                        "自定义UI系统",
                        "根据项目需求定制",
                        "可扩展架构设计",
                        "性能优化优先",
                        "灵活的渲染管线"
                    };
            }
        }

        [SettingsProvider]
        public static SettingsProvider CreateMcpUISettingsProvider()
        {
            var provider = new SettingsProvider("Project/MCP/UI-Prompts", SettingsScope.Project)
            {
                label = "UI-Prompts",
                guiHandler = (searchContext) =>
                {
                    DrawMcpUISettings();
                },
                keywords = new[] { "UI", "UI", "Generation", "Rules", "Figma", "Canvas", "Button", "Text", "Image" }
            };

            return provider;
        }

        private static void DrawMcpUISettings()
        {
            var settings = McpSettings.Instance;
            if (settings.uiSettings == null)
                settings.uiSettings = new McpUISettingsProvider();

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            // UI简介
            EditorGUILayout.LabelField("UI 生成规则配置", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "配置Unity UI系统的自动生成规则和偏好设置。" +
                "这些设置将影响通过MCP工具生成的UI组件的默认行为和结构。",
                MessageType.Info);

            EditorGUILayout.Space(10);

            // UI类型选择器
            EditorGUILayout.LabelField("UI类型选择", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            var newUIType = (UIType)EditorGUILayout.EnumPopup("当前UI类型", settings.uiSettings.selectedUIType);
            if (EditorGUI.EndChangeCheck())
            {
                // 保存当前数据
                settings.uiSettings.SerializeUITypeData();

                // 切换UI类型
                settings.uiSettings.selectedUIType = newUIType;
                currentUIType = newUIType;

                // 重置列表以刷新显示
                buildStepsList = null;
                preferredComponentsList = null;

                settings.SaveSettings();
            }

            EditorGUILayout.HelpBox($"当前选择: {settings.uiSettings.selectedUIType} - 每种UI类型都有独立的构建步骤和环境配置", MessageType.Info);
            EditorGUILayout.Space(10);

            // 初始化ReorderableList
            if (buildStepsList == null)
            {
                buildStepsList = new ReorderableList(settings.uiSettings.ui_build_steps, typeof(string), true, true, true, true);
                buildStepsList.drawHeaderCallback = (Rect rect) =>
                {
                    EditorGUI.LabelField(rect, "");

                    // 添加写入按钮
                    Rect writeButtonRect = new Rect(rect.width - 125, rect.y, 60, rect.height);
                    if (GUI.Button(writeButtonRect, "写入"))
                    {
                        if (EditorUtility.DisplayDialog("确认写入", "确定要将当前UI构建步骤写入到代码中作为默认值吗？", "确定", "取消"))
                        {
                            WriteDefaultBuildStepsToCode(settings.uiSettings.ui_build_steps);
                        }
                    }

                    // 添加重置按钮
                    Rect resetButtonRect = new Rect(rect.width - 60, rect.y, 60, rect.height);
                    if (GUI.Button(resetButtonRect, "重置"))
                    {
                        if (EditorUtility.DisplayDialog("确认重置", $"确定要重置{settings.uiSettings.selectedUIType}的UI构建步骤为默认值吗？", "确定", "取消"))
                        {
                            settings.uiSettings.ui_build_steps = McpUISettingsProvider.GetDefaultBuildSteps(settings.uiSettings.selectedUIType);
                            settings.SaveSettings();
                        }
                    }
                };
                buildStepsList.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) =>
                {
                    settings.uiSettings.ui_build_steps[index] = EditorGUI.TextField(rect, settings.uiSettings.ui_build_steps[index]);
                };
                buildStepsList.onAddCallback = (ReorderableList list) =>
                {
                    settings.uiSettings.ui_build_steps.Add("新步骤？");
                };
            }

            if (preferredComponentsList == null)
            {
                preferredComponentsList = new ReorderableList(settings.uiSettings.ui_build_enviroments, typeof(string), true, true, true, true);
                preferredComponentsList.drawHeaderCallback = (Rect rect) =>
                {
                    EditorGUI.LabelField(rect, "");

                    // 添加写入按钮
                    Rect writeButtonRect = new Rect(rect.width - 125, rect.y, 60, rect.height);
                    if (GUI.Button(writeButtonRect, "写入"))
                    {
                        if (EditorUtility.DisplayDialog("确认写入", "确定要将当前UI环境说明写入到代码中作为默认值吗？", "确定", "取消"))
                        {
                            WriteDefaultBuildEnvironmentsToCode(settings.uiSettings.ui_build_enviroments);
                        }
                    }

                    // 添加重置按钮
                    Rect resetButtonRect = new Rect(rect.width - 60, rect.y, 60, rect.height);
                    if (GUI.Button(resetButtonRect, "重置"))
                    {
                        if (EditorUtility.DisplayDialog("确认重置", $"确定要重置{settings.uiSettings.selectedUIType}的UI环境说明为默认值吗？", "确定", "取消"))
                        {
                            settings.uiSettings.ui_build_enviroments = McpUISettingsProvider.GetDefaultBuildEnvironments(settings.uiSettings.selectedUIType);
                            settings.SaveSettings();
                        }
                    }
                };
                preferredComponentsList.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) =>
                {
                    if (index > settings.uiSettings.ui_build_enviroments.Count)
                    {
                        settings.uiSettings.ui_build_enviroments.Add("");
                    }
                    settings.uiSettings.ui_build_enviroments[index] = EditorGUI.TextField(rect, settings.uiSettings.ui_build_enviroments[index]);
                };
                preferredComponentsList.onAddCallback = (ReorderableList list) =>
                {
                    settings.uiSettings.ui_build_enviroments.Add("");
                };
            }

            // 绘制UI构建步骤列表
            EditorGUILayout.LabelField($"UI构建步骤 ({settings.uiSettings.selectedUIType})", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox($"定义{settings.uiSettings.selectedUIType}类型UI生成的步骤流程，按顺序执行。", MessageType.Info);
            buildStepsList.DoLayoutList();

            EditorGUILayout.Space(10);

            // 绘制偏好组件列表
            EditorGUILayout.LabelField($"UI环境说明 ({settings.uiSettings.selectedUIType})", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox($"配置{settings.uiSettings.selectedUIType}类型UI生成时的环境和约束条件。", MessageType.Info);
            preferredComponentsList.DoLayoutList();

            EditorGUILayout.EndScrollView();

            // 自动保存
            if (GUI.changed)
            {
                // 序列化UI类型数据
                settings.uiSettings.SerializeUITypeData();
                settings.SaveSettings();
            }
        }

        /// <summary>
        /// 将当前UI构建步骤写入到代码中
        /// </summary>
        private static void WriteDefaultBuildStepsToCode(List<string> buildSteps)
        {
            try
            {
                string filePath = System.IO.Path.Combine(Application.dataPath, "../Packages/unity-mcp/Editor/Provider/McpUISettingsProvider.cs");
                string fileContent = System.IO.File.ReadAllText(filePath);

                // 获取当前UI类型
                var settings = McpSettings.Instance;
                var currentType = settings?.uiSettings?.selectedUIType ?? UIType.UGUI;

                // 构建新的GetDefaultBuildSteps方法代码
                var newMethodCode = GenerateGetDefaultBuildStepsCode(buildSteps, currentType);

                // 找到方法开始位置
                var methodStart = "public static List<string> GetDefaultBuildSteps()";
                int startIndex = fileContent.IndexOf(methodStart);
                if (startIndex == -1)
                {
                    throw new System.Exception("找不到GetDefaultBuildSteps方法");
                }

                // 找到方法体的开始大括号
                int braceStart = fileContent.IndexOf('{', startIndex);
                if (braceStart == -1)
                {
                    throw new System.Exception("找不到方法开始大括号");
                }

                // 计数大括号找到方法结束位置
                int braceCount = 0;
                int braceEnd = braceStart;
                for (int i = braceStart; i < fileContent.Length; i++)
                {
                    if (fileContent[i] == '{') braceCount++;
                    else if (fileContent[i] == '}') braceCount--;

                    if (braceCount == 0)
                    {
                        braceEnd = i;
                        break;
                    }
                }

                // 替换整个方法
                string beforeMethod = fileContent.Substring(0, startIndex);
                string afterMethod = fileContent.Substring(braceEnd + 1);
                fileContent = beforeMethod + newMethodCode + afterMethod;

                System.IO.File.WriteAllText(filePath, fileContent);
                AssetDatabase.Refresh();

                EditorUtility.DisplayDialog("写入成功", "UI构建步骤已成功写入到代码中！", "确定");
            }
            catch (System.Exception ex)
            {
                EditorUtility.DisplayDialog("写入失败", $"写入过程中发生错误：{ex.Message}", "确定");
                Debug.LogError($"写入UI构建步骤失败: {ex}");
            }
        }

        /// <summary>
        /// 将当前UI环境说明写入到代码中
        /// </summary>
        private static void WriteDefaultBuildEnvironmentsToCode(List<string> environments)
        {
            try
            {
                string filePath = System.IO.Path.Combine(Application.dataPath, "../Packages/unity-mcp/Editor/Provider/McpUISettingsProvider.cs");
                string fileContent = System.IO.File.ReadAllText(filePath);

                // 获取当前UI类型
                var settings = McpSettings.Instance;
                var currentType = settings?.uiSettings?.selectedUIType ?? UIType.UGUI;

                // 构建新的GetDefaultBuildEnvironments方法代码
                var newMethodCode = GenerateGetDefaultBuildEnvironmentsCode(environments, currentType);

                // 找到方法开始位置
                var methodStart = "public static List<string> GetDefaultBuildEnvironments()";
                int startIndex = fileContent.IndexOf(methodStart);
                if (startIndex == -1)
                {
                    throw new System.Exception("找不到GetDefaultBuildEnvironments方法");
                }

                // 找到方法体的开始大括号
                int braceStart = fileContent.IndexOf('{', startIndex);
                if (braceStart == -1)
                {
                    throw new System.Exception("找不到方法开始大括号");
                }

                // 计数大括号找到方法结束位置
                int braceCount = 0;
                int braceEnd = braceStart;
                for (int i = braceStart; i < fileContent.Length; i++)
                {
                    if (fileContent[i] == '{') braceCount++;
                    else if (fileContent[i] == '}') braceCount--;

                    if (braceCount == 0)
                    {
                        braceEnd = i;
                        break;
                    }
                }

                // 替换整个方法
                string beforeMethod = fileContent.Substring(0, startIndex);
                string afterMethod = fileContent.Substring(braceEnd + 1);
                fileContent = beforeMethod + newMethodCode + afterMethod;

                System.IO.File.WriteAllText(filePath, fileContent);
                AssetDatabase.Refresh();

                EditorUtility.DisplayDialog("写入成功", "UI环境说明已成功写入到代码中！", "确定");
            }
            catch (System.Exception ex)
            {
                EditorUtility.DisplayDialog("写入失败", $"写入过程中发生错误：{ex.Message}", "确定");
                Debug.LogError($"写入UI环境说明失败: {ex}");
            }
        }

        /// <summary>
        /// 生成GetDefaultBuildSteps方法的代码
        /// </summary>
        private static string GenerateGetDefaultBuildStepsCode(List<string> buildSteps, UIType uiType)
        {
            var code = new System.Text.StringBuilder();
            code.AppendLine($"// 自动生成的默认构建步骤 - {uiType} ({System.DateTime.Now:yyyy-MM-dd HH:mm:ss})");
            code.AppendLine("        public static List<string> GetDefaultBuildSteps()");
            code.AppendLine("        {");
            code.AppendLine("            return new List<string>");
            code.AppendLine("            {");

            for (int i = 0; i < buildSteps.Count; i++)
            {
                var step = buildSteps[i].Replace("\"", "\\\""); // 转义双引号
                var comma = i < buildSteps.Count - 1 ? "," : "";
                code.AppendLine($"                \"{step}\"{comma}");
            }

            code.AppendLine("            };");
            code.Append("        }");

            return code.ToString();
        }

        /// <summary>
        /// 生成GetDefaultBuildEnvironments方法的代码
        /// </summary>
        private static string GenerateGetDefaultBuildEnvironmentsCode(List<string> environments, UIType uiType)
        {
            var code = new System.Text.StringBuilder();
            code.AppendLine($"// 自动生成的默认环境说明 - {uiType} ({System.DateTime.Now:yyyy-MM-dd HH:mm:ss})");
            code.AppendLine("        public static List<string> GetDefaultBuildEnvironments()");
            code.AppendLine("        {");
            code.AppendLine("            return new List<string>");
            code.AppendLine("            {");

            for (int i = 0; i < environments.Count; i++)
            {
                var env = environments[i].Replace("\"", "\\\""); // 转义双引号
                var comma = i < environments.Count - 1 ? "," : "";
                code.AppendLine($"                \"{env}\"{comma}");
            }

            code.AppendLine("            };");
            code.Append("        }");

            return code.ToString();
        }
    }
}
