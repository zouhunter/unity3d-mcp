using System;
using System.Collections.Generic;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityMcp;

namespace UnityMcp.Tools
{
    /// <summary>
    /// 主线程执行器，用于确保代码在Unity主线程上执行
    /// </summary>
    public static class CoroutineRunner
    {
        private static readonly Queue<System.Action> _actions = new Queue<System.Action>();
        private static readonly List<CoroutineInfo> _coroutines = new List<CoroutineInfo>();
        private static readonly object _lock = new object();
        private static bool _initialized = false;

        /// <summary>
        /// 协程信息结构
        /// </summary>
        private class CoroutineInfo
        {
            public IEnumerator Coroutine { get; set; }
            public bool IsRunning { get; set; }
            public Action<object> CompleteCallback { get; set; }
            public object Result { get; set; }
            public bool HasResult { get; set; } // 标记是否有有效结果
            public Exception Error { get; set; } // 存储异常信息
        }

        /// <summary>
        /// 初始化主线程执行器
        /// </summary>
        static CoroutineRunner()
        {
            Initialize();
        }

        private static void Initialize()
        {
            if (_initialized) return;

            _initialized = true;

            // 使用EditorApplication.update确保在每一帧都能处理队列中的任务
            EditorApplication.update += ProcessQueue;
        }

        /// <summary>
        /// 处理队列中的任务
        /// </summary>
        private static void ProcessQueue()
        {
            lock (_lock)
            {
                // 处理普通任务队列
                while (_actions.Count > 0)
                {
                    var action = _actions.Dequeue();
                    try
                    {
                        action?.Invoke();
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"[MainThreadExecutor] Error executing action: {e}");
                    }
                }

                // 处理协程队列
                ProcessCoroutines();
            }
        }

        /// <summary>
        /// 处理协程队列
        /// </summary>
        private static void ProcessCoroutines()
        {
            var completedCoroutines = new List<CoroutineInfo>();

            foreach (var coroutineInfo in _coroutines.ToArray())
            {
                if (!coroutineInfo.IsRunning) continue;

                try
                {
                    // 执行协程的下一步
                    if (coroutineInfo.Coroutine.MoveNext())
                    {
                        // 协程还在运行，检查返回值
                        var current = coroutineInfo.Coroutine.Current;
                        if (current != null)
                        {
                            // 保存协程的返回值，这可能是最终结果
                            // 注意：协程的最后一个yield return的值才是最终结果
                            coroutineInfo.Result = current;
                            coroutineInfo.HasResult = true;

                            // 如果返回值是WaitForSeconds等，可以在这里处理
                            // 目前简单实现，直接继续执行
                        }
                    }
                    else
                    {
                        // 协程执行完毕
                        coroutineInfo.IsRunning = false;
                        completedCoroutines.Add(coroutineInfo);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"[CoroutineRunner] Error executing coroutine: {e}");
                    coroutineInfo.IsRunning = false;
                    coroutineInfo.Error = e; // 将异常存储到Error字段
                    coroutineInfo.HasResult = false;
                    completedCoroutines.Add(coroutineInfo);
                }
            }

            // 移除已完成的协程并调用完成回调
            foreach (var completed in completedCoroutines)
            {
                _coroutines.Remove(completed);
                try
                {
                    // 决定传递给回调的结果
                    object resultToPass;
                    if (completed.Error != null)
                    {
                        // 如果有异常，传递异常
                        resultToPass = completed.Error;
                    }
                    else if (completed.HasResult)
                    {
                        // 如果有结果，传递结果
                        resultToPass = completed.Result;
                    }
                    else
                    {
                        // 既没有异常也没有结果，传递null
                        resultToPass = null;
                    }

                    completed.CompleteCallback?.Invoke(resultToPass);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[CoroutineRunner] Error in coroutine complete callback: {e}");
                }
            }
        }

        /// <summary>
        /// 启动协程
        /// </summary>
        /// <param name="coroutine">协程枚举器</param>
        /// <param name="completeCallback">完成回调</param>
        public static void StartCoroutine(IEnumerator coroutine, Action<object> completeCallback = null)
        {
            if (coroutine == null) return;

            lock (_lock)
            {
                var coroutineInfo = new CoroutineInfo
                {
                    Coroutine = coroutine,
                    IsRunning = true,
                    CompleteCallback = completeCallback,
                    Result = null,
                    HasResult = false,
                    Error = null
                };

                _coroutines.Add(coroutineInfo);
            }
        }

        /// <summary>
        /// 停止所有协程
        /// </summary>
        public static void StopAllCoroutines()
        {
            lock (_lock)
            {
                _coroutines.Clear();
            }
        }
    }
}
