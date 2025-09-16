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
            public CoroutineInfo SubCoroutine { get; set; } // 子协程
            public bool WaitingForSubCoroutine { get; set; } // 是否在等待子协程

            // WaitForSeconds支持
            public bool IsWaitingForTime { get; set; } // 是否在等待时间
            public double WaitEndTime { get; set; } // 等待结束时间（使用EditorApplication.timeSinceStartup）
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
                    // 检查等待状态

                    // 1. 如果正在等待时间，检查时间是否到了
                    if (coroutineInfo.IsWaitingForTime)
                    {
                        if (EditorApplication.timeSinceStartup < coroutineInfo.WaitEndTime)
                        {
                            // 还在等待时间，继续等待
                            continue;
                        }
                        else
                        {
                            // 等待时间结束，继续执行协程
                            coroutineInfo.IsWaitingForTime = false;
                            coroutineInfo.WaitEndTime = 0;
                        }
                    }

                    // 2. 如果正在等待子协程，先检查子协程状态
                    if (coroutineInfo.WaitingForSubCoroutine && coroutineInfo.SubCoroutine != null)
                    {
                        if (coroutineInfo.SubCoroutine.IsRunning)
                        {
                            // 子协程还在运行，继续等待
                            continue;
                        }
                        else
                        {
                            // 子协程完成，获取子协程结果
                            if (coroutineInfo.SubCoroutine.Error != null)
                            {
                                // 子协程有异常，传播异常
                                throw coroutineInfo.SubCoroutine.Error;
                            }

                            // 子协程正常完成，继续执行主协程
                            coroutineInfo.WaitingForSubCoroutine = false;
                            coroutineInfo.SubCoroutine = null;
                        }
                    }

                    // 3. 如果不在等待子协程且不在等待时间，执行协程的下一步
                    if (!coroutineInfo.WaitingForSubCoroutine && !coroutineInfo.IsWaitingForTime)
                    {
                        if (coroutineInfo.Coroutine.MoveNext())
                        {
                            // 协程还在运行，检查返回值
                            var current = coroutineInfo.Coroutine.Current;

                            // 检查是否返回了子协程（IEnumerator）
                            if (current is IEnumerator subCoroutine)
                            {
                                // 启动子协程
                                var subCoroutineInfo = new CoroutineInfo
                                {
                                    Coroutine = subCoroutine,
                                    IsRunning = true,
                                    CompleteCallback = null,
                                    Result = null,
                                    HasResult = false,
                                    Error = null,
                                    SubCoroutine = null,
                                    WaitingForSubCoroutine = false,
                                    IsWaitingForTime = false,
                                    WaitEndTime = 0
                                };

                                // 将子协程添加到协程列表
                                _coroutines.Add(subCoroutineInfo);

                                // 设置主协程等待子协程
                                coroutineInfo.SubCoroutine = subCoroutineInfo;
                                coroutineInfo.WaitingForSubCoroutine = true;
                            }
                            // 检查是否返回了WaitForSeconds
                            else if (current is WaitForSeconds waitForSeconds)
                            {
                                // 使用反射获取WaitForSeconds的等待时间
                                var waitTime = GetWaitTimeFromWaitForSeconds(waitForSeconds);
                                if (waitTime > 0)
                                {
                                    // 设置等待状态
                                    coroutineInfo.IsWaitingForTime = true;
                                    coroutineInfo.WaitEndTime = EditorApplication.timeSinceStartup + waitTime;
                                    //Debug.Log($"[CoroutineRunner] 开始等待 {waitTime} 秒，结束时间: {coroutineInfo.WaitEndTime}");
                                }
                                else
                                {
                                    // 无法获取等待时间，使用默认值
                                    Debug.LogWarning($"[CoroutineRunner] 无法获取WaitForSeconds的等待时间，使用默认0.1秒");
                                    coroutineInfo.IsWaitingForTime = true;
                                    coroutineInfo.WaitEndTime = EditorApplication.timeSinceStartup + 0.1;
                                }

                                // 保存结果
                                coroutineInfo.Result = current;
                                coroutineInfo.HasResult = true;
                            }
                            else if (current != null)
                            {
                                // 保存协程的返回值（其他类型）
                                coroutineInfo.Result = current;
                                coroutineInfo.HasResult = true;

                                // 对于其他类型（如null），直接继续下一帧
                            }
                        }
                        else
                        {
                            // 协程执行完毕
                            coroutineInfo.IsRunning = false;
                            completedCoroutines.Add(coroutineInfo);
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"[CoroutineRunner] Error executing coroutine: {e}");
                    coroutineInfo.IsRunning = false;
                    coroutineInfo.Error = e;
                    coroutineInfo.HasResult = false;
                    coroutineInfo.WaitingForSubCoroutine = false;
                    coroutineInfo.SubCoroutine = null;
                    coroutineInfo.IsWaitingForTime = false;
                    coroutineInfo.WaitEndTime = 0;
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
                    Debug.Log($"[CoroutineRunner] Complete callback: {resultToPass}");
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
                    Error = null,
                    SubCoroutine = null,
                    WaitingForSubCoroutine = false,
                    IsWaitingForTime = false,
                    WaitEndTime = 0
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

        /// <summary>
        /// 使用反射从WaitForSeconds对象中获取等待时间
        /// </summary>
        /// <param name="waitForSeconds">WaitForSeconds实例</param>
        /// <returns>等待时间（秒），如果获取失败返回-1</returns>
        private static float GetWaitTimeFromWaitForSeconds(WaitForSeconds waitForSeconds)
        {
            try
            {
                // 使用反射获取WaitForSeconds的私有字段 m_Seconds
                var field = typeof(WaitForSeconds).GetField("m_Seconds",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                if (field != null)
                {
                    var value = field.GetValue(waitForSeconds);
                    if (value is float seconds)
                    {
                        return seconds;
                    }
                }

                // 如果上面的字段名不对，尝试其他可能的字段名
                var fields = typeof(WaitForSeconds).GetFields(
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                foreach (var f in fields)
                {
                    if (f.FieldType == typeof(float))
                    {
                        var value = f.GetValue(waitForSeconds);
                        if (value is float seconds && seconds > 0)
                        {
                            Debug.Log($"[CoroutineRunner] 找到等待时间字段: {f.Name} = {seconds}");
                            return seconds;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[CoroutineRunner] 获取WaitForSeconds等待时间失败: {e.Message}");
            }

            return -1; // 获取失败
        }
    }
}
