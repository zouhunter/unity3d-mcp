using System;
using System.Text;
using UnityEngine;

namespace UnityMcp.Tools
{
    /// <summary>
    /// StateTree 调试和测试
    /// </summary>
    public class StateTreeDebug : MonoBehaviour
    {
        [ContextMenu("调试StateTree输出")]
        public void DebugStateTreeOutput()
        {
            // 创建简单的测试结构
            var stateTree = new StateTree();

            // 创建role节点
            var roleNode = new StateTree { key = "role" };
            stateTree.select["role"] = roleNode;

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
            envNode.select["prod"] = (Action)(() => Debug.Log("AdminProd"));
            envNode.select["dev"] = (Action)(() => Debug.Log("AdminDev"));

            // 创建level 2叶子节点
            levelNode.select[2] = (Action)(() => Debug.Log("AdminL2"));

            // 创建user叶子节点
            roleNode.select["user"] = (Action)(() => Debug.Log("User"));

            // 创建默认分支
            roleNode.select[StateTree.Default] = (Action)(() => Debug.Log("Default"));

            // 打印树形结构
            var sb = new StringBuilder();
            stateTree.Print(sb);

            Debug.Log("当前StateTree 结构输出：\n" + sb.ToString());

            // 分析问题
            Debug.Log("问题分析：");
            Debug.Log("1. 当前输出显示所有节点都使用 └─ 符号");
            Debug.Log("2. 应该使用 ├─ 表示非最后一个子节点");
            Debug.Log("3. 应该使用 └─ 表示最后一个子节点");
            Debug.Log("4. 连接线 │ 应该正确显示");
        }

        [ContextMenu("测试简单结构")]
        public void TestSimpleStructure()
        {
            var stateTree = new StateTree();

            // 创建简单的三个节点结构
            var node1 = new StateTree { key = "node1" };
            stateTree.select["node1"] = node1;

            var node2 = new StateTree { key = "node2" };
            stateTree.select["node2"] = node2;

            var node3 = new StateTree { key = "node3" };
            stateTree.select["node3"] = node3;

            // 给node1添加子节点
            node1.select["leaf1"] = (Action)(() => Debug.Log("Leaf1"));
            node1.select["leaf2"] = (Action)(() => Debug.Log("Leaf2"));

            var sb = new StringBuilder();
            stateTree.Print(sb);

            Debug.Log("简单结构输出：\n" + sb.ToString());
        }
    }
}
