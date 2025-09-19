
using UnityEngine;
using UnityEditor;

public class MCPRuntimeBuilder : MonoBehaviour
{
    [MenuItem("MCP Runtime/Execute Water Plant Build")]
    public static void ExecuteWaterPlantBuild()
    {
        Debug.Log("MCP: 正在执行自来水厂建设...");
        
        // 清理现有对象
        GameObject existing = GameObject.Find("地基");
        if (existing) DestroyImmediate(existing);
        existing = GameObject.Find("主处理建筑");
        if (existing) DestroyImmediate(existing);
        existing = GameObject.Find("沉淀池-1");
        if (existing) DestroyImmediate(existing);
        
        // 执行我们已有的建设脚本
        try
        {
            MCPWaterPlantBuilder.BuildCompleteWaterTreatmentPlant();
            Debug.Log("✓ MCP: 自来水厂建设成功完成!");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"MCP 建设错误: {e.Message}");
        }
    }
}
