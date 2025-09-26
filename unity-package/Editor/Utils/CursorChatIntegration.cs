using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEditor;
using Debug = UnityEngine.Debug;

namespace UnityMCP.Tools
{
    /// <summary>
    /// Cursor聊天集成工具 - 极简版，仅支持DirectPaste功能
    /// </summary>
    public static class CursorChatIntegration
    {
        #region Windows API声明
        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder strText, int maxCount);

        [DllImport("user32.dll")]
        private static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern int GetClassName(IntPtr hWnd, System.Text.StringBuilder strClassName, int maxCount);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        private const int VK_CONTROL = 0x11;
        private const uint KEYEVENTF_KEYUP = 0x0002;
        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
        #endregion

        /// <summary>
        /// 发送消息到Cursor（自动定位到输入框并粘贴）
        /// </summary>
        /// <param name="message">要发送的消息</param>
        /// <param name="autoSend">是否自动发送消息（按回车）</param>
        /// <param name="focusInputBox">是否自动定位到输入框，默认true</param>
        public static void SendToCursor(string message, bool autoSend = false, bool focusInputBox = true)
        {
            if (string.IsNullOrEmpty(message))
            {
                Debug.LogWarning("消息为空，跳过发送");
                return;
            }

            Debug.Log($"开始发送消息到Cursor，长度: {message.Length}");

            // 查找Cursor窗口
            IntPtr cursorWindow = FindCursorWindow();
            if (cursorWindow == IntPtr.Zero)
            {
                Debug.LogWarning("未找到Cursor窗口，消息已复制到剪贴板");
                GUIUtility.systemCopyBuffer = message;
                EditorUtility.DisplayDialog("Cursor未找到",
                    "未找到Cursor窗口，消息已复制到剪贴板。\n请手动粘贴到Cursor中。", "确定");
                return;
            }

            Debug.Log($"找到Cursor窗口: {cursorWindow}");

            // 激活窗口
            SetForegroundWindow(cursorWindow);

            // 延迟发送消息
            var delayCount = 0;
            EditorApplication.update += WaitAndSend;

            void WaitAndSend()
            {
                delayCount++;
                if (delayCount < 10) // 等待约1秒
                    return;

                EditorApplication.update -= WaitAndSend;

                // 复制消息到剪贴板
                GUIUtility.systemCopyBuffer = message;
                Debug.Log($"消息已复制到剪贴板: {message.Substring(0, Math.Min(50, message.Length))}...");

                // 再次确保窗口激活
                SetForegroundWindow(cursorWindow);

                if (focusInputBox)
                {
                    // 先发送Ctrl+L定位到输入框
                    var focusDelayCount = 0;
                    EditorApplication.update += DelayedFocus;

                    void DelayedFocus()
                    {
                        focusDelayCount++;
                        if (focusDelayCount < 2) // 等待约200ms
                            return;

                        EditorApplication.update -= DelayedFocus;

                        SendFocusInputBoxKeys();
                        Debug.Log("已发送Ctrl+I定位到输入框");

                        // 延迟发送粘贴键
                        var pasteDelayCount = 0;
                        EditorApplication.update += DelayedPaste;

                        void DelayedPaste()
                        {
                            pasteDelayCount++;
                            if (pasteDelayCount < 5) // 等待约500ms，给输入框足够时间响应
                                return;

                            EditorApplication.update -= DelayedPaste;
                            ExecutePasteAndSend();
                        }
                    }
                }
                else
                {
                    // 直接粘贴（原有逻辑）
                    var pasteDelayCount = 0;
                    EditorApplication.update += DelayedPaste;

                    void DelayedPaste()
                    {
                        pasteDelayCount++;
                        if (pasteDelayCount < 3) // 等待约300ms
                            return;

                        EditorApplication.update -= DelayedPaste;
                        ExecutePasteAndSend();
                    }
                }

                void ExecutePasteAndSend()
                {
                    SendPasteKeys();
                    Debug.Log("已发送粘贴快捷键");

                    if (autoSend)
                    {
                        var enterDelayCount = 0;
                        EditorApplication.update += DelayedEnter;

                        void DelayedEnter()
                        {
                            enterDelayCount++;
                            if (enterDelayCount < 5) // 等待约500ms
                                return;

                            EditorApplication.update -= DelayedEnter;
                            SendEnterKey();
                            Debug.Log("已发送回车键");
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 查找Cursor窗口
        /// </summary>
        private static IntPtr FindCursorWindow()
        {
            IntPtr foundWindow = IntPtr.Zero;

            EnumWindows((hWnd, lParam) =>
            {
                if (!IsWindowVisible(hWnd))
                    return true;

                try
                {
                    int titleLength = GetWindowTextLength(hWnd);
                    if (titleLength > 0)
                    {
                        var titleBuilder = new System.Text.StringBuilder(titleLength + 1);
                        GetWindowText(hWnd, titleBuilder, titleBuilder.Capacity);
                        string title = titleBuilder.ToString();

                        var classNameBuilder = new System.Text.StringBuilder(256);
                        GetClassName(hWnd, classNameBuilder, classNameBuilder.Capacity);
                        string className = classNameBuilder.ToString();

                        if (IsCursorWindow(title, className))
                        {
                            Debug.Log($"找到Cursor窗口: {title}");
                            foundWindow = hWnd;
                            return false;
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"检查窗口时出错: {e.Message}");
                }

                return true;
            }, IntPtr.Zero);

            return foundWindow;
        }

        /// <summary>
        /// 判断是否是Cursor窗口
        /// </summary>
        private static bool IsCursorWindow(string title, string className)
        {
            if (string.IsNullOrEmpty(title)) return false;

            string lowerTitle = title.ToLower();
            return lowerTitle.Contains("cursor") ||
                   (className == "Chrome_WidgetWin_1" && lowerTitle.Contains("code"));
        }

        /// <summary>
        /// 发送Ctrl+I定位到输入框
        /// </summary>
        private static void SendFocusInputBoxKeys()
        {
            try
            {
                // 方法1: 发送Ctrl+I (Cursor的Agent模式聊天快捷键)
                SendKeyboardShortcut(VK_CONTROL, 0x49); // Ctrl+I
                Debug.Log("已发送Ctrl+I快捷键");

                System.Threading.Thread.Sleep(200);

                // 方法2: 如果Ctrl+I不起作用，尝试点击页面底部（输入框通常在底部）
                // 这里我们发送End键来确保光标移到页面底部，然后Tab键来聚焦输入框
                keybd_event(0x23, 0, 0, UIntPtr.Zero); // End键
                System.Threading.Thread.Sleep(50);
                keybd_event(0x23, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
                System.Threading.Thread.Sleep(100);

                keybd_event(0x09, 0, 0, UIntPtr.Zero); // Tab键
                System.Threading.Thread.Sleep(50);
                keybd_event(0x09, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            }
            catch (Exception e)
            {
                Debug.LogError($"发送定位快捷键失败: {e.Message}");
            }
        }

        /// <summary>
        /// 发送组合键快捷键
        /// </summary>
        private static void SendKeyboardShortcut(int modifier, int key)
        {
            // 清理按键状态
            keybd_event((byte)modifier, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            keybd_event((byte)key, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            System.Threading.Thread.Sleep(50);

            // 发送组合键
            keybd_event((byte)modifier, 0, 0, UIntPtr.Zero);
            System.Threading.Thread.Sleep(50);
            keybd_event((byte)key, 0, 0, UIntPtr.Zero);
            System.Threading.Thread.Sleep(50);
            keybd_event((byte)key, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            System.Threading.Thread.Sleep(50);
            keybd_event((byte)modifier, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        }

        /// <summary>
        /// 发送Ctrl+V粘贴键
        /// </summary>
        private static void SendPasteKeys()
        {
            try
            {
                // 清理按键状态
                keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
                keybd_event(0x56, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
                System.Threading.Thread.Sleep(50);

                // 发送Ctrl+V
                keybd_event(VK_CONTROL, 0, 0, UIntPtr.Zero);
                System.Threading.Thread.Sleep(50);
                keybd_event(0x56, 0, 0, UIntPtr.Zero);
                System.Threading.Thread.Sleep(50);
                keybd_event(0x56, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
                System.Threading.Thread.Sleep(50);
                keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            }
            catch (Exception e)
            {
                Debug.LogError($"发送粘贴键失败: {e.Message}");
            }
        }

        /// <summary>
        /// 发送回车键
        /// </summary>
        private static void SendEnterKey()
        {
            try
            {
                keybd_event(0x0D, 0, 0, UIntPtr.Zero);
                System.Threading.Thread.Sleep(50);
                keybd_event(0x0D, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            }
            catch (Exception e)
            {
                Debug.LogError($"发送回车键失败: {e.Message}");
            }
        }

        #region 便捷方法
        /// <summary>
        /// 发送Figma数据到Cursor
        /// </summary>
        public static void SendFigmaDataToChat(string figmaData, string prompt = "", bool autoSend = false)
        {
            string message = string.IsNullOrEmpty(prompt)
                ? $"基于以下Figma数据生成Unity UI代码:\n\n{figmaData}"
                : $"{prompt}\n\n{figmaData}";

            SendToCursor(message, autoSend, focusInputBox: true);
        }

        /// <summary>
        /// 发送代码到Cursor进行优化
        /// </summary>
        public static void SendCodeForOptimization(string code, string description = "", bool autoSend = false)
        {
            string message = string.IsNullOrEmpty(description)
                ? $"请优化以下代码:\n\n```csharp\n{code}\n```"
                : $"{description}\n\n```csharp\n{code}\n```";

            SendToCursor(message, autoSend, focusInputBox: true);
        }

        /// <summary>
        /// 发送错误信息到Cursor寻求帮助
        /// </summary>
        public static void SendErrorForHelp(string error, string context = "", bool autoSend = false)
        {
            string message = string.IsNullOrEmpty(context)
                ? $"遇到以下错误，请帮助解决:\n\n{error}"
                : $"在 {context} 中遇到以下错误，请帮助解决:\n\n{error}";

            SendToCursor(message, autoSend, focusInputBox: true);
        }
        #endregion
    }
}