# Unity3d MCP 系统说明文档

## 目录
1. [系统概述](#系统概述)
2. [设计架构](#设计架构)
3. [源码解析](#源码解析)
4. [使用方法](#使用方法)
5. [创新点](#创新点)
6. [技术特性](#技术特性)
7. [部署指南](#部署指南)
8. [API参考](#api参考)
9. [故障排除](#故障排除)

---

## 系统概述

Unity3d MCP (Model Context Protocol) 是一个创新的AI-Unity集成系统，它通过MCP协议将AI助手（如Cursor、Claude、Trae）与Unity编辑器无缝连接，实现AI驱动的Unity开发工作流。

### 核心价值
- **AI驱动开发**：通过自然语言指令控制Unity编辑器
- **无缝集成**：支持主流AI客户端，无需修改现有工作流
- **功能丰富**：提供30+专业工具，覆盖Unity开发全流程
- **高性能**：基于TCP Socket的高效通信机制
- **可扩展**：模块化设计，易于添加新功能

### 系统组成
- **MCP Server** (Python)：基于FastMCP的服务器端
- **Unity Package** (C#)：Unity编辑器插件
- **工具生态**：30+专业Unity开发工具
- **通信协议**：基于TCP Socket的JSON-RPC通信

---

## 设计架构

### 整体架构

系统采用分层架构设计，从上到下分为：

1. **AI客户端层**：Cursor、Claude、Trae等AI助手
2. **MCP协议层**：Python MCP Server + Unity Package
3. **通信层**：TCP Socket (6400-6405端口) + JSON-RPC
4. **Unity编辑器层**：Unity Editor + Unity API
5. **工具层**：30+专业工具 + 状态树执行引擎

#### 系统架构图

![Unity3d MCP 系统架构图](doc/architecture.png)

*图1：Unity3d MCP系统整体架构图，展示了从AI客户端到Unity编辑器的完整数据流和组件关系*

详细架构图请参考：[doc/architecture.md](doc/architecture.md)

### 核心设计原则

#### 1. 双层调用架构
```
AI客户端 → FacadeTools → MethodTools → Unity API
```

- **FacadeTools**：`single_call` 和 `batch_call` 两个门面工具
- **MethodTools**：30+专业功能方法，仅通过FacadeTools调用

#### 2. 状态树执行引擎
- 基于状态模式的路由系统
- 支持参数验证和类型转换
- 提供统一的错误处理机制

#### 3. 智能连接管理
- 多端口自动发现 (6400-6405)
- 连接健康检查和自动重连
- 失败端口记录和智能切换

---

## 源码解析

### 1. Server端架构 (Python)

#### 核心文件结构
```
server/
├── server.py              # FastMCP服务器入口
├── config.py              # 配置管理
├── unity_connection.py    # Unity连接管理
├── tools/                 # 工具模块
│   ├── __init__.py       # 工具注册
│   ├── call_up.py        # 门面工具
│   ├── console.py        # 控制台工具
│   ├── hierarchy_*.py    # 层级管理工具
│   ├── edit_*.py         # 资源编辑工具
│   └── ...
└── requirements.txt       # 依赖管理
```

#### 关键组件解析

**1. FastMCP服务器 (server.py)**
```python
# 服务器生命周期管理
@asynccontextmanager
async def server_lifespan(server: FastMCP):
    # 启动时连接Unity
    _unity_connection = get_unity_connection()
    yield {"bridge": _unity_connection}
    # 关闭时清理连接

# 工具注册
register_all_tools(mcp)
```

**2. Unity连接管理 (unity_connection.py)**
```python
class UnityConnection:
    def connect(self, force_reconnect: bool = False) -> bool:
        # 多端口自动发现
        # 连接健康检查
        # 失败端口记录
        
    def send_command(self, command: dict) -> dict:
        # JSON序列化
        # TCP发送
        # 响应解析
```

**3. 工具注册系统 (tools/__init__.py)**
```python
def register_all_tools(mcp):
    """注册所有重构后的工具"""
    register_call_tools(mcp)      # 门面工具
    register_console_tools(mcp)   # 控制台工具
    register_hierarchy_*.py       # 层级工具
    # ... 30+工具注册
```

### 2. Unity端架构 (C#)

#### 核心文件结构
```
unity-package/
├── Runtime/                    # 运行时核心
│   ├── StateTree.cs           # 状态树引擎
│   ├── StateTreeContext.cs    # 执行上下文
│   └── CoroutineRunner.cs     # 协程运行器
├── Editor/                     # 编辑器扩展
│   ├── Connection/            # 连接管理
│   │   └── McpConnect.cs     # TCP连接核心
│   ├── Executer/              # 执行器
│   │   ├── SingleCall.cs     # 单次调用
│   │   ├── BatchCall.cs      # 批量调用
│   │   └── StateMethodBase.cs # 状态方法基类
│   ├── Tools/                 # 工具实现
│   │   ├── Hierarchy/        # 层级管理
│   │   ├── ResEdit/          # 资源编辑
│   │   ├── UI/               # UI工具
│   │   └── ...
│   └── Model/                 # 数据模型
│       ├── Command.cs        # 命令模型
│       └── Response.cs       # 响应模型
└── package.json              # 包配置
```

#### 关键组件解析

**1. 状态树引擎 (StateTree.cs)**
```csharp
public class StateTree
{
    public string key;                                    // 当前层变量
    public Dictionary<object, StateTree> select = new(); // 选择分支
    public Func<StateTreeContext, object> contextFunc;   // 叶子函数
    
    public object Run(StateTreeContext ctx)
    {
        // 状态树路由逻辑
        // 参数验证和类型转换
        // 方法执行和结果处理
    }
}
```

**2. TCP连接管理 (McpConnect.cs)**
```csharp
public static partial class McpConnect
{
    private static TcpListener listener;
    private static Dictionary<string, ClientInfo> connectedClients;
    
    public static void StartServer()
    {
        // 多端口监听 (6400-6405)
        // 客户端连接管理
        // 命令队列处理
    }
    
    public static string SendCommand(JObject command)
    {
        // JSON序列化
        // TCP发送
        // 响应等待
    }
}
```

**3. 门面工具 (SingleCall.cs / BatchCall.cs)**
```csharp
public class SingleCall : McpTool
{
    public override void HandleCommand(JObject cmd, Action<object> callback)
    {
        string functionName = cmd["func"]?.ToString();
        string argsJson = cmd["args"]?.ToString();
        
        // 反射调用目标方法
        ExecuteFunction(functionName, argsJson, callback);
    }
}
```

### 3. 工具生态架构

#### 工具分类体系
1. **层级管理工具**：`hierarchy_create`, `hierarchy_search`, `hierarchy_apply`
2. **资源编辑工具**：`edit_gameobject`, `edit_component`, `edit_material`
3. **项目管理工具**：`project_search`, `project_operate`
4. **UI开发工具**：`ugui_layout`, `ui_rule_manage`
5. **网络工具**：`request_http`, `figma_manage`
6. **编辑器工具**：`manage_editor`, `gameplay`, `console_write`

#### 工具实现模式
```csharp
[ToolName("tool_name", "工具描述")]
public class ToolClass : StateMethodBase
{
    protected override MethodKey[] CreateKeys()
    {
        return new[]
        {
            new MethodKey("param1", "参数描述", false),
            new MethodKey("param2", "参数描述", true)
        };
    }
    
    protected override StateTree CreateStateTree()
    {
        return StateTreeBuilder
            .Create()
            .Key("action")
                .Leaf("action1", HandleAction1)
                .Leaf("action2", HandleAction2)
            .Build();
    }
}
```

---

## 使用方法

### 1. 环境准备

#### 系统要求
- Unity 2020.3+ (推荐 2022.3.61f1c1)
- Python 3.8+
- 支持MCP协议的AI客户端 (Cursor/Claude/Trae)

#### 依赖安装
```bash
# Python依赖
cd server
pip install -r requirements.txt

# Unity Package
# 将unity-package导入Unity项目
```

### 2. 配置设置

#### MCP客户端配置
在AI客户端的MCP配置文件中添加：

**Cursor配置** (`~/.cursor/mcp.json`)：
```json
{
  "mcpServers": {
    "unityMCP": {
      "command": "uv",
      "args": [
        "--directory",
        "D:/unity-mcp/server",
        "run",
        "server.py"
      ]
    }
  }
}
```

**Claude配置** (`~/AppData/Roaming/Claude/claude_desktop_config.json`)：
```json
{
  "mcpServers": {
    "unityMCP": {
      "command": "uv",
      "args": [
        "--directory",
        "D:/unity-mcp/server",
        "run",
        "server.py"
      ]
    }
  }
}
```

### 3. 启动流程

#### 1. 启动Unity编辑器
```bash
# 打开Unity项目
# Unity Package会自动启动TCP服务器
```

#### 2. 启动MCP服务器
```bash
cd server
python server.py
```

#### 3. 验证连接
在AI客户端中测试连接：
```
请帮我创建一个Cube对象
```

### 4. 基本使用示例

#### 创建GameObject
```python
# 通过AI客户端发送指令
"创建一个名为Player的Cube对象"
```

#### 批量操作
```python
# 批量创建多个对象
"创建5个Enemy对象，位置分别为(0,0,0), (1,0,0), (2,0,0), (3,0,0), (4,0,0)"
```

#### 资源管理
```python
# 下载并应用图片
"下载一张随机图片并应用到Image组件"
```

### 5. 高级用法

#### 自定义工具开发
1. 在`server/tools/`目录创建新工具文件
2. 实现工具逻辑和参数定义
3. 在`tools/__init__.py`中注册工具
4. 重启MCP服务器

#### 批量操作优化
```python
# 使用batch_call提高性能
{
  "func": "batch_call",
  "args": {
    "funcs": [
      {"func": "hierarchy_create", "args": {...}},
      {"func": "edit_gameobject", "args": {...}},
      {"func": "edit_component", "args": {...}}
    ]
  }
}
```

---

## 创新点

### 1. 双层调用架构
**创新描述**：设计了FacadeTools + MethodTools的双层架构
- **FacadeTools**：`single_call`和`batch_call`两个门面工具
- **MethodTools**：30+专业功能方法，仅通过门面工具调用

**技术优势**：
- 统一的调用接口，简化AI客户端使用
- 批量操作支持，提高执行效率
- 参数验证和错误处理集中化

### 2. 状态树执行引擎
**创新描述**：基于状态模式的路由系统
```csharp
StateTreeBuilder
    .Create()
    .Key("action")
        .Leaf("create", HandleCreate)
        .Leaf("edit", HandleEdit)
        .Leaf("delete", HandleDelete)
    .Build();
```

**技术优势**：
- 灵活的参数路由和验证
- 支持可选参数和默认值
- 统一的错误处理机制

### 3. 智能连接管理
**创新描述**：多端口自动发现和智能切换
- 端口范围：6400-6405
- 失败端口记录和冷却机制
- 连接健康检查和自动重连

**技术优势**：
- 提高连接成功率
- 减少端口冲突
- 自动故障恢复

### 4. 协程支持
**创新描述**：支持Unity协程的异步操作
```csharp
IEnumerator DownloadFileAsync(string url, string savePath, ...)
{
    // 异步下载逻辑
    yield return null;
}
```

**技术优势**：
- 不阻塞主线程
- 支持长时间运行的操作
- 提供进度回调

### 5. 文件数据智能处理
**创新描述**：自动识别文件类型，优化响应数据
- 自动检测图片、视频、音频等文件类型
- 大型内容不返回实际数据，只返回元数据
- 提供文件路径和基本信息

**技术优势**：
- 减少内存使用
- 提高网络传输效率
- 保持响应格式一致性

---

## 技术特性

### 1. 高性能通信
- **TCP Socket**：低延迟、高吞吐量
- **JSON-RPC**：标准化协议，易于调试
- **连接池**：复用连接，减少开销
- **批量操作**：支持批量调用，提高效率

### 2. 可靠性保障
- **多端口支持**：6400-6405端口范围
- **自动重连**：连接断开自动恢复
- **错误处理**：完善的异常处理机制
- **超时控制**：防止长时间阻塞

### 3. 扩展性设计
- **模块化架构**：工具独立，易于扩展
- **反射调用**：动态方法调用
- **插件化**：支持自定义工具开发
- **配置化**：灵活的参数配置

### 4. 开发体验
- **自然语言**：通过AI助手自然交互
- **实时反馈**：即时执行结果反馈
- **调试支持**：详细的日志和错误信息
- **文档完善**：完整的API文档和示例

---

## 部署指南

### 1. 开发环境部署

#### 步骤1：克隆项目
```bash
git clone <repository-url>
cd unity-mcp
```

#### 步骤2：配置Python环境
```bash
cd server
python -m venv .venv
source .venv/bin/activate  # Linux/Mac
# 或
.venv\Scripts\activate     # Windows

pip install -r requirements.txt
```

#### 步骤3：导入Unity Package
1. 打开Unity编辑器
2. 选择 `Window > Package Manager`
3. 点击 `+ > Add package from disk`
4. 选择 `unity-package/package.json`

#### 步骤4：配置MCP客户端
参考[使用方法](#使用方法)中的配置部分

### 2. 生产环境部署

#### Docker部署
```dockerfile
FROM python:3.9-slim
WORKDIR /app
COPY server/ .
RUN pip install -r requirements.txt
EXPOSE 6400-6405
CMD ["python", "server.py"]
```

#### 系统服务部署
```bash
# 创建systemd服务文件
sudo nano /etc/systemd/system/unity-mcp.service

[Unit]
Description=Unity3d MCP Server
After=network.target

[Service]
Type=simple
User=unity
WorkingDirectory=/opt/unity-mcp/server
ExecStart=/opt/unity-mcp/server/.venv/bin/python server.py
Restart=always

[Install]
WantedBy=multi-user.target
```

### 3. 监控和维护

#### 日志监控
```bash
# 查看服务器日志
tail -f server.log

# 查看Unity控制台
# 在Unity编辑器中查看Console窗口
```

#### 性能监控
- 连接数监控
- 响应时间统计
- 错误率统计
- 资源使用情况

---

## API参考

### 1. 门面工具API

#### single_call
单次函数调用工具
```json
{
  "func": "single_call",
  "args": {
    "func": "hierarchy_create",
    "args": {
      "name": "Player",
      "primitive_type": "Cube",
      "source": "primitive"
    }
  }
}
```

#### batch_call
批量函数调用工具
```json
{
  "func": "batch_call",
  "args": {
    "funcs": [
      {
        "func": "hierarchy_create",
        "args": {"name": "Player", "primitive_type": "Cube"}
      },
      {
        "func": "edit_gameobject",
        "args": {"path": "Player", "position": [0, 1, 0]}
      }
    ]
  }
}
```

### 2. 核心工具API

#### 层级管理工具
- `hierarchy_create`：创建GameObject
- `hierarchy_search`：搜索GameObject
- `hierarchy_apply`：应用预制体

#### 资源编辑工具
- `edit_gameobject`：编辑GameObject属性
- `edit_component`：编辑组件属性
- `edit_material`：编辑材质
- `edit_texture`：编辑纹理

#### 项目管理工具
- `project_search`：搜索项目资源
- `project_operate`：项目操作

#### 网络工具
- `request_http`：HTTP请求
- `figma_manage`：Figma资源管理

### 3. 响应格式

#### 成功响应
```json
{
  "success": true,
  "message": "操作成功",
  "data": {
    "result": "具体结果数据"
  }
}
```

#### 错误响应
```json
{
  "success": false,
  "message": "错误描述",
  "error": "详细错误信息"
}
```

---

## 故障排除

### 1. 连接问题

#### 问题：无法连接到Unity
**可能原因**：
- Unity编辑器未启动
- 端口被占用
- 防火墙阻止连接

**解决方案**：
1. 确认Unity编辑器已启动
2. 检查端口6400-6405是否可用
3. 检查防火墙设置
4. 查看Unity控制台错误信息

#### 问题：连接频繁断开
**可能原因**：
- 网络不稳定
- Unity编辑器卡顿
- 超时设置过短

**解决方案**：
1. 检查网络连接
2. 优化Unity项目性能
3. 调整超时设置

### 2. 工具执行问题

#### 问题：工具调用失败
**可能原因**：
- 参数格式错误
- 目标对象不存在
- 权限不足

**解决方案**：
1. 检查参数格式和类型
2. 确认目标对象存在
3. 检查Unity编辑器权限

#### 问题：批量操作部分失败
**可能原因**：
- 某些操作依赖其他操作
- 资源冲突
- 内存不足

**解决方案**：
1. 调整操作顺序
2. 检查资源冲突
3. 分批执行操作

### 3. 性能问题

#### 问题：响应速度慢
**可能原因**：
- 网络延迟
- Unity编辑器性能
- 操作复杂度高

**解决方案**：
1. 优化网络环境
2. 关闭不必要的Unity功能
3. 简化操作逻辑

#### 问题：内存使用过高
**可能原因**：
- 大量对象创建
- 资源未释放
- 协程泄漏

**解决方案**：
1. 及时销毁不需要的对象
2. 释放未使用的资源
3. 检查协程生命周期

### 4. 调试技巧

#### 启用详细日志
```python
# 在config.py中设置
log_level: str = "DEBUG"
```

#### Unity控制台调试
```csharp
// 在Unity中启用详细日志
McpConnect.EnableLog = true;
```

#### 网络抓包分析
使用Wireshark等工具分析TCP通信数据

---

## 总结

Unity3d MCP系统是一个创新的AI-Unity集成解决方案，通过MCP协议实现了AI助手与Unity编辑器的无缝连接。系统采用双层调用架构、状态树执行引擎、智能连接管理等创新技术，提供了30+专业工具，覆盖Unity开发全流程。

### 核心优势
1. **AI驱动**：通过自然语言控制Unity编辑器
2. **功能丰富**：30+专业工具，覆盖开发全流程
3. **高性能**：基于TCP Socket的高效通信
4. **可扩展**：模块化设计，易于扩展
5. **易用性**：支持主流AI客户端，无需修改工作流

### 应用场景
- AI辅助游戏开发
- 自动化资源管理
- 批量操作优化
- 开发流程自动化
- 教育和培训

### 未来发展方向
1. 更多Unity工具支持
2. 可视化工具开发
3. 性能优化和监控
4. 云端部署支持
5. 多平台兼容性

通过Unity3d MCP系统，开发者可以享受AI驱动的Unity开发体验，提高开发效率，降低学习成本，实现更智能的游戏开发工作流。

---

*文档版本：v1.0*  
*最后更新：2025年09月*  
*维护团队：Unity3d MCP Development Team*
