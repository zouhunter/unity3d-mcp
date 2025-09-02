# Unity MCP 协程系统使用指南

## 概述

本协程系统基于 `EditorApplication.update` 实现，提供了完整的协程功能支持，包括基本协程、命名协程、延迟协程、重复协程和条件协程等。

## 核心组件

### 1. MainThreadExecutor
- **位置**: `Packages/unity-mcp/Runtime/MainThreadExecutor.cs`
- **功能**: 主线程执行器，负责协程的调度和执行
- **特点**: 基于 `EditorApplication.update`，确保协程在Unity主线程上执行

### 2. CoroutineManager
- **位置**: `Packages/unity-mcp/Runtime/CoroutineManager.cs`
- **功能**: 协程管理器，提供高级协程功能
- **特点**: 单例模式，支持命名协程、延迟协程、重复协程等

### 3. StateTreeContext
- **位置**: `Packages/unity-mcp/Runtime/StateTreeContext.cs`
- **功能**: 状态树上下文，集成协程功能
- **特点**: 提供统一的协程接口，支持上下文管理

## 基本用法

### 1. 启动基本协程

```csharp
using UnityMcp.Tools;

// 创建上下文
var context = new StateTreeContext();

// 启动基本协程
context.StartCoroutine(SimpleCoroutine());

private IEnumerator SimpleCoroutine()
{
    Debug.Log("协程开始");
    yield return new WaitForSeconds(1f);
    Debug.Log("协程完成");
}
```

### 2. 启动带回调的协程

```csharp
context.StartCoroutine(CountdownCoroutine(), (result) =>
{
    Debug.Log($"协程完成，结果: {result}");
});

private IEnumerator CountdownCoroutine()
{
    for (int i = 3; i > 0; i--)
    {
        Debug.Log($"倒计时: {i}");
        yield return new WaitForSeconds(1f);
    }
    yield return "倒计时完成";
}
```

### 3. 启动命名协程

```csharp
// 启动命名协程
context.StartNamedCoroutine("MyCoroutine", MyCoroutine(), (result) =>
{
    Debug.Log($"命名协程完成: {result}");
});

// 检查协程状态
bool isRunning = context.IsNamedCoroutineRunning("MyCoroutine");

// 停止特定协程
context.StopNamedCoroutine("MyCoroutine");
```

### 4. 启动延迟协程

```csharp
// 延迟2秒后执行
context.StartDelayedCoroutine(2f, () =>
{
    Debug.Log("延迟2秒后执行");
});
```

### 5. 启动重复协程

```csharp
// 每秒重复执行，总共执行5次
context.StartRepeatingCoroutine(1f, () =>
{
    Debug.Log("重复执行");
}, 5);

// 无限重复执行
context.StartRepeatingCoroutine(1f, () =>
{
    Debug.Log("无限重复");
}, -1);
```

### 6. 启动条件协程

```csharp
// 等待条件满足后执行
context.StartConditionalCoroutine(
    () => someCondition, // 条件函数
    () => Debug.Log("条件满足！"), // 条件满足时的动作
    10f // 10秒超时
);
```

### 7. 异步协程

```csharp
// 异步等待协程完成
async void TestAsyncCoroutine()
{
    var result = await context.StartCoroutineAsync(AsyncCoroutine());
    Debug.Log($"异步协程完成: {result}");
}

private IEnumerator AsyncCoroutine()
{
    yield return new WaitForSeconds(2f);
    yield return "异步操作成功";
}
```

## 高级功能

### 1. 协程管理

```csharp
// 获取所有运行的命名协程
var runningCoroutines = context.GetRunningNamedCoroutines();
foreach (var coroutineName in runningCoroutines)
{
    Debug.Log($"运行中的协程: {coroutineName}");
}

// 停止所有命名协程
context.StopAllNamedCoroutines();
```

### 2. 协程状态监控

```csharp
// 检查特定协程是否运行
bool isRunning = context.IsNamedCoroutineRunning("MyCoroutine");

// 获取运行中的协程数量
int count = context.GetRunningNamedCoroutines().Count;
```

### 3. 错误处理

协程系统内置错误处理机制：

```csharp
// 协程执行过程中的异常会被捕获并记录到Unity控制台
context.StartCoroutine(RiskyCoroutine());

private IEnumerator RiskyCoroutine()
{
    try
    {
        // 可能出错的代码
        yield return new WaitForSeconds(1f);
    }
    catch (Exception e)
    {
        Debug.LogError($"协程执行出错: {e.Message}");
    }
}
```

## 测试脚本

### 1. CoroutineTest.cs
- **位置**: `Assets/Scripts/CoroutineTest.cs`
- **功能**: 基本协程功能测试
- **用法**: 挂载到GameObject上，自动运行测试

### 2. AdvancedCoroutineTest.cs
- **位置**: `Assets/Scripts/AdvancedCoroutineTest.cs`
- **功能**: 高级协程功能测试
- **用法**: 挂载到GameObject上，演示所有协程功能

## 注意事项

### 1. 线程安全
- 协程始终在Unity主线程上执行
- 使用 `MainThreadExecutor` 确保线程安全

### 2. 内存管理
- 协程完成后会自动清理资源
- 使用 `StopAllNamedCoroutines()` 清理所有协程

### 3. 性能考虑
- 协程基于 `EditorApplication.update`，每帧都会检查
- 大量协程可能影响编辑器性能

### 4. 生命周期
- 协程在编辑器模式下运行
- 场景切换时协程会自动停止

## 扩展功能

### 1. 自定义协程类型

```csharp
// 创建自定义协程类型
public class CustomCoroutine
{
    public static IEnumerator WaitForCondition(Func<bool> condition, float timeout = -1f)
    {
        float elapsedTime = 0f;
        while (!condition())
        {
            if (timeout > 0 && elapsedTime >= timeout)
                yield break;
            
            elapsedTime += 0.1f;
            yield return new WaitForSeconds(0.1f);
        }
    }
}
```

### 2. 协程组合

```csharp
// 组合多个协程
context.StartCoroutine(CombinedCoroutine());

private IEnumerator CombinedCoroutine()
{
    // 执行第一个协程
    yield return StartCoroutine(Coroutine1());
    
    // 等待条件满足
    yield return CustomCoroutine.WaitForCondition(() => someCondition);
    
    // 执行第二个协程
    yield return StartCoroutine(Coroutine2());
}
```

## 故障排除

### 1. 协程不执行
- 检查是否在Unity编辑器中运行
- 确认 `EditorApplication.update` 正常工作
- 检查控制台是否有错误信息

### 2. 协程无法停止
- 使用 `StopNamedCoroutine()` 停止特定协程
- 使用 `StopAllNamedCoroutines()` 停止所有协程
- 检查协程名称是否正确

### 3. 性能问题
- 减少同时运行的协程数量
- 使用 `WaitForSeconds` 控制执行频率
- 及时停止不需要的协程

## 总结

本协程系统提供了完整的协程功能支持，适用于Unity编辑器环境下的各种异步操作需求。通过合理的协程管理，可以实现复杂的时序逻辑和异步操作流程。 