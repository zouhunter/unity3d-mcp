using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityMcp.Models;
using UnityMcp.Tools;

namespace UnityMcp.Tools
{
    /// <summary>
    /// MCP调试客户端窗口 - 用于测试和调试MCP函数调用
    /// </summary>
    public class McpDebugWindow : EditorWindow
    {
        public static void ShowWindow()
        {
            McpDebugWindow window = GetWindow<McpDebugWindow>("MCP Debug Client");
            window.minSize = new Vector2(500, 300);
            window.Show();
        }

        /// <summary>
        /// 打开调试窗口并预填充指定的JSON内容
        /// </summary>
        /// <param name="jsonContent">要预填充的JSON内容</param>
        public static void ShowWindowWithContent(string jsonContent)
        {
            McpDebugWindow window = GetWindow<McpDebugWindow>("MCP Debug Client");
            window.minSize = new Vector2(500, 300);
            window.SetInputJson(jsonContent);
            window.Show();
            window.Focus();
        }

        /// <summary>
        /// 设置输入框的JSON内容
        /// </summary>
        /// <param name="jsonContent">JSON内容</param>
        public void SetInputJson(string jsonContent)
        {
            if (!string.IsNullOrEmpty(jsonContent))
            {
                inputJson = jsonContent;
                ClearResults(); // 清空之前的结果
                Repaint(); // 刷新界面
            }
        }

        // UI状态变量
        private Vector2 inputScrollPosition;
        private Vector2 resultScrollPosition;
        private string inputJson = "{\n  \"func\": \"hierarchy_create\",\n  \"args\": {\n    \"from\": \"primitive\",\n    \"primitive_type\": \"Cube\",\n    \"name\": \"RedCube\",\n    \"position\": [\n      0,\n      0,\n      0\n    ]\n  }\n}";
        private string resultText = "";
        private bool showResult = false;
        private bool isExecuting = false;
        private int currentExecutionIndex = 0; // 当前执行的任务索引
        private int totalExecutionCount = 0; // 总任务数

        private object currentResult = null; // 存储当前执行结果

        // 执行记录相关变量
        private ReorderableList recordList;
        private int selectedRecordIndex = -1;
        private Vector2 recordScrollPosition; // 记录列表滚动位置

        // 分组相关变量
        private bool showGroupManager = false; // 是否显示分组管理界面
        private string newGroupName = ""; // 新分组名称
        private string newGroupDescription = ""; // 新分组描述
        private Vector2 groupScrollPosition; // 分组列表滚动位置
        private int selectedGroupIndex = -1; // 选中的分组索引

        // 编辑相关变量
        private int editingRecordIndex = -1; // 当前正在编辑的记录索引
        private string editingText = ""; // 编辑中的文本
        private double lastClickTime = 0; // 上次点击时间，用于检测双击
        private int lastClickedIndex = -1; // 上次点击的索引
        private bool editingStarted = false; // 标记编辑是否刚开始

        // 分栏布局相关变量
        private float splitterPos = 0.3f; // 默认左侧占30%
        private bool isDraggingSplitter = false;
        private const float SplitterWidth = 4f;

        // 布局参数
        private const float MinInputHeight = 100f;
        private const float MaxInputHeight = 300f;
        private const float LineHeight = 16f;
        private const float ResultAreaHeight = 200f;

        // 样式
        private GUIStyle headerStyle;
        private GUIStyle codeStyle;
        private GUIStyle inputStyle;  // 专门用于输入框的样式
        private GUIStyle resultStyle;
        private MethodsCall methodsCall = new MethodsCall();
        private void InitializeStyles()
        {
            if (headerStyle == null)
            {
                headerStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    fontSize = 14,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = EditorGUIUtility.isProSkin ? Color.white : Color.black }
                };
            }

            if (codeStyle == null)
            {
                codeStyle = new GUIStyle(EditorStyles.textArea)
                {
                    wordWrap = true,  // 启用自动换行
                    fontSize = 12,
                    fontStyle = FontStyle.Normal,
                    stretchWidth = false,  // 不自动拉伸，使用固定宽度
                    stretchHeight = true   // 拉伸以适应容器高度
                };
            }

            if (inputStyle == null)
            {
                inputStyle = new GUIStyle(EditorStyles.textArea)
                {
                    wordWrap = true,        // 强制启用自动换行
                    fontSize = 12,
                    fontStyle = FontStyle.Normal,
                    normal = { textColor = EditorGUIUtility.isProSkin ? new Color(0.9f, 0.9f, 0.9f) : Color.black },
                    stretchWidth = false,   // 不自动拉伸宽度
                    stretchHeight = true,   // 允许高度拉伸
                    fixedWidth = 0,         // 不使用固定宽度
                    fixedHeight = 0,        // 不使用固定高度
                    margin = new RectOffset(2, 2, 2, 2),
                    padding = new RectOffset(4, 4, 4, 4)
                };
            }

            if (resultStyle == null)
            {
                resultStyle = new GUIStyle(EditorStyles.textArea)
                {
                    wordWrap = true,        // 启用自动换行
                    fontSize = 12,          // 与输入框保持一致的字体大小
                    fontStyle = FontStyle.Normal,
                    normal = { textColor = EditorGUIUtility.isProSkin ? new Color(0.9f, 0.9f, 0.9f) : Color.black },
                    richText = true,        // 支持富文本，方便显示格式化内容
                    stretchWidth = false,   // 与输入框保持一致，不自动拉伸宽度
                    stretchHeight = true,   // 拉伸以适应容器高度
                    margin = new RectOffset(2, 2, 2, 2),    // 与输入框保持一致的边距
                    padding = new RectOffset(4, 4, 4, 4)    // 与输入框保持一致的内边距
                };
            }

            InitializeRecordList();
        }

        private void InitializeRecordList()
        {
            if (recordList == null)
            {
                // 确保默认启用分组
                if (!McpExecuteRecordObject.instance.useGrouping)
                {
                    McpExecuteRecordObject.instance.useGrouping = true;
                    McpExecuteRecordObject.instance.InitializeDefaultGroup();
                    McpExecuteRecordObject.instance.saveRecords();
                }

                // 根据分组模式获取记录
                var records = McpExecuteRecordObject.instance.GetCurrentGroupRecords();
                recordList = new ReorderableList(records, typeof(McpExecuteRecordObject.McpExecuteRecord), false, true, false, true);

                recordList.drawHeaderCallback = (Rect rect) =>
                {
                    var records = McpExecuteRecordObject.instance.GetCurrentGroupRecords();
                    int successCount = records.Where(r => r.success).Count();
                    int errorCount = records.Count - successCount;
                    var recordObject = McpExecuteRecordObject.instance;

                    // 分组下拉框直接作为标题
                    var groups = recordObject.recordGroups;
                    if (groups.Count == 0)
                    {
                        EditorGUI.LabelField(rect, "暂无分组");
                    }
                    else
                    {
                        // 构建分组选项（包含统计信息）
                        string[] groupNames = groups.Select(g =>
                        {
                            string stats = recordObject.GetGroupStatistics(g.id);
                            return $"{g.name} ({stats})";
                        }).ToArray();

                        int currentIndex = groups.FindIndex(g => g.id == recordObject.currentGroupId);
                        if (currentIndex == -1) currentIndex = 0;

                        EditorGUI.BeginChangeCheck();
                        int newIndex = EditorGUI.Popup(rect, currentIndex, groupNames, EditorStyles.boldLabel);
                        if (EditorGUI.EndChangeCheck() && newIndex >= 0 && newIndex < groups.Count)
                        {
                            recordObject.SwitchToGroup(groups[newIndex].id);
                            recordList = null;
                            EditorApplication.delayCall += () =>
                            {
                                InitializeRecordList();
                                Repaint();
                            };
                        }
                    }
                };

                recordList.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) =>
                {
                    var records = McpExecuteRecordObject.instance.GetCurrentGroupRecords();
                    if (index >= 0 && index < records.Count)
                    {
                        var record = records[records.Count - 1 - index]; // 倒序显示
                        DrawRecordElement(rect, record, records.Count - 1 - index, isActive, isFocused);
                    }
                };

                recordList.onSelectCallback = (ReorderableList list) =>
                {
                    var records = McpExecuteRecordObject.instance.GetCurrentGroupRecords();
                    if (list.index >= 0 && list.index < records.Count)
                    {
                        int actualIndex = records.Count - 1 - list.index; // 转换为实际索引
                        SelectRecord(actualIndex);
                    }
                };

                recordList.onRemoveCallback = (ReorderableList list) =>
                {
                    var records = McpExecuteRecordObject.instance.GetCurrentGroupRecords();
                    if (list.index >= 0 && list.index < records.Count)
                    {
                        int actualIndex = records.Count - 1 - list.index;
                        if (EditorUtility.DisplayDialog("确认删除", $"确定要删除这条执行记录吗？\n函数: {records[actualIndex].name}", "删除", "取消"))
                        {
                            records.RemoveAt(actualIndex);
                            McpExecuteRecordObject.instance.saveRecords();
                            if (selectedRecordIndex == actualIndex)
                            {
                                selectedRecordIndex = -1;
                            }
                        }
                    }
                };

                recordList.elementHeight = 40f; // 设置元素高度
            }
        }

        /// <summary>
        /// 根据文本内容动态计算输入框高度（考虑自动换行和固定宽度）
        /// </summary>
        private float CalculateInputHeight()
        {
            if (string.IsNullOrEmpty(inputJson))
                return MinInputHeight;

            // 基础行数计算
            int basicLineCount = inputJson.Split('\n').Length;

            // 根据固定宽度估算换行，考虑字体大小和宽度限制
            // 估算每行可显示的字符数（基于12px字体和可用宽度）
            const int avgCharsPerLine = 60; // 保守估计，适应较窄的面板
            int totalChars = inputJson.Length;
            int estimatedWrappedLines = Mathf.CeilToInt((float)totalChars / avgCharsPerLine);

            // 取较大值作为实际行数估算，但给换行更多权重
            int estimatedLineCount = Mathf.Max(basicLineCount, (int)(estimatedWrappedLines * 0.8f));

            // 根据行数计算高度，加上适当的padding
            float calculatedHeight = estimatedLineCount * LineHeight + 40f; // 适当的padding

            // 限制在最小和最大高度之间
            return Mathf.Clamp(calculatedHeight, MinInputHeight, MaxInputHeight);
        }

        /// <summary>
        /// 计算标题区域的实际高度
        /// </summary>
        private float CalculateHeaderHeight()
        {
            // 标题文字高度（基于headerStyle的fontSize）
            float titleHeight = headerStyle?.fontSize ?? 14;
            titleHeight += 16; // 标题的上下边距，增加更多空间

            // 间距
            float spacing = 10; // 增加间距

            // 总高度，确保有足够空间显示标题
            return titleHeight + spacing + 10; // 增加额外边距
        }

        private void OnGUI()
        {
            InitializeStyles();

            // 计算标题区域的实际高度
            float headerHeight = CalculateHeaderHeight();

            // 分栏布局
            DrawSplitView(headerHeight);

            // 处理分栏拖拽
            HandleSplitterEvents(headerHeight);
        }

        private void DrawSplitView(float headerHeight)
        {
            Rect windowRect = new Rect(0, headerHeight, position.width, position.height - headerHeight);
            float leftWidth = windowRect.width * splitterPos;
            float rightWidth = windowRect.width * (1 - splitterPos) - SplitterWidth;

            // 左侧区域 - 执行记录
            Rect leftRect = new Rect(windowRect.x, windowRect.y, leftWidth, windowRect.height);
            DrawLeftPanel(leftRect);

            // 分隔条
            Rect splitterRect = new Rect(leftRect.xMax, windowRect.y, SplitterWidth, windowRect.height);
            DrawSplitter(splitterRect);

            // 右侧区域 - 原有功能
            Rect rightRect = new Rect(splitterRect.xMax, windowRect.y, rightWidth, windowRect.height);
            DrawRightPanel(headerHeight, rightRect);
        }

        private void DrawLeftPanel(Rect rect)
        {
            // 使用更精确的垂直布局
            float currentY = 5; // 恢复原来的起始位置
            float padding = 5;  // 恢复原来的内边距

            // 记录列表操作按钮区域
            Rect buttonRect = new Rect(padding, currentY, rect.width - padding * 2, 28);
            GUI.BeginGroup(buttonRect);
            GUILayout.BeginArea(new Rect(0, 0, buttonRect.width, buttonRect.height));

            GUILayout.BeginHorizontal();

            if (GUILayout.Button("刷新", GUILayout.Width(50)))
            {
                recordList = null;
                InitializeRecordList();
                Repaint();
            }

            if (GUILayout.Button("清空", GUILayout.Width(60)))
            {
                string confirmMessage = $"确定要清空当前分组 '{GetCurrentGroupDisplayName()}' 的所有记录吗？";

                if (EditorUtility.DisplayDialog("确认清空", confirmMessage, "确定", "取消"))
                {
                    McpExecuteRecordObject.instance.clearRecords();
                    McpExecuteRecordObject.instance.saveRecords();
                    selectedRecordIndex = -1;
                    recordList = null;
                    InitializeRecordList();
                }
            }

            if (GUILayout.Button(showGroupManager ? "隐藏" : "管理", GUILayout.Width(60)))
            {
                showGroupManager = !showGroupManager;
            }

            GUILayout.EndHorizontal();
            GUILayout.EndArea();
            GUI.EndGroup();
            currentY += 30;


            // 分组管理区域
            if (showGroupManager)
            {
                float groupManagerHeight = CalculateGroupManagerHeight();
                Rect groupManagerRect = new Rect(padding, currentY, rect.width - padding * 2, groupManagerHeight);
                GUI.BeginGroup(groupManagerRect);
                GUILayout.BeginArea(new Rect(0, 0, groupManagerRect.width, groupManagerRect.height));
                DrawGroupManager(groupManagerRect.width);
                GUILayout.EndArea();
                GUI.EndGroup();
                currentY += groupManagerHeight + padding;
            }

            // 记录列表区域
            if (recordList != null)
            {
                var records = McpExecuteRecordObject.instance.GetCurrentGroupRecords();
                recordList.list = records;

                float listContentHeight = recordList.GetHeight();
                float availableHeight = rect.height - currentY - padding;

                // 确保有最小高度
                if (availableHeight < 100)
                {
                    availableHeight = 100;
                }

                Rect scrollViewRect = new Rect(padding, currentY, rect.width - padding * 2, availableHeight);
                Rect scrollContentRect = new Rect(0, 0, scrollViewRect.width - 16, listContentHeight);

                recordScrollPosition = GUI.BeginScrollView(scrollViewRect, recordScrollPosition, scrollContentRect, false, true);
                recordList.DoList(new Rect(0, 0, scrollContentRect.width, listContentHeight));
                GUI.EndScrollView();
            }
        }

        private void DrawRightPanel(float headerHeight, Rect rect)
        {
            // 先绘制标题在顶部居中
            Rect titleRect = new Rect(rect.x, 0, position.width, headerHeight);
            GUI.BeginGroup(titleRect);
            GUILayout.BeginArea(new Rect(0, 0, titleRect.width, titleRect.height));
            GUILayout.Space(8); // 顶部间距
            GUILayout.Label("Unity MCP Debug Client", headerStyle);
            GUILayout.EndArea();
            GUI.EndGroup();


            GUILayout.BeginArea(rect);

            // 使用垂直布局组来控制整体宽度
            GUILayout.BeginVertical(GUILayout.MaxWidth(rect.width));

            // 说明文字
            EditorGUILayout.HelpBox(
                "输入单个函数调用:\n{\"func\": \"function_name\", \"args\": {...}}\n\n" +
                "或批量调用 (顺序执行):\n{\"funcs\": [{\"func\": \"...\", \"args\": {...}}, ...]}",
                MessageType.Info);

            GUILayout.Space(5);

            // JSON输入框区域
            DrawInputArea(rect.width);

            GUILayout.Space(10);

            // 操作按钮区域
            DrawControlButtons();

            GUILayout.Space(10);

            // 结果显示区域
            if (showResult)
            {
                DrawResultArea(rect.width);
            }

            GUILayout.EndVertical();
            GUILayout.EndArea();
        }

        private void DrawSplitter(Rect rect)
        {
            Color originalColor = GUI.color;
            GUI.color = EditorGUIUtility.isProSkin ? new Color(0.3f, 0.3f, 0.3f) : new Color(0.6f, 0.6f, 0.6f);
            GUI.DrawTexture(rect, EditorGUIUtility.whiteTexture);
            GUI.color = originalColor;

            EditorGUIUtility.AddCursorRect(rect, MouseCursor.ResizeHorizontal);
        }

        private void HandleSplitterEvents(float headerHeight)
        {
            Event e = Event.current;
            Rect windowRect = new Rect(0, headerHeight, position.width, position.height - headerHeight);
            float splitterX = windowRect.width * splitterPos;
            Rect splitterRect = new Rect(splitterX, headerHeight, SplitterWidth, windowRect.height);

            switch (e.type)
            {
                case EventType.MouseDown:
                    if (splitterRect.Contains(e.mousePosition))
                    {
                        isDraggingSplitter = true;
                        e.Use();
                    }
                    break;

                case EventType.MouseDrag:
                    if (isDraggingSplitter)
                    {
                        float newSplitterPos = e.mousePosition.x / position.width;
                        splitterPos = Mathf.Clamp(newSplitterPos, 0.2f, 0.8f); // 限制在20%-80%之间
                        Repaint();
                        e.Use();
                    }
                    break;

                case EventType.MouseUp:
                    if (isDraggingSplitter)
                    {
                        isDraggingSplitter = false;
                        e.Use();
                    }
                    break;
            }
        }

        private void DrawRecordElement(Rect rect, McpExecuteRecordObject.McpExecuteRecord record, int index, bool isActive, bool isFocused)
        {
            Color originalColor = GUI.color;

            // 添加padding - 每个元素都有padding
            const float padding = 6f;
            Rect paddedRect = new Rect(rect.x + padding, rect.y + padding, rect.width - padding * 2, rect.height - padding * 2);

            // 处理鼠标事件（双击检测）
            HandleRecordElementMouseEvents(rect, index);

            if (isActive || selectedRecordIndex == index)
            {
                // 选中时显示背景颜色（在原始rect上绘制，不受padding影响）
                GUI.color = new Color(0.3f, 0.7f, 1f, 0.3f); // 蓝色高亮
                GUI.DrawTexture(rect, EditorGUIUtility.whiteTexture);
            }
            else
            {
                // 未选中时绘制box边框
                Color boxColor = EditorGUIUtility.isProSkin ? new Color(0.4f, 0.4f, 0.4f) : new Color(0.7f, 0.7f, 0.7f);
                GUI.color = boxColor;
                GUI.Box(paddedRect, "", EditorStyles.helpBox);
            }

            GUI.color = originalColor;

            // 绘制内容（在box内部）
            const float numberWidth = 24f; // 序号宽度
            const float iconWidth = 20f;
            const float boxMargin = 4f; // box内部边距

            // 计算box内部的绘制区域
            Rect contentRect = new Rect(paddedRect.x + boxMargin, paddedRect.y + boxMargin,
                paddedRect.width - boxMargin * 2, paddedRect.height - boxMargin * 2);

            // 序号显示（左上角，在box内部）
            var records = McpExecuteRecordObject.instance.GetCurrentGroupRecords();
            int displayIndex = index + 1; // 正序显示序号，从1开始
            Rect numberRect = new Rect(contentRect.x, contentRect.y, numberWidth, 14f);
            Color numberColor = EditorGUIUtility.isProSkin ? new Color(0.6f, 0.6f, 0.6f) : new Color(0.5f, 0.5f, 0.5f);
            Color originalContentColor = GUI.contentColor;
            GUI.contentColor = numberColor;
            GUI.Label(numberRect, $"#{displayIndex}", EditorStyles.miniLabel);
            GUI.contentColor = originalContentColor;

            // 状态图标（在box内部）
            string statusIcon = record.success ? "●" : "×";
            Rect iconRect = new Rect(contentRect.x + numberWidth + 2f, contentRect.y, iconWidth, 16f);

            // 为状态图标设置颜色
            Color iconColor = record.success ? Color.green : Color.red;
            GUI.contentColor = iconColor;
            GUI.Label(iconRect, statusIcon, EditorStyles.boldLabel);
            GUI.contentColor = originalContentColor;

            // 函数名（第一行）- 在box内部，为序号和图标留出空间
            Rect funcRect = new Rect(contentRect.x + numberWidth + iconWidth + 4f, contentRect.y,
                contentRect.width - numberWidth - iconWidth - 4f, 16f);

            // 检查是否正在编辑此记录
            if (editingRecordIndex == index)
            {
                // 编辑模式：显示文本输入框
                GUI.SetNextControlName($"RecordEdit_{index}");

                // 先处理键盘事件，确保优先级
                if (Event.current.type == EventType.KeyDown)
                {
                    if (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter)
                    {
                        // 回车确认编辑
                        FinishEditing(index, editingText);
                        Event.current.Use();
                        return; // 直接返回，避免继续处理其他事件
                    }
                    else if (Event.current.keyCode == KeyCode.Escape)
                    {
                        // ESC取消编辑
                        CancelEditing();
                        Event.current.Use();
                        return; // 直接返回，避免继续处理其他事件
                    }
                }

                // 设置焦点（只在刚开始编辑时设置）
                if (editingStarted)
                {
                    GUI.FocusControl($"RecordEdit_{index}");
                    editingStarted = false;
                }

                // 使用BeginChangeCheck来检测文本变化
                EditorGUI.BeginChangeCheck();
                string newName = EditorGUI.TextField(funcRect, editingText);
                if (EditorGUI.EndChangeCheck())
                {
                    editingText = newName;
                }

                // 检测失去焦点
                if (Event.current.type == EventType.Repaint)
                {
                    string focusedControl = GUI.GetNameOfFocusedControl();
                    if (string.IsNullOrEmpty(focusedControl) || focusedControl != $"RecordEdit_{index}")
                    {
                        // 延迟一帧检查，避免刚设置焦点就检测到失去焦点
                        EditorApplication.delayCall += () =>
                        {
                            if (editingRecordIndex == index && GUI.GetNameOfFocusedControl() != $"RecordEdit_{index}")
                            {
                                FinishEditing(index, editingText);
                            }
                        };
                    }
                }
            }
            else
            {
                // 正常模式：显示函数名
                GUI.Label(funcRect, record.name, EditorStyles.boldLabel);
            }

            // 时间和来源（第二行）- 在box内部
            Rect timeRect = new Rect(contentRect.x + numberWidth + iconWidth + 4f, contentRect.y + 18f,
                contentRect.width - numberWidth - iconWidth - 4f, 14f);
            string timeInfo = $"{record.timestamp} | [{record.source}]";
            if (record.duration > 0)
            {
                timeInfo += $" | {record.duration:F1}ms";
            }

            // 为时间信息设置较淡的颜色
            Color timeColor = EditorGUIUtility.isProSkin ? new Color(0.8f, 0.8f, 0.8f) : new Color(0.4f, 0.4f, 0.4f);
            GUI.contentColor = timeColor;
            GUI.Label(timeRect, timeInfo, EditorStyles.miniLabel);
            GUI.contentColor = originalContentColor;
        }

        /// <summary>
        /// 绘制输入区域（带滚动和动态高度）
        /// </summary>
        private void DrawInputArea(float availableWidth)
        {
            GUILayout.Label("MCP调用 (JSON格式):");

            float inputHeight = CalculateInputHeight();
            float textAreaWidth = availableWidth; // 减去边距和滚动条宽度

            // 创建输入框的滚动区域，限制宽度避免水平滚动
            GUILayout.BeginVertical(EditorStyles.helpBox);
            inputScrollPosition = EditorGUILayout.BeginScrollView(
                inputScrollPosition,
                false, true,  // 禁用水平滚动条，启用垂直滚动条
                GUILayout.Height(inputHeight),
                GUILayout.ExpandWidth(true)
            );

            // 输入框，使用专门的输入样式确保自动换行
            inputJson = EditorGUILayout.TextArea(
                inputJson,
                inputStyle,
                GUILayout.ExpandHeight(true),
                GUILayout.Width(textAreaWidth),
                GUILayout.MaxWidth(textAreaWidth)  // 确保不会超过指定宽度
            );

            EditorGUILayout.EndScrollView();
            GUILayout.EndVertical();

            // 显示行数信息
            int lineCount = inputJson?.Split('\n').Length ?? 0;
            GUILayout.Label($"行数: {lineCount} | 高度: {inputHeight:F0}px", EditorStyles.miniLabel);
        }

        /// <summary>
        /// 绘制控制按钮区域
        /// </summary>
        private void DrawControlButtons()
        {
            // 获取剪贴板可用性
            bool clipboardAvailable = IsClipboardAvailable();

            // 第一行按钮
            GUILayout.BeginHorizontal();

            GUI.enabled = !isExecuting;
            if (GUILayout.Button("执行", GUILayout.Height(30), GUILayout.Width(100)))
            {
                ExecuteCall();
            }

            GUI.enabled = !isExecuting && clipboardAvailable;
            if (GUILayout.Button("执行剪贴板", GUILayout.Height(30), GUILayout.Width(100)))
            {
                ExecuteClipboard();
            }
            GUI.enabled = true;

            if (GUILayout.Button("格式化JSON", GUILayout.Height(30), GUILayout.Width(120)))
            {
                FormatJson();
            }

            if (GUILayout.Button("清空", GUILayout.Height(30), GUILayout.Width(60)))
            {
                inputJson = "{}";
                ClearResults();
            }

            if (isExecuting)
            {
                if (totalExecutionCount > 1)
                {
                    GUILayout.Label($"执行中... ({currentExecutionIndex}/{totalExecutionCount})", GUILayout.Width(150));
                }
                else
                {
                    GUILayout.Label("执行中...", GUILayout.Width(100));
                }
            }

            GUILayout.EndHorizontal();

            // 第二行按钮（剪贴板操作）
            GUILayout.BeginHorizontal();

            // 剪贴板操作按钮 - 根据剪贴板内容动态启用/禁用
            GUI.enabled = clipboardAvailable;
            if (GUILayout.Button("粘贴到输入框", GUILayout.Height(25), GUILayout.Width(100)))
            {
                PasteFromClipboard();
            }

            if (GUILayout.Button("预览剪贴板", GUILayout.Height(25), GUILayout.Width(100)))
            {
                PreviewClipboard();
            }
            GUI.enabled = true;

            // 显示剪贴板状态 - 带颜色指示
            DrawClipboardStatus();

            GUILayout.EndHorizontal();
        }

        /// <summary>
        /// 绘制结果显示区域（带滚动）
        /// </summary>
        private void DrawResultArea(float availableWidth)
        {
            EditorGUILayout.LabelField("执行结果", EditorStyles.boldLabel);

            float textAreaWidth = availableWidth - 40; // 减去边距和滚动条宽度

            // 创建结果显示的滚动区域，限制宽度避免水平滚动
            GUILayout.BeginVertical(EditorStyles.helpBox);
            resultScrollPosition = EditorGUILayout.BeginScrollView(
                resultScrollPosition,
                false, true,  // 禁用水平滚动条，启用垂直滚动条
                GUILayout.Height(ResultAreaHeight),
                GUILayout.MaxWidth(availableWidth)
            );

            // 结果文本区域，限制宽度以防止水平溢出
            EditorGUILayout.TextArea(
                resultText,
                resultStyle,
                GUILayout.ExpandHeight(true),
                GUILayout.Width(textAreaWidth)
            );

            EditorGUILayout.EndScrollView();
            GUILayout.EndVertical();

            // 结果操作按钮
            GUILayout.BeginHorizontal();

            // 记录结果按钮 - 只有当有执行结果且不是从历史记录加载时才显示
            if (currentResult != null && !string.IsNullOrEmpty(inputJson))
            {
                if (GUILayout.Button("记录结果", GUILayout.Width(80)))
                {
                    RecordCurrentResult();
                }
            }

            // 格式化结果按钮 - 只有当有结果文本时才显示
            if (!string.IsNullOrEmpty(resultText))
            {
                if (GUILayout.Button("格式化结果", GUILayout.Width(80)))
                {
                    FormatResultText();
                }
            }

            // 检查是否为批量结果，如果是则显示额外操作
            if (IsBatchResultDisplayed())
            {
                if (GUILayout.Button("复制统计", GUILayout.Width(80)))
                {
                    CopyBatchStatistics();
                }

                if (GUILayout.Button("仅显示错误", GUILayout.Width(80)))
                {
                    ShowOnlyErrors();
                }
            }

            GUILayout.EndHorizontal();
        }

        private void FormatJson()
        {
            try
            {
                JObject jsonObj = JObject.Parse(inputJson);
                inputJson = JsonConvert.SerializeObject(jsonObj, Formatting.Indented);
            }
            catch (Exception e)
            {
                EditorUtility.DisplayDialog("JSON格式错误", $"无法解析JSON: {e.Message}", "确定");
            }
        }

        /// <summary>
        /// 格式化结果文本，支持JSON和YAML
        /// </summary>
        private void FormatResultText()
        {
            if (string.IsNullOrEmpty(resultText))
            {
                EditorUtility.DisplayDialog("提示", "没有可格式化的结果", "确定");
                return;
            }

            try
            {
                StringBuilder formattedResult = new StringBuilder();
                string[] lines = resultText.Split(new[] { '\n', '\r' }, StringSplitOptions.None);

                int i = 0;
                while (i < lines.Length)
                {
                    string line = lines[i];
                    if (string.IsNullOrEmpty(line))
                    {
                        i++;
                        continue;
                    }

                    // 检查当前行是否是 JSON 对象或数组的开始
                    string trimmedLine = line.TrimStart();

                    if (trimmedLine.StartsWith("{") || trimmedLine.StartsWith("["))
                    {
                        // 收集完整的 JSON 块
                        string jsonBlock = CollectJsonBlock(lines, ref i);

                        // 尝试格式化 JSON
                        string formattedJson = TryFormatJson(jsonBlock);
                        if (formattedJson != null)
                        {
                            formattedResult.AppendLine(formattedJson);
                        }
                        else
                        {
                            // 格式化失败，保持原样
                            formattedResult.AppendLine(jsonBlock);
                        }
                    }
                    else if (!string.IsNullOrEmpty(line))
                    {
                        // 非 JSON 行，检查是否包含内嵌 JSON
                        string processedLine = ProcessLineWithEmbeddedJson(line);
                        formattedResult.AppendLine(processedLine);
                        i++;
                    }
                    else
                    {
                        // 空行，保持原样
                        formattedResult.AppendLine(line);
                        i++;
                    }
                }

                resultText = formattedResult.ToString().TrimEnd();
                Repaint();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[McpDebugWindow] 格式化结果时发生错误: {e.Message}");
                EditorUtility.DisplayDialog("格式化失败", $"无法格式化结果: {e.Message}", "确定");
            }
        }

        /// <summary>
        /// 收集完整的 JSON 块（处理多行 JSON）
        /// </summary>
        private string CollectJsonBlock(string[] lines, ref int startIndex)
        {
            StringBuilder jsonBlock = new StringBuilder();
            int braceCount = 0;
            int bracketCount = 0;
            bool inString = false;
            bool isFirstLine = true;

            for (int i = startIndex; i < lines.Length; i++)
            {
                string line = lines[i];
                jsonBlock.AppendLine(line);

                // 计算括号平衡
                foreach (char c in line)
                {
                    if (c == '"' && (jsonBlock.Length == 0 || jsonBlock[jsonBlock.Length - 2] != '\\'))
                    {
                        inString = !inString;
                    }

                    if (!inString)
                    {
                        if (c == '{') braceCount++;
                        else if (c == '}') braceCount--;
                        else if (c == '[') bracketCount++;
                        else if (c == ']') bracketCount--;
                    }
                }

                // 检查是否完成
                if (!isFirstLine && braceCount == 0 && bracketCount == 0)
                {
                    startIndex = i + 1;
                    return jsonBlock.ToString().TrimEnd();
                }

                isFirstLine = false;
            }

            startIndex = lines.Length;
            return jsonBlock.ToString().TrimEnd();
        }

        /// <summary>
        /// 处理包含内嵌 JSON 的行（如 "执行结果: {...}"）
        /// </summary>
        private string ProcessLineWithEmbeddedJson(string line)
        {
            // 查找第一个 { 或 [ 的位置
            int jsonStartIndex = -1;
            for (int i = 0; i < line.Length; i++)
            {
                if (line[i] == '{' || line[i] == '[')
                {
                    jsonStartIndex = i;
                    break;
                }
            }

            if (jsonStartIndex > 0)
            {
                // 有前缀文本和 JSON
                string prefix = line.Substring(0, jsonStartIndex);
                string jsonPart = line.Substring(jsonStartIndex);

                // 尝试格式化 JSON 部分
                string formattedJson = TryFormatJson(jsonPart);
                if (formattedJson != null)
                {
                    // 成功格式化，返回前缀 + 格式化后的 JSON（缩进对齐）
                    string[] jsonLines = formattedJson.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                    StringBuilder result = new StringBuilder();
                    result.AppendLine(prefix);
                    foreach (string jsonLine in jsonLines)
                    {
                        result.AppendLine(jsonLine);
                    }
                    return result.ToString().TrimEnd();
                }
            }

            return line;
        }

        /// <summary>
        /// 尝试将文本格式化为JSON
        /// </summary>
        private string TryFormatJson(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return null;

            try
            {
                // 尝试作为JObject解析
                try
                {
                    JObject jsonObj = JObject.Parse(text);
                    string formatted = JsonConvert.SerializeObject(jsonObj, Formatting.Indented);

                    // 序列化后展开 yaml 字段中的 \n
                    return ExpandYamlInFormattedJson(formatted);
                }
                catch
                {
                    // 尝试作为JArray解析
                    JArray jsonArray = JArray.Parse(text);
                    string formatted = JsonConvert.SerializeObject(jsonArray, Formatting.Indented);

                    // 序列化后展开 yaml 字段中的 \n
                    return ExpandYamlInFormattedJson(formatted);
                }
            }
            catch
            {
                // 不是有效的JSON
                return null;
            }
        }

        /// <summary>
        /// 在格式化后的 JSON 文本中展开 yaml 字段的换行符
        /// </summary>
        private string ExpandYamlInFormattedJson(string formattedJson)
        {
            if (string.IsNullOrEmpty(formattedJson))
                return formattedJson;

            // 使用正则表达式查找 "yaml": "..." 模式
            // 匹配 "yaml": "内容"（包括多行的情况）
            string pattern = @"""yaml"":\s*""([^""\\]*(\\.[^""\\]*)*)""";

            return System.Text.RegularExpressions.Regex.Replace(formattedJson, pattern, match =>
            {
                string fullMatch = match.Value;
                string yamlContent = match.Groups[1].Value;

                // 只有包含 \n 才处理
                if (!yamlContent.Contains("\\n"))
                    return fullMatch;

                // 展开 \n 为实际换行，并保持 JSON 的缩进
                string expandedYaml = yamlContent.Replace("\\n", "\n");

                // 获取当前的缩进级别
                int indentLevel = GetIndentLevel(formattedJson, match.Index);
                string indent = new string(' ', indentLevel + 2); // +2 是因为字符串内容需要额外缩进

                // 为 yaml 内容的每一行添加适当的缩进（除了第一行）
                string[] lines = expandedYaml.Split('\n');
                for (int i = 1; i < lines.Length; i++)
                {
                    lines[i] = indent + lines[i];
                }
                expandedYaml = string.Join("\n", lines);

                // 返回格式化后的结果
                return $"\"yaml\": \"{expandedYaml}\"";
            });
        }

        /// <summary>
        /// 获取指定位置的缩进级别
        /// </summary>
        private int GetIndentLevel(string text, int position)
        {
            // 向前查找到行首
            int lineStart = position;
            while (lineStart > 0 && text[lineStart - 1] != '\n' && text[lineStart - 1] != '\r')
            {
                lineStart--;
            }

            // 计算缩进空格数
            int indent = 0;
            for (int i = lineStart; i < position && i < text.Length; i++)
            {
                if (text[i] == ' ')
                    indent++;
                else if (text[i] == '\t')
                    indent += 4; // 制表符算作4个空格
                else
                    break;
            }

            return indent;
        }

        /// <summary>
        /// 尝试格式化YAML文本
        /// </summary>
        private string TryFormatYaml(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return null;

            try
            {
                // 检测YAML特征：包含冒号和换行，但不是JSON格式
                if (!text.Contains(":") || text.TrimStart().StartsWith("{") || text.TrimStart().StartsWith("["))
                    return null;

                // 简单的YAML格式化：确保每个键值对单独一行，适当缩进
                string[] lines = text.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                StringBuilder formatted = new StringBuilder();

                int indentLevel = 0;
                foreach (string line in lines)
                {
                    string trimmedLine = line.Trim();
                    if (string.IsNullOrWhiteSpace(trimmedLine))
                        continue;

                    // 检测缩进变化
                    if (trimmedLine.Contains(":"))
                    {
                        // 键值对
                        string[] parts = trimmedLine.Split(new[] { ':' }, 2);
                        if (parts.Length == 2)
                        {
                            string key = parts[0].Trim();
                            string value = parts[1].Trim();

                            // 如果值为空或是列表/对象开始，可能需要增加缩进
                            if (string.IsNullOrEmpty(value) || value == "[" || value == "{")
                            {
                                formatted.AppendLine($"{new string(' ', indentLevel * 2)}{key}:");
                                if (value == "[" || value == "{")
                                {
                                    indentLevel++;
                                }
                            }
                            else
                            {
                                formatted.AppendLine($"{new string(' ', indentLevel * 2)}{key}: {value}");
                            }
                        }
                        else
                        {
                            formatted.AppendLine($"{new string(' ', indentLevel * 2)}{trimmedLine}");
                        }
                    }
                    else if (trimmedLine == "]" || trimmedLine == "}")
                    {
                        indentLevel = Math.Max(0, indentLevel - 1);
                        formatted.AppendLine($"{new string(' ', indentLevel * 2)}{trimmedLine}");
                    }
                    else if (trimmedLine.StartsWith("-"))
                    {
                        // 列表项
                        formatted.AppendLine($"{new string(' ', indentLevel * 2)}{trimmedLine}");
                    }
                    else
                    {
                        formatted.AppendLine($"{new string(' ', indentLevel * 2)}{trimmedLine}");
                    }
                }

                string result = formatted.ToString().TrimEnd();

                // 如果格式化后与原文本差异很大，可能不是YAML，返回null
                if (result.Length > text.Length * 2 || result.Length < text.Length / 2)
                    return null;

                return result;
            }
            catch
            {
                return null;
            }
        }

        private void ExecuteCall()
        {
            if (string.IsNullOrWhiteSpace(inputJson))
            {
                EditorUtility.DisplayDialog("错误", "请输入JSON内容", "确定");
                return;
            }

            isExecuting = true;
            showResult = true;
            resultText = "正在执行...";

            try
            {
                DateTime startTime = DateTime.Now;
                object result = ExecuteJsonCall(startTime);

                // 如果结果为null，表示异步执行
                if (result == null)
                {
                    resultText = "异步执行中...";
                    // 刷新界面显示异步状态
                    Repaint();
                    // 注意：isExecuting保持为true，等待异步回调完成
                }
                else
                {
                    // 同步执行完成
                    DateTime endTime = DateTime.Now;
                    TimeSpan duration = endTime - startTime;
                    CompleteExecution(result, duration);
                }
            }
            catch (Exception e)
            {
                string errorResult = $"执行错误:\n{e.Message}\n\n堆栈跟踪:\n{e.StackTrace}";
                resultText = errorResult;
                isExecuting = false;

                Debug.LogException(new Exception("ExecuteCall error", e));
            }
        }

        /// <summary>
        /// 完成执行并更新UI显示
        /// </summary>
        private void CompleteExecution(object result, TimeSpan duration)
        {

            try
            {
                // 存储当前结果并格式化
                currentResult = result;
                string formattedResult = FormatResult(result, duration);
                resultText = formattedResult;

                // 刷新界面
                Repaint();
            }
            catch (Exception e)
            {
                Debug.LogException(new Exception("CompleteExecution error", e));
            }
            finally
            {
                isExecuting = false;
            }
        }

        private object ExecuteJsonCall(DateTime startTime)
        {
            return ExecuteJsonCallInternal(inputJson, startTime, (result, duration) =>
            {
                CompleteExecution(result, duration);
            });
        }

        /// <summary>
        /// 执行JSON调用的内部通用方法
        /// </summary>
        private object ExecuteJsonCallInternal(string jsonString, DateTime startTime, System.Action<object, TimeSpan> onSingleComplete)
        {
            JObject inputObj = JObject.Parse(jsonString);

            // 检查是否为批量调用
            if (inputObj.ContainsKey("func") && inputObj["func"].ToString() == "batch_call")
            {
                // 批量函数调用
                var functionsCall = new BatchCall();
                object callResult = null;
                bool callbackExecuted = false;
                functionsCall.HandleCommand(inputObj, (result) =>
                {
                    callResult = result;
                    callbackExecuted = true;

                    // 如果是异步回调，更新UI
                    if (isExecuting)
                    {
                        DateTime endTime = DateTime.Now;
                        TimeSpan duration = endTime - startTime;
                        onSingleComplete?.Invoke(result, duration);
                    }
                });
                // 如果回调立即执行，返回结果；否则返回null表示异步执行
                return callbackExecuted ? callResult : null;
            }
            else if (inputObj.ContainsKey("func"))
            {
                // 单个函数调用
                var functionCall = new SingleCall();
                object callResult = null;
                bool callbackExecuted = false;

                functionCall.HandleCommand(inputObj, (result) =>
                {
                    callResult = result;
                    callbackExecuted = true;

                    // 如果是异步回调，更新UI
                    if (isExecuting)
                    {
                        DateTime endTime = DateTime.Now;
                        TimeSpan duration = endTime - startTime;
                        onSingleComplete?.Invoke(result, duration);
                    }
                });

                // 如果回调立即执行，返回结果；否则返回null表示异步执行
                return callbackExecuted ? callResult : null;
            }
            else
            {
                throw new ArgumentException("输入的JSON必须包含 'func' 字段（单个调用）或 'funcs' 字段（批量调用）");
            }
        }


        private string FormatResult(object result, TimeSpan duration)
        {
            string formattedResult = $"执行时间: {duration.TotalMilliseconds:F2}ms\n\n";

            if (result != null)
            {
                // 检查是否为批量调用结果
                if (IsBatchCallResult(result))
                {
                    formattedResult += FormatBatchCallResult(result);
                }
                else
                {
                    // 单个结果处理
                    if (result.GetType().Name == "Response" || result.ToString().Contains("\"success\""))
                    {
                        try
                        {
                            string jsonResult = JsonConvert.SerializeObject(result, Formatting.Indented);
                            formattedResult += jsonResult;
                        }
                        catch
                        {
                            formattedResult += result.ToString();
                        }
                    }
                    else
                    {
                        formattedResult += result.ToString();
                    }
                }
            }
            else
            {
                formattedResult += "null";
            }

            return formattedResult;
        }

        /// <summary>
        /// 检查是否为批量调用结果
        /// </summary>
        private bool IsBatchCallResult(object result)
        {
            try
            {
                var resultJson = JsonConvert.SerializeObject(result);
                var resultObj = JObject.Parse(resultJson);

                return resultObj.ContainsKey("results") &&
                       resultObj.ContainsKey("total_calls") &&
                       resultObj.ContainsKey("successful_calls") &&
                       resultObj.ContainsKey("failed_calls");
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 格式化批量调用结果，分条显示
        /// </summary>
        private string FormatBatchCallResult(object result)
        {
            try
            {
                var resultJson = JsonConvert.SerializeObject(result);
                var resultObj = JObject.Parse(resultJson);

                var results = resultObj["results"] as JArray;
                var errors = resultObj["errors"] as JArray;
                var totalCalls = resultObj["total_calls"]?.Value<int>() ?? 0;
                var successfulCalls = resultObj["successful_calls"]?.Value<int>() ?? 0;
                var failedCalls = resultObj["failed_calls"]?.Value<int>() ?? 0;
                var overallSuccess = resultObj["success"]?.Value<bool>() ?? false;

                var output = new StringBuilder();

                // 显示总体统计
                output.AppendLine("=== 批量调用执行结果 ===");
                output.AppendLine($"总调用数: {totalCalls}");
                output.AppendLine($"成功: {successfulCalls}");
                output.AppendLine($"失败: {failedCalls}");
                output.AppendLine($"整体状态: {(overallSuccess ? "成功" : "部分失败")}");
                output.AppendLine();

                // 分条显示每个结果
                if (results != null)
                {
                    for (int i = 0; i < results.Count; i++)
                    {
                        output.AppendLine($"--- 调用 #{i + 1} ---");

                        var singleResult = results[i];
                        if (singleResult != null && !singleResult.Type.Equals(JTokenType.Null))
                        {
                            output.AppendLine("✅ 成功");
                            try
                            {
                                string formattedSingleResult = JsonConvert.SerializeObject(singleResult, Formatting.Indented);
                                output.AppendLine(formattedSingleResult);
                            }
                            catch
                            {
                                output.AppendLine(singleResult.ToString());
                            }
                        }
                        else
                        {
                            output.AppendLine("❌ 失败");
                            if (errors != null && i < errors.Count)
                            {
                                output.AppendLine($"错误信息: {errors[i]}");
                            }
                        }
                        output.AppendLine();
                    }
                }

                // 显示所有错误汇总
                if (errors != null && errors.Count > 0)
                {
                    output.AppendLine("=== 错误汇总 ===");
                    for (int i = 0; i < errors.Count; i++)
                    {
                        if (errors[i] != null && !string.IsNullOrEmpty(errors[i].ToString()))
                        {
                            output.AppendLine($"{i + 1}. {errors[i]}");
                        }
                    }
                }

                return output.ToString();
            }
            catch (Exception e)
            {
                return $"批量结果格式化失败: {e.Message}\n\n原始结果:\n{JsonConvert.SerializeObject(result, Formatting.Indented)}";
            }
        }

        /// <summary>
        /// 清理所有结果
        /// </summary>
        private void ClearResults()
        {
            resultText = "";
            showResult = false;
            currentResult = null;
            currentExecutionIndex = 0;
            totalExecutionCount = 0;
        }

        /// <summary>
        /// 检查当前显示的是否为批量结果
        /// </summary>
        private bool IsBatchResultDisplayed()
        {
            return currentResult != null && IsBatchCallResult(currentResult);
        }

        /// <summary>
        /// 复制批量调用的统计信息
        /// </summary>
        private void CopyBatchStatistics()
        {
            if (currentResult == null || !IsBatchCallResult(currentResult))
                return;

            try
            {
                var resultJson = JsonConvert.SerializeObject(currentResult);
                var resultObj = JObject.Parse(resultJson);

                var totalCalls = resultObj["total_calls"]?.Value<int>() ?? 0;
                var successfulCalls = resultObj["successful_calls"]?.Value<int>() ?? 0;
                var failedCalls = resultObj["failed_calls"]?.Value<int>() ?? 0;
                var overallSuccess = resultObj["success"]?.Value<bool>() ?? false;

                var statistics = $"批量调用统计:\n" +
                               $"总调用数: {totalCalls}\n" +
                               $"成功: {successfulCalls}\n" +
                               $"失败: {failedCalls}\n" +
                               $"整体状态: {(overallSuccess ? "成功" : "部分失败")}";

                EditorGUIUtility.systemCopyBuffer = statistics;
                EditorUtility.DisplayDialog("已复制", "统计信息已复制到剪贴板", "确定");
            }
            catch (Exception e)
            {
                EditorUtility.DisplayDialog("复制失败", $"无法复制统计信息: {e.Message}", "确定");
            }
        }

        /// <summary>
        /// 仅显示错误信息
        /// </summary>
        private void ShowOnlyErrors()
        {
            if (currentResult == null || !IsBatchCallResult(currentResult))
                return;

            try
            {
                var resultJson = JsonConvert.SerializeObject(currentResult);
                var resultObj = JObject.Parse(resultJson);

                var errors = resultObj["errors"] as JArray;
                var failedCalls = resultObj["failed_calls"]?.Value<int>() ?? 0;

                var output = new StringBuilder();
                output.AppendLine("=== 错误信息汇总 ===");
                output.AppendLine($"失败调用数: {failedCalls}");
                output.AppendLine();

                if (errors != null && errors.Count > 0)
                {
                    for (int i = 0; i < errors.Count; i++)
                    {
                        if (errors[i] != null && !string.IsNullOrEmpty(errors[i].ToString()))
                        {
                            output.AppendLine($"错误 #{i + 1}:");
                            output.AppendLine($"  {errors[i]}");
                            output.AppendLine();
                        }
                    }
                }
                else
                {
                    output.AppendLine("没有发现错误信息。");
                }

                resultText = output.ToString();
            }
            catch (Exception e)
            {
                resultText = $"显示错误信息失败: {e.Message}";
            }
        }

        /// <summary>
        /// 执行剪贴板中的JSON内容
        /// </summary>
        private void ExecuteClipboard()
        {
            try
            {
                string clipboardContent = EditorGUIUtility.systemCopyBuffer;

                if (string.IsNullOrWhiteSpace(clipboardContent))
                {
                    EditorUtility.DisplayDialog("错误", "剪贴板为空", "确定");
                    return;
                }

                // 验证JSON格式
                if (!ValidateClipboardJson(clipboardContent, out string errorMessage))
                {
                    EditorUtility.DisplayDialog("JSON格式错误", $"剪贴板内容不是有效的JSON:\n{errorMessage}", "确定");
                    return;
                }

                // 执行剪贴板内容
                isExecuting = true;
                showResult = true;
                resultText = "正在执行剪贴板内容...";

                try
                {
                    DateTime startTime = DateTime.Now;
                    object result = ExecuteJsonCallFromString(clipboardContent, startTime);

                    // 如果结果为null，表示异步执行
                    if (result == null)
                    {
                        resultText = "异步执行剪贴板内容中...";
                        // 刷新界面显示异步状态
                        Repaint();
                        // 注意：isExecuting保持为true，等待异步回调完成
                    }
                    else
                    {
                        // 同步执行完成
                        DateTime endTime = DateTime.Now;
                        TimeSpan duration = endTime - startTime;

                        // 存储当前结果并格式化
                        currentResult = result;
                        string formattedResult = FormatResult(result, duration);
                        resultText = $"📋 从剪贴板执行\n原始JSON:\n{clipboardContent}\n\n{formattedResult}";

                        // 刷新界面
                        Repaint();
                        isExecuting = false;
                    }
                }
                catch (Exception e)
                {
                    string errorResult = $"执行剪贴板内容错误:\n{e.Message}\n\n堆栈跟踪:\n{e.StackTrace}";
                    resultText = errorResult;

                    Debug.LogError($"[McpDebugWindow] 执行剪贴板内容时发生错误: {e}");
                }
                finally
                {
                    isExecuting = false;
                }
            }
            catch (Exception e)
            {
                EditorUtility.DisplayDialog("执行失败", $"无法执行剪贴板内容: {e.Message}", "确定");
                isExecuting = false;
            }
        }

        /// <summary>
        /// 从字符串执行JSON调用，支持异步回调
        /// </summary>
        private object ExecuteJsonCallFromString(string jsonString, DateTime startTime)
        {
            return ExecuteJsonCallInternal(jsonString, startTime, (result, duration) =>
            {
                // 剪贴板格式的UI更新
                currentResult = result;
                string formattedResult = FormatResult(result, duration);
                resultText = $"📋 从剪贴板执行\n原始JSON:\n{jsonString}\n\n{formattedResult}";

                // 刷新界面
                Repaint();
                isExecuting = false;
            });
        }



        /// <summary>
        /// 粘贴剪贴板内容到输入框
        /// </summary>
        private void PasteFromClipboard()
        {
            try
            {
                string clipboardContent = EditorGUIUtility.systemCopyBuffer;

                if (string.IsNullOrWhiteSpace(clipboardContent))
                {
                    EditorUtility.DisplayDialog("提示", "剪贴板为空", "确定");
                    return;
                }

                // 验证JSON格式
                if (!ValidateClipboardJson(clipboardContent, out string errorMessage))
                {
                    bool proceed = EditorUtility.DisplayDialog("JSON格式警告",
                        $"剪贴板内容可能不是有效的JSON:\n{errorMessage}\n\n是否仍要粘贴？",
                        "仍要粘贴", "取消");

                    if (!proceed) return;
                }

                inputJson = clipboardContent;
                EditorUtility.DisplayDialog("成功", "已粘贴剪贴板内容到输入框", "确定");
            }
            catch (Exception e)
            {
                EditorUtility.DisplayDialog("粘贴失败", $"无法粘贴剪贴板内容: {e.Message}", "确定");
            }
        }

        /// <summary>
        /// 预览剪贴板内容
        /// </summary>
        private void PreviewClipboard()
        {
            try
            {
                string clipboardContent = EditorGUIUtility.systemCopyBuffer;

                if (string.IsNullOrWhiteSpace(clipboardContent))
                {
                    EditorUtility.DisplayDialog("剪贴板预览", "剪贴板为空", "确定");
                    return;
                }

                // 限制预览长度
                string preview = clipboardContent;
                if (preview.Length > 500)
                {
                    preview = preview.Substring(0, 500) + "\n...(内容过长，已截断)";
                }

                // 验证JSON格式
                string jsonStatus = ValidateClipboardJson(clipboardContent, out string errorMessage)
                    ? "✅ 有效的JSON格式"
                    : $"❌ JSON格式错误: {errorMessage}";

                EditorUtility.DisplayDialog("剪贴板预览",
                    $"格式状态: {jsonStatus}\n\n内容预览:\n{preview}", "确定");
            }
            catch (Exception e)
            {
                EditorUtility.DisplayDialog("预览失败", $"无法预览剪贴板内容: {e.Message}", "确定");
            }
        }

        /// <summary>
        /// 检查剪贴板是否可用（包含有效JSON内容）
        /// </summary>
        private bool IsClipboardAvailable()
        {
            try
            {
                string clipboardContent = EditorGUIUtility.systemCopyBuffer;

                if (string.IsNullOrWhiteSpace(clipboardContent))
                    return false;

                return ValidateClipboardJson(clipboardContent, out _);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 绘制带颜色指示的剪贴板状态
        /// </summary>
        private void DrawClipboardStatus()
        {
            try
            {
                string clipboardContent = EditorGUIUtility.systemCopyBuffer;
                Color statusColor;
                string statusText;

                if (string.IsNullOrWhiteSpace(clipboardContent))
                {
                    statusColor = Color.red;
                    statusText = "剪切板: 空";
                }
                else
                {
                    bool isValidJson = ValidateClipboardJson(clipboardContent, out _);
                    if (isValidJson)
                    {
                        statusColor = Color.green;
                        statusText = $"剪切板: ✅ JSON ({clipboardContent.Length} 字符)";
                    }
                    else
                    {
                        statusColor = new Color(1f, 0.5f, 0f); // 橙色
                        statusText = $"剪切板: ❌ 非JSON ({clipboardContent.Length} 字符)";
                    }
                }

                // 显示带颜色的状态
                Color originalColor = GUI.color;
                GUI.color = statusColor;
                GUILayout.Label(statusText, EditorStyles.miniLabel);
                GUI.color = originalColor;
            }
            catch
            {
                Color originalColor = GUI.color;
                GUI.color = Color.red;
                GUILayout.Label("剪切板: 读取失败", EditorStyles.miniLabel);
                GUI.color = originalColor;
            }
        }

        /// <summary>
        /// 验证剪贴板JSON格式
        /// </summary>
        private bool ValidateClipboardJson(string content, out string errorMessage)
        {
            errorMessage = "";

            if (string.IsNullOrWhiteSpace(content))
            {
                errorMessage = "内容为空";
                return false;
            }

            try
            {
                JObject.Parse(content);
                return true;
            }
            catch (JsonException e)
            {
                errorMessage = e.Message;
                return false;
            }
            catch (Exception e)
            {
                errorMessage = $"未知错误: {e.Message}";
                return false;
            }
        }

        /// <summary>
        /// 检查结果是否为错误响应
        /// </summary>
        private bool IsErrorResponse(object result)
        {
            if (result == null) return true;

            try
            {
                var resultJson = JsonConvert.SerializeObject(result);
                var resultObj = JObject.Parse(resultJson);

                // 检查是否有success字段且为false
                if (resultObj.ContainsKey("success"))
                {
                    return !resultObj["success"]?.Value<bool>() ?? true;
                }

                // 检查是否有error字段
                return resultObj.ContainsKey("error");
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 从错误响应中提取错误消息
        /// </summary>
        private string ExtractErrorMessage(object result)
        {
            if (result == null) return "结果为空";

            try
            {
                var resultJson = JsonConvert.SerializeObject(result);
                var resultObj = JObject.Parse(resultJson);

                // 尝试从error字段获取错误信息
                if (resultObj.ContainsKey("error"))
                {
                    return resultObj["error"]?.ToString() ?? "未知错误";
                }

                // 尝试从message字段获取错误信息
                if (resultObj.ContainsKey("message"))
                {
                    return resultObj["message"]?.ToString() ?? "未知错误";
                }

                return result.ToString();
            }
            catch
            {
                return result.ToString();
            }
        }

        /// <summary>
        /// 手动记录当前执行结果
        /// </summary>
        private void RecordCurrentResult()
        {
            if (currentResult == null || string.IsNullOrEmpty(inputJson))
            {
                EditorUtility.DisplayDialog("无法记录", "没有可记录的执行结果", "确定");
                return;
            }

            try
            {
                // 解析输入的JSON来获取函数名和参数
                JObject inputObj = JObject.Parse(inputJson);

                // 检查是否为批量调用
                if (inputObj.ContainsKey("funcs"))
                {
                    // 批量调用记录
                    RecordBatchResult(inputObj, currentResult);
                }
                else if (inputObj.ContainsKey("func"))
                {
                    // 单个函数调用记录
                    RecordSingleResult(inputObj, currentResult);
                }
                else
                {
                    EditorUtility.DisplayDialog("记录失败", "无法解析输入的JSON格式", "确定");
                    return;
                }

                EditorUtility.DisplayDialog("记录成功", "执行结果已保存到记录中", "确定");

                // 刷新记录列表
                recordList = null;
                InitializeRecordList();
            }
            catch (Exception e)
            {
                EditorUtility.DisplayDialog("记录失败", $"记录执行结果时发生错误: {e.Message}", "确定");
                Debug.LogError($"[McpDebugWindow] 手动记录结果时发生错误: {e}");
            }
        }

        /// <summary>
        /// 记录单个函数调用结果
        /// </summary>
        private void RecordSingleResult(JObject inputObj, object result)
        {
            var funcName = inputObj["func"]?.ToString() ?? "Unknown";
            var argsJson = inputObj["args"]?.ToString() ?? "{}";
            var recordObject = McpExecuteRecordObject.instance;

            bool isSuccess = result != null && !IsErrorResponse(result);
            string errorMsg = "";
            string resultJson = "";

            if (isSuccess)
            {
                resultJson = JsonConvert.SerializeObject(result);
                if (result is JObject resultObj && resultObj["success"] != null && resultObj["success"].Type == JTokenType.Boolean && resultObj["success"].Value<bool>() == false)
                {
                    errorMsg = resultObj["error"]?.ToString() ?? "执行失败";
                }
            }
            else
            {
                errorMsg = result != null ? ExtractErrorMessage(result) : "执行失败，返回null";
                resultJson = result != null ? JsonConvert.SerializeObject(result) : "";
            }

            recordObject.addRecord(
                funcName,
                argsJson,
                resultJson,
                errorMsg,
                0, // 手动记录时没有执行时间
                "Debug Window (手动记录)"
            );
            recordObject.saveRecords();
        }

        /// <summary>
        /// 记录批量函数调用结果
        /// </summary>
        private void RecordBatchResult(JObject inputObj, object result)
        {
            try
            {
                var resultJson = JsonConvert.SerializeObject(result);
                var resultObj = JObject.Parse(resultJson);

                var results = resultObj["results"] as JArray;
                var errors = resultObj["errors"] as JArray;
                var funcsArray = inputObj["funcs"] as JArray;

                if (funcsArray != null && results != null)
                {
                    var recordObject = McpExecuteRecordObject.instance;

                    for (int i = 0; i < funcsArray.Count && i < results.Count; i++)
                    {
                        var funcCall = funcsArray[i] as JObject;
                        if (funcCall == null) continue;

                        var funcName = funcCall["func"]?.ToString() ?? "Unknown";
                        var argsJson = funcCall["args"]?.ToString() ?? "{}";
                        var singleResult = results[i];

                        bool isSuccess = singleResult != null && !singleResult.Type.Equals(JTokenType.Null);
                        string errorMsg = "";
                        string singleResultJson = "";

                        if (isSuccess)
                        {
                            singleResultJson = JsonConvert.SerializeObject(singleResult);
                        }
                        else
                        {
                            if (errors != null && i < errors.Count && errors[i] != null)
                            {
                                errorMsg = errors[i].ToString();
                            }
                            else
                            {
                                errorMsg = "批量调用中此项失败";
                            }
                        }

                        recordObject.addRecord(
                            funcName,
                            argsJson,
                            singleResultJson,
                            errorMsg,
                            0, // 手动记录时没有执行时间
                            $"Debug Window (手动记录 {i + 1}/{funcsArray.Count})"
                        );
                    }

                    recordObject.saveRecords();
                }
            }
            catch (Exception e)
            {
                throw new Exception($"记录批量结果时发生错误: {e.Message}", e);
            }
        }


        /// <summary>
        /// 选择记录并刷新到界面
        /// </summary>
        private void SelectRecord(int index)
        {
            var records = McpExecuteRecordObject.instance.GetCurrentGroupRecords();
            if (index < 0 || index >= records.Count) return;

            selectedRecordIndex = index;
            var record = records[index];

            inputJson = record.cmd;

            // 将执行结果刷新到结果区域
            if (!string.IsNullOrEmpty(record.result) || !string.IsNullOrEmpty(record.error))
            {
                showResult = true;
                var resultBuilder = new StringBuilder();
                resultBuilder.AppendLine($"📋 从执行记录加载 (索引: {index})");
                resultBuilder.AppendLine($"函数: {record.name}");
                resultBuilder.AppendLine($"时间: {record.timestamp}");
                resultBuilder.AppendLine($"来源: {record.source}");
                resultBuilder.AppendLine($"状态: {(record.success ? "成功" : "失败")}");
                if (record.duration > 0)
                {
                    resultBuilder.AppendLine($"执行时间: {record.duration:F2}ms");
                }
                resultBuilder.AppendLine();

                if (!string.IsNullOrEmpty(record.result))
                {
                    resultBuilder.AppendLine("执行结果:");
                    resultBuilder.AppendLine(record.result);
                }

                if (!string.IsNullOrEmpty(record.error))
                {
                    resultBuilder.AppendLine("错误信息:");
                    resultBuilder.AppendLine(record.error);
                }

                resultText = resultBuilder.ToString();
                currentResult = null; // 清空当前结果，因为这是历史记录
            }

            Repaint();
        }

        /// <summary>
        /// 处理记录元素的鼠标事件（双击检测）
        /// </summary>
        private void HandleRecordElementMouseEvents(Rect rect, int index)
        {
            Event e = Event.current;

            // 只在函数名区域检测双击，避免与整个元素的选择冲突
            const float numberWidth = 24f;
            const float iconWidth = 20f;
            const float padding = 6f;
            const float boxMargin = 4f;

            Rect funcNameRect = new Rect(
                rect.x + padding + boxMargin + numberWidth + iconWidth + 4f,
                rect.y + padding + boxMargin,
                rect.width - padding * 2 - boxMargin * 2 - numberWidth - iconWidth - 4f,
                16f
            );

            if (e.type == EventType.MouseDown && e.button == 0 && funcNameRect.Contains(e.mousePosition))
            {
                double currentTime = EditorApplication.timeSinceStartup;

                // 检测双击
                if (lastClickedIndex == index && (currentTime - lastClickTime) < 0.5) // 500ms内的双击
                {
                    // 开始编辑
                    StartEditing(index);
                    e.Use();
                }
                else
                {
                    // 单击，记录时间和索引
                    lastClickTime = currentTime;
                    lastClickedIndex = index;
                }
            }
        }

        /// <summary>
        /// 开始编辑记录名称
        /// </summary>
        private void StartEditing(int index)
        {
            var records = McpExecuteRecordObject.instance.GetCurrentGroupRecords();
            if (index >= 0 && index < records.Count)
            {
                editingRecordIndex = index;
                editingText = records[index].name;
                editingStarted = true;

                Repaint();
            }
        }

        /// <summary>
        /// 完成编辑并保存
        /// </summary>
        private void FinishEditing(int index, string newName)
        {
            if (editingRecordIndex == index && !string.IsNullOrWhiteSpace(newName))
            {
                var records = McpExecuteRecordObject.instance.GetCurrentGroupRecords();
                if (index >= 0 && index < records.Count)
                {
                    // 更新记录名称
                    records[index].name = newName.Trim();
                    McpExecuteRecordObject.instance.saveRecords();

                    // 显示成功提示（可选）
                    Debug.Log($"[McpDebugWindow] 记录名称已更新: {newName.Trim()}");
                }
            }

            // 退出编辑模式
            editingRecordIndex = -1;
            editingText = "";
            editingStarted = false;
            GUI.FocusControl(null); // 清除焦点
            Repaint();
        }

        /// <summary>
        /// 取消编辑
        /// </summary>
        private void CancelEditing()
        {
            editingRecordIndex = -1;
            editingText = "";
            editingStarted = false;
            GUI.FocusControl(null); // 清除焦点
            Repaint();
        }

        #region 分组管理UI

        /// <summary>
        /// 绘制分组管理界面
        /// </summary>
        private void DrawGroupManager(float width)
        {
            var recordObject = McpExecuteRecordObject.instance;

            GUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label("分组管理", EditorStyles.boldLabel);

            // 创建新分组
            GUILayout.Label("创建新分组:");
            newGroupName = EditorGUILayout.TextField("名称", newGroupName);
            newGroupDescription = EditorGUILayout.TextField("描述", newGroupDescription);

            GUILayout.BeginHorizontal();
            GUI.enabled = !string.IsNullOrWhiteSpace(newGroupName);
            if (GUILayout.Button("创建分组", GUILayout.Width(80)))
            {
                string groupId = System.Guid.NewGuid().ToString("N")[..8];
                string groupNameTrimmed = newGroupName.Trim();
                if (recordObject.CreateGroup(groupId, groupNameTrimmed, newGroupDescription.Trim()))
                {
                    newGroupName = "";
                    newGroupDescription = "";
                    EditorUtility.DisplayDialog("成功", $"分组 '{groupNameTrimmed}' 创建成功！", "确定");
                }
                else
                {
                    EditorUtility.DisplayDialog("失败", "创建分组失败，请检查名称是否重复。", "确定");
                }
            }
            GUI.enabled = true;
            GUILayout.EndHorizontal();

            // 分组列表（缩小高度）
            if (recordObject.recordGroups.Count > 0)
            {
                GUILayout.Space(5);
                GUILayout.Label("现有分组:");

                // 使用固定高度的滚动区域
                groupScrollPosition = GUILayout.BeginScrollView(groupScrollPosition, GUILayout.Height(120));

                for (int i = 0; i < recordObject.recordGroups.Count; i++)
                {
                    var group = recordObject.recordGroups[i];

                    GUILayout.BeginVertical(EditorStyles.helpBox);

                    GUILayout.BeginHorizontal();

                    // 分组信息（简化显示）
                    GUILayout.BeginVertical();
                    GUILayout.Label($"{group.name}", EditorStyles.boldLabel);
                    GUILayout.Label($"{recordObject.GetGroupStatistics(group.id)}", EditorStyles.miniLabel);
                    GUILayout.EndVertical();

                    GUILayout.FlexibleSpace();

                    // 操作按钮（水平排列）
                    if (GUILayout.Button("切换", GUILayout.Width(50), GUILayout.Height(20)))
                    {
                        recordObject.SwitchToGroup(group.id);
                        recordList = null;
                        InitializeRecordList();
                    }

                    GUI.enabled = !group.isDefault;
                    if (GUILayout.Button("删除", GUILayout.Width(50), GUILayout.Height(20)))
                    {
                        if (EditorUtility.DisplayDialog("确认删除",
                            $"确定要删除分组 '{group.name}' 吗？\n\n该分组的所有记录将被移动到默认分组。",
                            "删除", "取消"))
                        {
                            recordObject.DeleteGroup(group.id);
                            recordList = null;
                            InitializeRecordList();
                        }
                    }
                    GUI.enabled = true;

                    GUILayout.EndHorizontal();
                    GUILayout.EndVertical();
                }

                GUILayout.EndScrollView();
            }

            GUILayout.EndVertical();
        }

        /// <summary>
        /// 计算分组管理区域的高度
        /// </summary>
        private float CalculateGroupManagerHeight()
        {
            var recordObject = McpExecuteRecordObject.instance;
            float baseHeight = 120; // 基本高度（标题 + 创建区域）

            if (recordObject.recordGroups.Count > 0)
            {
                baseHeight += 140; // 分组列表区域（标题 + 固定高度的滚动区域）
            }

            return baseHeight;
        }

        /// <summary>
        /// 获取当前分组的显示名称
        /// </summary>
        private string GetCurrentGroupDisplayName()
        {
            var recordObject = McpExecuteRecordObject.instance;
            var currentGroup = recordObject.GetCurrentGroup();
            return currentGroup?.name ?? "未知分组";
        }

        #endregion

    }
}