using System;
using System.Collections.Generic;
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
    public static class MainThreadExecutor
    {
        private static readonly Queue<System.Action> _actions = new Queue<System.Action>();
        private static readonly object _lock = new object();
        private static bool _initialized = false;

        /// <summary>
        /// 初始化主线程执行器
        /// </summary>
        static MainThreadExecutor()
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
                while (_actions.Count > 0)
                {
                    var action = _actions.Dequeue();
                    try
                    {
                        action?.Invoke();
                    }
                    catch (Exception e)
                    {
                        if (McpConnect.EnableLog) Debug.LogError($"[MainThreadExecutor] Error executing action: {e}");
                    }
                }
            }
        }

        /// <summary>
        /// 在主线程执行操作
        /// </summary>
        /// <param name="action">要执行的操作</param>
        public static void Execute(System.Action action)
        {
            if (IsMainThread())
            {
                // 如果已经在主线程，直接执行
                action?.Invoke();
            }
            else
            {
                // 否则加入队列等待主线程执行
                lock (_lock)
                {
                    _actions.Enqueue(action);
                }
            }
        }

        /// <summary>
        /// 检查当前是否在主线程
        /// </summary>
        /// <returns>如果在主线程返回true，否则返回false</returns>
        public static bool IsMainThread()
        {
            return Thread.CurrentThread.ManagedThreadId == 1;
        }

        /// <summary>
        /// 异步执行操作并等待结果
        /// </summary>
        /// <typeparam name="T">返回值类型</typeparam>
        /// <param name="func">要执行的函数</param>
        /// <returns>函数执行的结果</returns>
        public static async Task<T> ExecuteAsync<T>(Func<T> func)
        {
            if (IsMainThread())
            {
                // 如果已经在主线程，直接执行
                return func();
            }

            // 使用TaskCompletionSource来等待主线程执行完成
            var tcs = new TaskCompletionSource<T>();

            Execute(() =>
            {
                try
                {
                    var result = func();
                    tcs.SetResult(result);
                }
                catch (Exception e)
                {
                    tcs.SetException(e);
                }
            });

            return await tcs.Task;
        }
    }
}
