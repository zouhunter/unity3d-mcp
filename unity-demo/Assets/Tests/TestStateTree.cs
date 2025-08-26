using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityMcp.Tools;
using System.Text;
using Newtonsoft.Json.Linq;

public class TestStateTree
{
    #region 基础状态树测试

    [Test]
    public void TestBasicStateTree()
    {
        // 创建没有DefaultLeaf的树
        var tree = StateTreeBuilder.Create()
            .Key("role")
                .Node("admin", "level")
                    .Node(3, "env")
                        .Leaf("prod", AnimProd)
                        .Leaf("dev", AnimDev)
                    .ULeaf(2, AdminL2)
                .ULeaf("user", User)
            .Build();
        Debug.Log(tree.ToString());
        // 测试正常路径
        var result1 = tree.Run(new JObject
        {
            ["role"] = "admin",
            ["level"] = 3,
            ["env"] = "prod"
        });
        Debug.Log($"TestBasicStateTree - 路径1结果: {result1}, 错误消息: {tree.ErrorMessage}");
        Assert.AreEqual("AnimProd", result1);

        var result2 = tree.Run(new JObject
        {
            ["role"] = "admin",
            ["level"] = 3,
            ["env"] = "dev"
        });
        Assert.AreEqual("AnimDev", result2);

        var result3 = tree.Run(new JObject
        {
            ["role"] = "admin",
            ["level"] = 2
        });
        Assert.AreEqual("AdminL2", result3);

        var result4 = tree.Run(new JObject
        {
            ["role"] = "user"
        });
        Assert.AreEqual("User", result4);

        // 测试错误路径
        var result5 = tree.Run(new JObject
        {
            ["role"] = "admin1", // 不存在的角色
            ["level"] = 3,
            ["env"] = "prod"
        });
        Debug.Log($"TestBasicStateTree - 错误路径结果: {result5}, 错误消息: {tree.ErrorMessage}");
        Assert.IsNull(result5);
        Assert.IsNotEmpty(tree.ErrorMessage);
    }

    [Test]
    public void TestStateTreeWithDefault()
    {
        // 创建带有DefaultLeaf的树
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
        Debug.Log(treeWithDefault.ToString());

        // 测试正常路径
        var result1 = treeWithDefault.Run(new JObject
        {
            ["role"] = "admin",
            ["level"] = 3,
            ["env"] = "prod"
        });
        Assert.AreEqual("AnimProd", result1);

        // 测试默认路径
        var result2 = treeWithDefault.Run(new JObject
        {
            ["role"] = "admin1", // 不存在的角色
            ["level"] = 3,
            ["env"] = "prod"
        });
        Assert.AreEqual("Default", result2);
        Assert.IsNull(treeWithDefault.ErrorMessage);
    }

    #endregion

    #region 可选参数测试

    [Test]
    public void TestOptionalParameters()
    {
        // 创建一个带有可选参数的树
        var optionalTree = StateTreeBuilder.Create()
            .Key("action")
                .NodeNext("create")
                    .OptionalLeaf("debug", HandleDebug)
                    .OptionalLeaf("verbose", HandleVerbose)
                    .DefaultLeaf(HandleCreate)
                    .Up()
                .Leaf("update", HandleUpdate)
            .DefaultLeaf(HandleUnknown)
            .Build();
        Debug.Log("可选参数测试状态树结构：\n" + optionalTree.ToString());

        // 输出节点结构以便调试
        Debug.Log("创建节点的键：" + (optionalTree.select.ContainsKey("create") ? "存在" : "不存在"));
        if (optionalTree.select.ContainsKey("create"))
        {
            var createNode = optionalTree.select["create"];
            Debug.Log($"创建节点的键名：{createNode.key}");
            Debug.Log($"创建节点的子节点数量：{createNode.select.Count}");
            foreach (var key in createNode.select.Keys)
            {
                Debug.Log($"创建节点的子节点键：{key}");
            }
        }
        // 测试1：常规参数
        var result1 = optionalTree.Run(new JObject
        {
            ["action"] = "create"
        });
        Debug.Log($"测试1：普通创建操作结果: {result1}, 错误消息: {optionalTree.ErrorMessage}");
        Assert.AreEqual("执行创建操作", result1);

        // 测试2：常规参数
        var result2 = optionalTree.Run(new JObject
        {
            ["action"] = "update"
        });
        Assert.AreEqual("执行更新操作", result2);

        // 测试3：create操作带debug参数
        var result3 = optionalTree.Run(new JObject
        {
            ["action"] = "create",
            ["debug"] = false
        });
        Debug.Log($"测试3：创建操作带debug参数结果: {result3}, 错误消息: {optionalTree.ErrorMessage}");
        Assert.AreEqual("启用调试模式", result3);

        // 测试4：create操作带verbose参数
        var result4 = optionalTree.Run(new JObject
        {
            ["action"] = "create",
            ["verbose"] = "yes"
        });
        Assert.AreEqual("启用详细输出模式", result4);

        // 测试5：create操作带多个可选参数，应匹配第一个
        var result5 = optionalTree.Run(new JObject
        {
            ["action"] = "create",
            ["debug"] = true,
            ["verbose"] = "yes"
        });
        Debug.Log($"测试5：创建操作带多个可选参数结果: {result5}, 错误消息: {optionalTree.ErrorMessage}");
        Assert.AreEqual("启用调试模式", result5);

        // 测试6：无匹配，使用默认
        var result6 = optionalTree.Run(new JObject
        {
            ["action"] = "unknown" // 无效action，无可选参数
        });
        Assert.AreEqual("未知操作", result6);
    }

    #endregion

    #region 混合参数测试

    [Test]
    public void TestMixedParameters()
    {
        // 创建一个带有可选参数分支的树
        var mixedTree = StateTreeBuilder.Create()
            .Key("operation")
                .Node("search", "type")
                    .Leaf("user", HandleSearchUser)
                    .Leaf("product", HandleSearchProduct)
                    .OptionalNode("advanced", "criteria")
                        .Leaf("name", HandleAdvancedNameSearch)
                        .Leaf("id", HandleAdvancedIdSearch)
                        .DefaultLeaf(HandleAdvancedSearch)
                        .Up() // 确保返回到search节点
                    .Up()
                .OptionalLeaf("sort", HandleSort)
                .Up()
            .DefaultLeaf(HandleUnknownOperation)
            .Build();

        Debug.Log("混合参数测试状态树结构：\n" + mixedTree.ToString());

        // 输出节点结构以便调试
        Debug.Log("search节点是否存在：" + (mixedTree.select.ContainsKey("search") ? "是" : "否"));
        if (mixedTree.select.ContainsKey("search"))
        {
            var searchNode = mixedTree.select["search"];
            Debug.Log($"search节点的键名：{searchNode.key}");
            Debug.Log($"search节点的子节点数量：{searchNode.select.Count}");
            foreach (var key in searchNode.select.Keys)
            {
                Debug.Log($"search节点的子节点键：{key}");
            }

            // 检查可选参数节点
            string optionalKey = "__OPTIONAL_PARAM__advanced";
            Debug.Log($"可选参数节点是否存在：{optionalKey} - " + (mixedTree.select.ContainsKey(optionalKey) ? "是" : "否"));
        }

        // 测试1：基本搜索 - 用户
        var result1 = mixedTree.Run(new JObject
        {
            ["operation"] = "search",
            ["type"] = "user"
        });
        Assert.AreEqual("搜索用户", result1);

        // 测试2：基本搜索 - 产品
        var result2 = mixedTree.Run(new JObject
        {
            ["operation"] = "search",
            ["type"] = "product"
        });
        Assert.AreEqual("搜索产品", result2);

        // 测试3：高级搜索 - 按名称
        var result3 = mixedTree.Run(new JObject
        {
            ["operation"] = "search",
            ["advanced"] = true,
            ["criteria"] = "name"
        });
        Assert.AreEqual("高级名称搜索", result3);

        // 测试4：高级搜索 - 按ID
        var result4 = mixedTree.Run(new JObject
        {
            ["operation"] = "search",
            ["advanced"] = true,
            ["criteria"] = "id"
        });
        Assert.AreEqual("高级ID搜索", result4);

        // 测试5：高级搜索 - 默认
        var result5 = mixedTree.Run(new JObject
        {
            ["operation"] = "search",
            ["advanced"] = true,
            ["criteria"] = "unknown"
        });
        Debug.Log($"测试5：高级搜索默认结果: {result5}, 错误消息: {mixedTree.ErrorMessage}");
        Assert.AreEqual("默认高级搜索", result5);

        // 测试6：排序
        var result6 = mixedTree.Run(new JObject
        {
            ["operation"] = "unknown",
            ["sort"] = "desc"
        });
        Assert.AreEqual("按desc排序", result6);

        // 测试7：未知操作
        var result7 = mixedTree.Run(new JObject
        {
            ["operation"] = "unknown"
        });
        Assert.AreEqual("未知操作类型", result7);
    }

    #endregion

    #region 处理方法

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

    #endregion
}
