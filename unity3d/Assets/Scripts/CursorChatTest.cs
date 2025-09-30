using UnityEngine;
using UnityMCP.Tools;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace UnityMCP.Test
{
    /// <summary>
    /// Cursor聊天功能测试脚本
    /// 可以在Inspector中测试各种Cursor聊天调用方式
    /// </summary>
    public class CursorChatTest : MonoBehaviour
    {
        [Header("测试配置")]
        [TextArea(3, 10)]
        public string testMessage = "你好，这是来自Unity的测试消息！";

        public bool autoSendMessage = false;

        [Header("Figma数据测试")]
        [TextArea(5, 15)]
        public string sampleFigmaData = @"{
    ""id"": ""2:2"",
    ""name"": ""StartScreen"",
    ""type"": ""FRAME"",
    ""size"": [1280, 720],
    ""backgroundColor"": {
        ""type"": ""GRADIENT_LINEAR""
    },
    ""children"": [
        {
            ""id"": ""3:1279"",
            ""name"": ""TopBar"",
            ""type"": ""INSTANCE"",
            ""size"": [1280, 96]
        }
    ]
}";

        [Header("运行时测试")]
        [SerializeField] private KeyCode testHotkey = KeyCode.F1;

        private void Update()
        {
            // 运行时快捷键测试
            if (Input.GetKeyDown(testHotkey))
            {
                TestCursorChatBasic();
            }
        }

        #region 公共测试方法

        /// <summary>
        /// 基础Cursor聊天测试
        /// </summary>
        [ContextMenu("测试基础聊天")]
        public void TestCursorChatBasic()
        {
            Debug.Log("开始测试Cursor聊天...");

#if UNITY_EDITOR
            CursorChatIntegration.SendToCursor(testMessage, autoSendMessage);
#else
            Debug.LogWarning("Cursor聊天集成仅在Editor模式下可用");
#endif
        }
        #endregion
    }

}
