# Unity-MCP项目技术分析

## 一、整体架构图

Unity-MCP（Unity上下文协议工具）是一个轻量级、可扩展的Unity编辑器通信桥接系统，支持Unity与外部服务的高效通信。

### 1.1 架构概览

```
┌─────────────────────────────────────────────────────────────┐
│                     Unity Editor                            │
│  ┌─────────────────┐  ┌─────────────────┐  ┌─────────────── │
│  │  Editor Window  │  │  Method Registry│  │  State Tree   │
│  │  - MCP Server   │  │  - Function     │  │  - Route      │
│  │  - Client List  │  │    Discovery    │  │    Logic      │
│  │  - Port Status  │  │  - Auto Scan    │  │  - Execution  │
│  └─────────────────┘  └─────────────────┘  └─────────────── │
│                                                             │
│  ┌─────────────────────────────────────────────────────────┐
│  │                 MCP Connect (TCP Server)                │
│  │  - Port Range: 6400-6405                               │
│  │  - Client Management                                   │
│  │  - Command Queue                                       │
│  │  - JSON Protocol                                       │
│  └─────────────────────────────────────────────────────────┘
└─────────────────────────────────────────────────────────────┘
                                │
                                ▼
┌─────────────────────────────────────────────────────────────┐
│              External MCP Clients                           │
│  ┌─────────────────┐  ┌─────────────────┐  ┌─────────────── │
│  │   Cursor IDE    │  │   Python/Node   │  │   CI/CD       │
│  │   Extensions    │  │   Scripts       │  │   Pipelines   │
│  └─────────────────┘  └─────────────────┘  └─────────────── │
└─────────────────────────────────────────────────────────────┘
```

### 1.2 核心组件结构

```
unity-mcp/
├── Editor/                          # 编辑器相关功能
│   ├── Connection/                  # 网络连接管理
│   │   ├── McpConnect.cs           # TCP服务器核心
│   │   ├── Command.cs              # 命令数据模型
│   │   └── Response.cs             # 响应数据模型
│   ├── Methods/                     # 具体功能方法
│   │   ├── StateMethodBase.cs      # 状态方法基类
│   │   ├── ManageAsset.cs          # 资产管理
│   │   ├── ManageGameObject.cs     # GameObject管理
│   │   ├── ManageScene.cs          # 场景管理
│   │   └── ...                     # 其他方法
│   ├── Tools/                       # 工具类
│   │   ├── FunctionCall.cs         # 函数调用路由
│   │   ├── IToolMethod.cs          # 工具接口
│   │   └── MainThreadExecutor.cs   # 主线程执行器
│   └── UnityMcpEditorWindow.cs     # 主控制窗口
└── Runtime/                         # 运行时组件
    ├── StateTree.cs                # 状态树核心
    └── StateTreeBuilder.cs         # 状态树构建器
```

## 二、控制界面与规则文件

### 2.1 主控制界面（UnityMcpEditorWindow）

Unity MCP提供了一个直观的编辑器窗口，位于`Window > Unity MCP`菜单下：

#### 2.1.1 界面功能区域

1. **服务状态区**
   - 运行状态指示器（绿色/红色点）
   - 端口信息显示（6400-6405范围）
   - 启动/停止按钮
   - 日志开关控制

2. **客户端连接监控**
   - 实时连接数显示
   - 客户端详细信息列表
   - 连接时长统计
   - 命令执行次数

3. **工具方法列表**
   - 可折叠的方法树
   - 参数说明（必需/可选）
   - 状态树预览
   - 快速定位脚本文件

#### 2.1.2 关键代码片段

```csharp
// 端口范围配置
public static readonly int unityPortStart = 6400;
public static readonly int unityPortEnd = 6405;

// 自动端口选择机制
for (int port = unityPortStart; port <= unityPortEnd; port++)
{
    try
    {
        listener = new TcpListener(IPAddress.Loopback, port);
        listener.Start();
        currentPort = port;
        isRunning = true;
        break;
    }
    catch (SocketException ex)
    {
        // 端口被占用，尝试下一个
    }
}
```

### 2.2 规则文件系统（unity-mcp.mdc）

规则文件采用分层结构，清晰定义了工具调用规范：

#### 2.2.1 工具分类

**Tools（顶层调用）**
- `function_call`: 单次函数调用
- `functions_call`: 批量函数调用

**Methods（具体功能）**
- `manage_asset`: 资产管理
- `manage_gameobject`: GameObject操作
- `manage_scene`: 场景管理
- `manage_script`: 脚本管理
- `manage_editor`: 编辑器控制
- `manage_network`: 网络操作
- `read_console`: 控制台读取
- `execute_menu_item`: 菜单执行

#### 2.2.2 调用规范示例

```python
# 单次调用示例
function_call(
    func="manage_gameobject",
    args='{"action":"create","name":"Cube","primitive_type":"Cube","position":[0,0,0]}'
)

# 批量调用示例
functions_call(
    function_calls='[
        {"func":"manage_gameobject","args":"{\\"action\\":\\"create\\",\\"name\\":\\"Enemy\\",\\"primitive_type\\":\\"Cube\\"}"},
        {"func":"manage_gameobject","args":"{\\"action\\":\\"add_component\\",\\"target\\":\\"Enemy\\",\\"component_name\\":\\"Rigidbody\\"}"}
    ]'
)
```

## 三、状态树详解

### 3.1 状态树设计理念

状态树（StateTree）是Unity MCP的核心路由机制，提供了一种基于参数的动态方法分发系统。

#### 3.1.1 核心特性

- **参数驱动路由**: 根据输入参数自动选择执行路径
- **可选参数支持**: 灵活处理可选参数分支
- **默认分支机制**: 提供兜底处理逻辑
- **错误处理**: 详细的参数验证和错误提示

#### 3.1.2 状态树结构

```csharp
public class StateTree
{
    public string key;                              // 当前层变量键
    public Dictionary<object, StateTree> select;   // 分支选择字典
    public HashSet<string> optionalParams;         // 可选参数集合
    public Func<JObject, object> func;             // 叶子执行函数
    public const string Default = "*";             // 默认分支标识
}
```

### 3.2 状态树构建器（StateTreeBuilder）

提供了流畅的API来构建复杂的状态树：

```csharp
StateTreeBuilder
    .Create()
    .Key("action")                          // 设置路由键
        .Branch("create")                   // 创建分支
            .OptionalLeaf("prefab_path", HandleCreateFromPrefab)    // 可选参数处理
            .OptionalKey("primitive_type")   // 可选参数子分支
                .Leaf("Cube", HandleCreateCube)                     // 具体实现
                .Leaf("Sphere", HandleCreateSphere)
                .DefaultLeaf(HandleCreateFromPrimitive)             // 默认处理
            .Up()
            .DefaultLeaf(HandleCreateEmpty)  // 默认创建空对象
        .Up()
        .Leaf("modify", HandleModifyAction)  // 其他操作
    .Build();
```

### 3.3 状态树执行流程

#### 3.3.1 路由算法

```csharp
public object Run(JObject ctx)
{
    var cur = this;
    while (cur.func == null)
    {
        object keyToLookup = Default;
        StateTree next = null;

        // 1. 常规参数匹配
        if (!string.IsNullOrEmpty(cur.key) && ctx.TryGetValue(cur.key, out JToken token))
        {
            keyToLookup = ConvertTokenToKey(token);
            cur.select.TryGetValue(keyToLookup, out next);
        }

        // 2. 可选参数检查
        if (next == null && ctx != null)
        {
            foreach (var kvp in cur.select)
            {
                string key = kvp.Key.ToString();
                if (cur.optionalParams.Contains(key) && 
                    ctx.TryGetValue(key, out JToken paramToken) &&
                    paramToken != null)
                {
                    next = kvp.Value;
                    break;
                }
            }
        }

        // 3. 默认分支处理
        if (next == null && !cur.select.TryGetValue(Default, out next))
        {
            // 生成详细错误信息
            ErrorMessage = GenerateErrorMessage(cur, keyToLookup);
            return null;
        }
        cur = next;
    }
    return cur.func?.Invoke(ctx);
}
```

#### 3.3.2 状态树可视化

状态树支持美化打印，便于调试：

```
StateTree
└─ action:
   ├─ create
   │  ├─ prefab_path(option) → HandleCreateFromPrefab
   │  ├─ primitive_type(option)
   │  │  ├─ Cube → HandleCreateCube
   │  │  ├─ Sphere → HandleCreateSphere
   │  │  └─ * → HandleCreateFromPrimitive
   │  └─ * → HandleCreateEmpty
   ├─ modify → HandleModifyAction
   └─ delete → HandleDeleteAction
```

## 四、自定义扩展

### 4.1 扩展方法开发

#### 4.1.1 继承StateMethodBase

创建自定义方法需要继承`StateMethodBase`基类：

```csharp
[ToolName("custom_method")]  // 指定工具名称
public class CustomMethod : StateMethodBase
{
    // 1. 定义参数键
    protected override MethodKey[] CreateKeys()
    {
        return new[]
        {
            new MethodKey("action", "操作类型", false),
            new MethodKey("target", "目标对象", true),
            new MethodKey("value", "设置值", true)
        };
    }

    // 2. 构建状态树
    protected override StateTree CreateStateTree()
    {
        return StateTreeBuilder
            .Create()
            .Key("action")
                .Leaf("get", GetValue)
                .Leaf("set", SetValue)
                .DefaultLeaf(DefaultAction)
            .Build();
    }

    // 3. 实现具体方法
    private object GetValue(JObject args)
    {
        string target = args["target"]?.ToString();
        // 实现获取逻辑
        return Response.Success("获取成功", new { target, value = "..." });
    }

    private object SetValue(JObject args)
    {
        string target = args["target"]?.ToString();
        string value = args["value"]?.ToString();
        // 实现设置逻辑
        return Response.Success("设置成功");
    }

    private object DefaultAction(JObject args)
    {
        return Response.Error("不支持的操作");
    }
}
```

#### 4.1.2 自动注册机制

Unity MCP采用反射机制自动发现和注册工具方法：

```csharp
// FunctionCall.cs 中的自动注册逻辑
var methodTypes = AppDomain.CurrentDomain.GetAssemblies()
    .SelectMany(a => a.GetTypes())
    .Where(t => typeof(IToolMethod).IsAssignableFrom(t) &&
               !t.IsInterface &&
               !t.IsAbstract);

foreach (var methodType in methodTypes)
{
    var methodInstance = Activator.CreateInstance(methodType) as IToolMethod;
    string methodName = GetMethodName(methodType);  // 支持ToolNameAttribute
    _registeredMethods[methodName] = methodInstance;
}
```

### 4.2 工具名称映射

#### 4.2.1 命名策略

1. **ToolNameAttribute**：显式指定工具名称
2. **自动转换**：PascalCase → snake_case

```csharp
// 显式命名
[ToolName("manage_custom_asset")]
public class CustomAssetManager : StateMethodBase { }

// 自动转换：ManageGameObject → manage_game_object
public class ManageGameObject : StateMethodBase { }
```

#### 4.2.2 转换实现

```csharp
private static string ConvertToSnakeCase(string pascalCase)
{
    if (string.IsNullOrEmpty(pascalCase))
        return pascalCase;

    // 在大写字母前插入下划线
    return Regex.Replace(pascalCase, "(?<!^)([A-Z])", "_$1").ToLower();
}
```

### 4.3 复杂状态树示例

以ManageAsset为例，展示复杂状态树的构建：

```csharp
protected override StateTree CreateStateTree()
{
    return StateTreeBuilder
        .Create()
        .Key("action")
            .Leaf("import", ReimportAsset)
            .Leaf("create", CreateAsset)
            .Leaf("modify", ModifyAsset)
            .Leaf("delete", DeleteAsset)
            .Leaf("duplicate", DuplicateAsset)
            .Leaf("move", MoveOrRenameAsset)
            .Leaf("rename", MoveOrRenameAsset)
            .Leaf("search", SearchAssets)
            .Leaf("get_info", GetAssetInfo)
            .Leaf("create_folder", CreateFolder)
            .Leaf("get_components", GetComponentsFromAsset)
        .Build();
}
```

## 五、使用实例

### 5.1 基础GameObject操作

#### 5.1.1 创建游戏对象

```python
# 创建基础立方体
function_call(
    func="manage_gameobject",
    args='{"action":"create","name":"MyCube","primitive_type":"Cube","position":[0,1,0]}'
)

# 从预制体创建
function_call(
    func="manage_gameobject", 
    args='{"action":"create","name":"Player","prefab_path":"Assets/Prefabs/Player.prefab"}'
)
```

#### 5.1.2 组件管理

```python
# 添加组件
function_call(
    func="manage_gameobject",
    args='{"action":"add_component","target":"MyCube","component_name":"Rigidbody"}'
)

# 设置组件属性
function_call(
    func="manage_gameobject",
    args='{"action":"set_component_property","target":"MyCube","component_name":"Rigidbody","property":"mass","value":2.0}'
)
```

### 5.2 资产管理操作

#### 5.2.1 创建和导入资产

```python
# 创建材质
function_call(
    func="manage_asset",
    args='{"action":"create","path":"Assets/Materials/NewMaterial.mat","asset_type":"Material"}'
)

# 导入纹理
function_call(
    func="manage_asset",
    args='{"action":"import","path":"Assets/Textures/wall.png"}'
)
```

#### 5.2.2 资产搜索

```python
# 搜索所有预制体
function_call(
    func="manage_asset",
    args='{"action":"search","search_pattern":"*.prefab","recursive":true}'
)
```

### 5.3 批量操作示例

#### 5.3.1 创建完整场景

```python
# 批量创建游戏场景
functions_call(
    function_calls='[
        {"func":"manage_gameobject","args":"{\\"action\\":\\"create\\",\\"name\\":\\"Ground\\",\\"primitive_type\\":\\"Plane\\",\\"scale\\":[10,1,10]}"},
        {"func":"manage_gameobject","args":"{\\"action\\":\\"create\\",\\"name\\":\\"Player\\",\\"prefab_path\\":\\"Assets/Prefabs/Player.prefab\\",\\"position\\":[0,1,0]}"},
        {"func":"manage_gameobject","args":"{\\"action\\":\\"create\\",\\"name\\":\\"Enemy\\",\\"primitive_type\\":\\"Cube\\",\\"position\\":[5,0.5,5]}"},
        {"func":"manage_gameobject","args":"{\\"action\\":\\"add_component\\",\\"target\\":\\"Enemy\\",\\"component_name\\":\\"Rigidbody\\"}"},
        {"func":"manage_asset","args":"{\\"action\\":\\"create\\",\\"path\\":\\"Assets/Materials/EnemyMaterial.mat\\",\\"asset_type\\":\\"Material\\"}"}
    ]'
)
```

### 5.4 网络工具使用

#### 5.4.1 HTTP请求

```python
# GET请求
function_call(
    func="manage_network",
    args='{"action":"get","url":"https://api.example.com/data","timeout":10}'
)

# POST请求
function_call(
    func="manage_network", 
    args='{"action":"post","url":"https://api.example.com/submit","data":{"key":"value"},"timeout":15}'
)
```

#### 5.4.2 网络测试

Unity MCP提供了专门的网络测试窗口（`Unity MCP > Test Network Tool`），可以：

- 测试URL连通性
- 验证HTTP请求
- 调试网络配置

### 5.5 控制台日志读取

```python
# 读取最新日志
function_call(
    func="read_console",
    args='{"action":"get_recent","count":10}'
)

# 按级别过滤日志
function_call(
    func="read_console",
    args='{"action":"filter","level":"Error","since":"2024-01-01"}'
)
```

### 5.6 自定义GM命令示例

项目中包含了一个简单的GM命令示例：

```csharp
[ToolName("gm_command")]
public class GMCommandMethod : StateMethodBase
{
    protected override MethodKey[] CreateKeys()
    {
        return new[]
        {
            new MethodKey("action", "操作类型，默认执行主要功能", true)
        };
    }

    protected override StateTree CreateStateTree()
    {
        return StateTreeBuilder.Create()
            .Key("action")
            .DefaultLeaf(Execute)
            .Build();
    }

    private object Execute(JObject args)
    {
        return Response.Success("Test");
    }
}
```

### 5.7 错误处理和调试

#### 5.7.1 错误响应格式

```json
{
  "status": "error",
  "error": "Invalid value 'invalid_action' for key 'action'. Supported values: [create, modify, delete, find]",
  "command": "manage_gameobject"
}
```

#### 5.7.2 成功响应格式

```json
{
  "status": "success", 
  "result": {
    "message": "GameObject created successfully",
    "data": {
      "name": "MyCube",
      "instanceId": 12345,
      "position": [0, 1, 0]
    }
  }
}
```

## 总结

Unity-MCP项目展现了一个设计精良的插件通信架构：

1. **模块化设计**：清晰的组件分层，便于扩展和维护
2. **智能路由**：基于状态树的参数驱动路由机制
3. **自动发现**：反射机制实现的工具自动注册
4. **友好界面**：直观的编辑器窗口和详细的文档
5. **灵活扩展**：简单的继承体系支持快速开发新功能

该项目为Unity编辑器自动化、外部工具集成和开发流水线构建提供了强大的基础框架。
