// [ContextMenu("测试修复后的输出")]
// public void TestFixedOutput()
// {
//     // 创建与您期望输出完全匹配的结构
//     var stateTree = new StateTree();

//     // 创建role节点
//     var roleNode = new StateTree { key = "role" };
//     stateTree.select["role"] = roleNode;

//     // 创建admin分支
//     var adminNode = new StateTree { key = "admin" };
//     roleNode.select["admin"] = adminNode;

//     // 创建level分支
//     var levelNode = new StateTree { key = "level" };
//     adminNode.select["level"] = levelNode;

//     // 创建level 3分支
//     var level3Node = new StateTree { key = "3" };
//     levelNode.select[3] = level3Node;

//     // 创建env分支
//     var envNode = new StateTree { key = "env" };
//     level3Node.select["env"] = envNode;

//     // 创建prod和dev叶子节点
//     envNode.select["prod"] = (Action)(() => Debug.Log("AdminProd"));
//     envNode.select["dev"] = (Action)(() => Debug.Log("AdminDev"));

//     // 创建level 2叶子节点
//     levelNode.select[2] = (Action)(() => Debug.Log("AdminL2"));

//     // 创建user叶子节点
//     roleNode.select["user"] = (Action)(() => Debug.Log("User"));

//     // 创建默认分支
//     roleNode.select[StateTree.Default] = (Action)(() => Debug.Log("Default"));

//     // 打印树形结构
//     var sb = new StringBuilder();
//     stateTree.Print(sb);

//     Debug.Log("修复后的StateTree 结构输出：\n" + sb.ToString());

//     // 显示期望的输出格式
//     Debug.Log("期望的输出格式：\n" +
//         "StateTree\n" +
//         "└─ role\n" +
//         "   ├─ admin\n" +
//         "   │  └─ level\n" +
//         "   │     ├─ 3\n" +
//         "   │     │  └─ env\n" +
//         "   │     │     ├─ prod → AdminProd\n" +
//         "   │     │     └─ dev  → AdminDev\n" +
//         "   │     └─ 2 → AdminL2\n" +
//         "   ├─ user → User\n" +
//         "   └─ * → Default");
// }
