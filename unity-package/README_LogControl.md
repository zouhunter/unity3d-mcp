# Unity MCP 日志控制功能

## 概述

Unity MCP 现在支持全局日志控制功能，可以通过 `EnableLog` 开关来控制所有相关的日志输出。

## 功能特性

- **全局日志控制**：通过单个开关控制所有 UnityMcp 相关的日志输出
- **持久化设置**：日志设置会自动保存到 EditorPrefs，重启 Unity 后设置会保持
- **实时切换**：可以在运行时动态开启/关闭日志输出
- **统一管理**：所有相关文件的日志输出都受到统一控制

## 使用方法

### 方法一：通过编辑器窗口控制

1. 打开 Unity 编辑器
2. 在菜单栏选择 `Window > Unity MCP` 打开 MCP 服务管理窗口
3. 在控制面板中找到"启用日志输出"选项
4. 勾选或取消勾选来控制日志输出

### 方法二：通过测试窗口

1. 在菜单栏选择 `Window > Unity MCP > 测试日志控制`
2. 在测试窗口中可以：
   - 查看当前日志状态
   - 测试日志输出功能
   - 切换日志开关状态

### 方法三：通过代码控制

```csharp
// 启用日志输出
UnityMcp.EnableLog = true;

// 禁用日志输出
UnityMcp.EnableLog = false;

// 保存设置到 EditorPrefs
EditorPrefs.SetBool("mcp_enable_log", true);
```

## 受控制的文件

以下文件中的日志输出都受到 `EnableLog` 开关控制：

- `UnityMcpBridge.cs` - 主要的 MCP 桥接服务
- `FunctionCall.cs` - 函数调用处理
- `ReadConsole.cs` - 控制台读取功能
- `ManageEditor.cs` - 编辑器管理功能
- `ExecuteMenuItem.cs` - 菜单项执行功能
- `ManageScript.cs` - 脚本管理功能

## 日志类型

支持控制的日志类型包括：
- `Debug.Log()` - 普通日志
- `Debug.LogWarning()` - 警告日志
- `Debug.LogError()` - 错误日志

## 技术实现

### 核心机制

1. **统一日志方法**：在 `UnityMcpBridge.cs` 中定义了统一的日志输出方法
   ```csharp
   private static void Log(string message)
   {
       if (EnableLog) Debug.Log(message);
   }
   
   private static void LogWarning(string message)
   {
       if (EnableLog) Debug.LogWarning(message);
   }
   
   private static void LogError(string message)
   {
       if (EnableLog) Debug.LogError(message);
   }
   ```

2. **条件检查**：在其他文件中使用条件检查来控制日志输出
   ```csharp
   if (UnityMcp.EnableLog) Debug.Log("日志消息");
   ```

3. **设置持久化**：通过 EditorPrefs 保存设置
   ```csharp
   EditorPrefs.SetBool("mcp_enable_log", true);
   ```

### 默认设置

- 默认情况下日志输出是**禁用**的
- 启动时会从 EditorPrefs 读取上次的设置
- 如果没有保存的设置，则默认为禁用状态

## 注意事项

1. **性能影响**：禁用日志输出可以提高性能，特别是在高频操作时
2. **调试便利**：启用日志输出有助于调试和问题排查
3. **设置同步**：所有相关窗口的设置都是同步的，修改一处会影响所有地方
4. **重启保持**：设置会在 Unity 重启后保持

## 故障排除

### 日志仍然显示
- 检查是否还有其他地方直接使用 `Debug.Log` 而没有通过条件检查
- 确认 `UnityMcp.EnableLog` 的值是否正确设置

### 设置不保存
- 确认 EditorPrefs 的键名是否正确：`"mcp_enable_log"`
- 检查是否有权限问题

### 性能问题
- 如果遇到性能问题，建议禁用日志输出
- 在发布版本中应该默认禁用日志输出

## 扩展

如果需要添加新的日志输出，请遵循以下模式：

```csharp
// 在文件顶部添加引用
using UnityMcp;

// 在日志输出前添加条件检查
if (UnityMcp.EnableLog) Debug.Log("你的日志消息");
```

这样可以确保新的日志输出也受到统一控制。
