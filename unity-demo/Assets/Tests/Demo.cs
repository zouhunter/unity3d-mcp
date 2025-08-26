using System.Collections;

// ------------------------- Quick Demo -------------------------
using System.Collections.Generic;
using System;
using UnityMcp.Tools;
using UnityEngine;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;

public class Demo : MonoBehaviour
{
    public void Start()
    {
        // 基本演示
        BasicDemo();

        // 可选参数演示
        OptionalParamDemo();

        // 混合参数演示
        MixedParamsDemo();
    }

    private void BasicDemo()
    {
        Debug.Log("========== 基本功能演示 ==========");

        // 第一个树：没有DefaultLeaf
        var tree = StateTreeBuilder.Create()
            .Key("role")
                .Node("admin", "level")
                    .Node(3, "env")
                        .Leaf("prod", AnimProd)
                        .Leaf("dev", AnimDev)
                    .ULeaf(2, AdminL2)
                .ULeaf("user", User)
            // .DefaultLeaf(Default)
            .Build();

        // 第二个树：有DefaultLeaf
        var treeWithDefault = StateTreeBuilder.Create()
            .Key("role")
                .Node("admin", "level")
                    .Node(3, "env")
                        .Leaf("prod", AnimProd)
                        .Leaf("dev", AnimDev)
                    .ULeaf(2, AdminL2)
                .ULeaf("user", User)
            .DefaultLeaf(Default)
            .Build();

        StringBuilder sb = new StringBuilder();
        tree.Print(sb);
        Debug.Log("基本树结构:");
        Debug.Log(sb);

        var reu = tree.Run(new JObject
        {
            ["role"] = "admin1",  // 这里使用了不存在的role值
            ["level"] = 3,
            ["env"] = "prod"
        });        // ? AdminProd

        // 检查是否有错误信息
        if (!string.IsNullOrEmpty(tree.ErrorMessage))
        {
            Debug.LogError("StateTree错误: " + tree.ErrorMessage);
        }

        Debug.Log("基本树结果: " + reu);

        // 测试带有DefaultLeaf的树
        Debug.Log("测试带有DefaultLeaf的树：");
        var reuWithDefault = treeWithDefault.Run(new JObject
        {
            ["role"] = "admin1",  // 同样使用不存在的role值
            ["level"] = 3,
            ["env"] = "prod"
        });

        // 检查是否有错误信息（这里不应该有，因为有DefaultLeaf）
        if (!string.IsNullOrEmpty(treeWithDefault.ErrorMessage))
        {
            Debug.LogError("带DefaultLeaf的StateTree错误: " + treeWithDefault.ErrorMessage);
        }

        Debug.Log("带DefaultLeaf的结果: " + reuWithDefault);
    }

    private void OptionalParamDemo()
    {
        Debug.Log("\n========== 可选参数演示 ==========");

        // 创建一个带有可选参数的树
        var optionalTree = StateTreeBuilder.Create()
            .Key("action")
                .Leaf("create", HandleCreate)
                .Leaf("update", HandleUpdate)
                .OptionalLeaf("debug", HandleDebug) // 当debug参数存在时执行
                .OptionalLeaf("verbose", HandleVerbose) // 当verbose参数存在时执行
            .DefaultLeaf(HandleUnknown)
            .Build();

        StringBuilder sb = new StringBuilder();
        optionalTree.Print(sb);
        Debug.Log("可选参数树结构:");
        Debug.Log(sb);

        // 测试1：没有可选参数
        Debug.Log("测试1：没有可选参数");
        var result1 = optionalTree.Run(new JObject
        {
            ["action"] = "create"
        });
        Debug.Log("结果1: " + result1);

        // 测试2：有debug可选参数
        Debug.Log("测试2：有debug可选参数");
        var result2 = optionalTree.Run(new JObject
        {
            ["action"] = "create",
            ["debug"] = true
        });
        Debug.Log("结果2: " + result2);

        // 测试3：有verbose可选参数
        Debug.Log("测试3：有verbose可选参数");
        var result3 = optionalTree.Run(new JObject
        {
            ["action"] = "update",
            ["verbose"] = "yes"
        });
        Debug.Log("结果3: " + result3);

        // 测试4：两个可选参数都有，应该匹配第一个
        Debug.Log("测试4：两个可选参数都有");
        var result4 = optionalTree.Run(new JObject
        {
            ["action"] = "unknown", // 无效action，但有可选参数
            ["debug"] = true,
            ["verbose"] = "yes"
        });
        Debug.Log("结果4: " + result4);
    }

    private void MixedParamsDemo()
    {
        Debug.Log("\n========== 混合参数演示 ==========");

        // 创建一个带有可选参数分支的树
        var mixedTree = StateTreeBuilder.Create()
            .Key("operation")
                .Node("search", "type")
                    .Leaf("user", HandleSearchUser)
                    .Leaf("product", HandleSearchProduct)
                    .OptionalNode("advanced", "criteria") // 当advanced参数存在时，进入criteria分支
                        .Leaf("name", HandleAdvancedNameSearch)
                        .Leaf("id", HandleAdvancedIdSearch)
                        .DefaultLeaf(HandleAdvancedSearch)
                .OptionalLeaf("sort", HandleSort) // 当sort参数存在时执行
            .DefaultLeaf(HandleUnknownOperation)
            .Build();

        StringBuilder sb = new StringBuilder();
        mixedTree.Print(sb);
        Debug.Log("混合参数树结构:");
        Debug.Log(sb);

        // 测试1：基本搜索
        Debug.Log("测试1：基本搜索");
        var result1 = mixedTree.Run(new JObject
        {
            ["operation"] = "search",
            ["type"] = "user"
        });
        Debug.Log("结果1: " + result1);

        // 测试2：高级搜索 - 按名称
        Debug.Log("测试2：高级搜索 - 按名称");
        var result2 = mixedTree.Run(new JObject
        {
            ["operation"] = "search",
            ["advanced"] = true, // 触发可选分支
            ["criteria"] = "name"
        });
        Debug.Log("结果2: " + result2);

        // 测试3：高级搜索 - 默认
        Debug.Log("测试3：高级搜索 - 默认");
        var result3 = mixedTree.Run(new JObject
        {
            ["operation"] = "search",
            ["advanced"] = true, // 触发可选分支
            ["criteria"] = "unknown" // 使用默认处理
        });
        Debug.Log("结果3: " + result3);

        // 测试4：排序
        Debug.Log("测试4：排序");
        var result4 = mixedTree.Run(new JObject
        {
            ["operation"] = "unknown", // 无效操作
            ["sort"] = "asc" // 但有排序参数
        });
        Debug.Log("结果4: " + result4);
    }

    // 基本处理方法
    private object AnimProd(JObject ctx)
    {
        return "AnimProd";
    }

    private object AnimDev(JObject ctx)
    {
        return "AnimDev";
    }

    private object AdminL2(JObject ctx)
    {
        return "AdminL2";
    }

    private object User(JObject ctx)
    {
        return "User";
    }

    private object Default(JObject ctx)
    {
        return "Default";
    }

    // 可选参数处理方法
    private object HandleCreate(JObject ctx)
    {
        return "执行创建操作";
    }

    private object HandleUpdate(JObject ctx)
    {
        return "执行更新操作";
    }

    private object HandleDebug(JObject ctx)
    {
        return "启用调试模式";
    }

    private object HandleVerbose(JObject ctx)
    {
        return "启用详细输出模式";
    }

    private object HandleUnknown(JObject ctx)
    {
        return "未知操作";
    }

    // 混合参数处理方法
    private object HandleSearchUser(JObject ctx)
    {
        return "搜索用户";
    }

    private object HandleSearchProduct(JObject ctx)
    {
        return "搜索产品";
    }

    private object HandleAdvancedNameSearch(JObject ctx)
    {
        return "高级名称搜索";
    }

    private object HandleAdvancedIdSearch(JObject ctx)
    {
        return "高级ID搜索";
    }

    private object HandleAdvancedSearch(JObject ctx)
    {
        return "默认高级搜索";
    }

    private object HandleSort(JObject ctx)
    {
        string direction = ctx["sort"]?.ToString() ?? "asc";
        return $"按{direction}排序";
    }

    private object HandleUnknownOperation(JObject ctx)
    {
        return "未知操作类型";
    }
}
