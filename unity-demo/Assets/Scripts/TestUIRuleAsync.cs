using System.Collections;
using UnityEngine;
using UnityEditor;
using Newtonsoft.Json.Linq;
using UnityMcp.Tools;

public class TestUIRuleAsync : MonoBehaviour
{
    [MenuItem("Test/Test UI Rule Async")]
    public static void TestUIRuleAsyncFunction()
    {
        // 创建测试数据
        var testData = new JObject();
        testData["name"] = "MatchRuleUI";

        Debug.Log("[TestUIRuleAsync] 开始测试异步UI规则获取");

        // 这里应该通过Unity MCP调用ui_rule_manage的get_rule方法
        Debug.Log("[TestUIRuleAsync] 测试数据准备完成，请通过MCP调用进行测试");
        Debug.Log($"[TestUIRuleAsync] 测试参数: {testData}");
    }
}
