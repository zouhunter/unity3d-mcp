using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace UnityMcp.Tools
{
    /// <summary>
    /// StateTree 使用示例
    /// </summary>
    public class StateTreeExample : MonoBehaviour
    {
        [ContextMenu("运行StateTree示例")]
        public void RunExample()
        {
            // 创建StateTree实例
            var stateTree = new StateTree();

            // 构建状态树结构
            BuildStateTree(stateTree);

            // 打印树形结构
            var sb = new StringBuilder();
            stateTree.Print(sb);
            Debug.Log("StateTree 结构：\n" + sb.ToString());

            // 测试不同的上下文
            TestContexts(stateTree);
        }

        private void BuildStateTree(StateTree root)
        {
            // 创建role节点
            var roleNode = new StateTree { key = "role" };
            root.select["role"] = roleNode;

            // 创建admin分支
            var adminNode = new StateTree { key = "admin" };
            roleNode.select["admin"] = adminNode;

            // 创建level分支
            var levelNode = new StateTree { key = "level" };
            adminNode.select["level"] = levelNode;

            // 创建level 3分支
            var level3Node = new StateTree { key = "3" };
            levelNode.select[3] = level3Node;

            // 创建env分支
            var envNode = new StateTree { key = "env" };
            level3Node.select["env"] = envNode;

            // 创建prod和dev叶子节点
            envNode.select["prod"] = (Action)(() => Debug.Log("执行: AdminProd"));
            envNode.select["dev"] = (Action)(() => Debug.Log("执行: AdminDev"));

            // 创建level 2叶子节点
            levelNode.select[2] = (Action)(() => Debug.Log("执行: AdminL2"));

            // 创建user叶子节点
            roleNode.select["user"] = (Action)(() => Debug.Log("执行: User"));

            // 创建默认分支
            roleNode.select[StateTree.Default] = (Action)(() => Debug.Log("执行: Default"));
        }

        private void TestContexts(StateTree stateTree)
        {
            Debug.Log("=== 测试不同的上下文 ===");

            // 测试1: admin, level=3, env=prod
            var ctx1 = new Dictionary<string, object>
            {
                ["role"] = "admin",
                ["level"] = 3,
                ["env"] = "prod"
            };
            Debug.Log("上下文1: admin, level=3, env=prod");
            stateTree.Run(ctx1);

            // 测试2: admin, level=3, env=dev
            var ctx2 = new Dictionary<string, object>
            {
                ["role"] = "admin",
                ["level"] = 3,
                ["env"] = "dev"
            };
            Debug.Log("上下文2: admin, level=3, env=dev");
            stateTree.Run(ctx2);

            // 测试3: admin, level=2
            var ctx3 = new Dictionary<string, object>
            {
                ["role"] = "admin",
                ["level"] = 2
            };
            Debug.Log("上下文3: admin, level=2");
            stateTree.Run(ctx3);

            // 测试4: user
            var ctx4 = new Dictionary<string, object>
            {
                ["role"] = "user"
            };
            Debug.Log("上下文4: user");
            stateTree.Run(ctx4);

            // 测试5: 未知角色（使用默认）
            var ctx5 = new Dictionary<string, object>
            {
                ["role"] = "guest"
            };
            Debug.Log("上下文5: guest (未知角色)");
            stateTree.Run(ctx5);
        }

        [ContextMenu("测试简单StateTree")]
        public void TestSimpleStateTree()
        {
            var stateTree = new StateTree();

            // 创建简单的状态机
            var modeNode = new StateTree { key = "mode" };
            stateTree.select["mode"] = modeNode;

            modeNode.select["edit"] = (Action)(() => Debug.Log("进入编辑模式"));
            modeNode.select["play"] = (Action)(() => Debug.Log("进入播放模式"));
            modeNode.select[StateTree.Default] = (Action)(() => Debug.Log("使用默认模式"));

            // 打印结构
            var sb = new StringBuilder();
            stateTree.Print(sb);
            Debug.Log("简单StateTree结构：\n" + sb.ToString());

            // 测试
            var ctx1 = new Dictionary<string, object> { ["mode"] = "edit" };
            var ctx2 = new Dictionary<string, object> { ["mode"] = "play" };
            var ctx3 = new Dictionary<string, object> { ["mode"] = "unknown" };

            Debug.Log("测试 edit 模式:");
            stateTree.Run(ctx1);

            Debug.Log("测试 play 模式:");
            stateTree.Run(ctx2);

            Debug.Log("测试未知模式:");
            stateTree.Run(ctx3);
        }
    }
}
