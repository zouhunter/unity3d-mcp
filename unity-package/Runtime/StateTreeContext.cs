using System.Collections.Generic;
using System;
using Newtonsoft.Json.Linq;

namespace UnityMcp.Tools
{
    /// <summary>
    /// StateTree执行上下文包装类，支持JSON序列化字段和非序列化对象引用
    /// </summary>
    public class StateTreeContext
    {
        /// <summary>
        /// JSON可序列化的参数数据
        /// </summary>
        public JObject JsonData { get; }

        /// <summary>
        /// 非序列化的对象引用字典，用于存储Unity对象等不可序列化的数据
        /// </summary>
        public Dictionary<string, object> ObjectReferences { get; }

        public Action<object> CompleteAction { get; private set; }
        /// <summary>
        /// 构造函数，基于现有JObject创建上下文
        /// </summary>
        /// <param name="jsonData">JSON数据</param>
        /// <param name="objectReferences">可选的对象引用字典</param>
        public StateTreeContext(JObject jsonData = null, Dictionary<string, object> objectReferences = null)
        {
            JsonData = jsonData ?? new JObject();
            ObjectReferences = objectReferences ?? new Dictionary<string, object>();
        }
        /// <summary>
        /// 注册完成回调
        /// </summary>
        /// <param name="completeAction"></param>
        public void RegistComplete(Action<object> completeAction)
        {
            CompleteAction = completeAction;
        }
        /// <summary>
        /// 结束回调
        /// </summary>
        /// <param name="result"></param>
        public void Complete(object result)
        {
            CompleteAction?.Invoke(result);
        }
        /// <summary>
        /// 获取JSON字段值
        /// </summary>
        /// <param name="key">字段键</param>
        /// <param name="token">输出的Token值</param>
        /// <returns>是否找到该字段</returns>
        public bool TryGetJsonValue(string key, out JToken token)
        {
            return JsonData.TryGetValue(key, out token);
        }

        /// <summary>
        /// 获取对象引用
        /// </summary>
        /// <param name="key">对象键</param>
        /// <param name="obj">输出的对象引用</param>
        /// <returns>是否找到该对象引用</returns>
        public bool TryGetObjectReference(string key, out object obj)
        {
            return ObjectReferences.TryGetValue(key, out obj);
        }

        /// <summary>
        /// 获取对象引用的泛型版本
        /// </summary>
        /// <typeparam name="T">期望的对象类型</typeparam>
        /// <param name="key">对象键</param>
        /// <param name="obj">输出的对象引用</param>
        /// <returns>是否找到该对象引用且类型匹配</returns>
        public bool TryGetObjectReference<T>(string key, out T obj) where T : class
        {
            if (ObjectReferences.TryGetValue(key, out object objRef) && objRef is T typedObj)
            {
                obj = typedObj;
                return true;
            }
            obj = null;
            return false;
        }

        /// <summary>
        /// 设置JSON字段值
        /// </summary>
        /// <param name="key">字段键</param>
        /// <param name="value">字段值</param>
        public void SetJsonValue(string key, JToken value)
        {
            JsonData[key] = value;
        }

        /// <summary>
        /// 设置对象引用
        /// </summary>
        /// <param name="key">对象键</param>
        /// <param name="obj">对象引用</param>
        public void SetObjectReference(string key, object obj)
        {
            ObjectReferences[key] = obj;
        }

        /// <summary>
        /// 复制上下文，可以用于创建新的上下文实例
        /// </summary>
        /// <returns>新的上下文实例</returns>
        public StateTreeContext Clone()
        {
            var newJsonData = new JObject(JsonData);
            var newObjectReferences = new Dictionary<string, object>(ObjectReferences);
            return new StateTreeContext(newJsonData, newObjectReferences);
        }

        /// <summary>
        /// 索引器，支持通过[]语法访问JsonData或ObjectReferences中的值
        /// 查找优先级：JsonData -> ObjectReferences
        /// 设置规则：基本类型和可序列化对象 -> JsonData，Unity对象等 -> ObjectReferences
        /// </summary>
        /// <param name="key">要访问的键</param>
        /// <returns>找到的值，如果没找到则返回null</returns>
        public object this[string key]
        {
            get
            {
                // 优先查找JsonData
                if (JsonData.TryGetValue(key, out JToken token))
                {
                    // 如果是基本类型，直接返回值
                    switch (token.Type)
                    {
                        case JTokenType.String:
                            return token.Value<string>();
                        case JTokenType.Integer:
                            return token.Value<long>();
                        case JTokenType.Float:
                            return token.Value<double>();
                        case JTokenType.Boolean:
                            return token.Value<bool>();
                        case JTokenType.Null:
                            return null;
                        default:
                            return token; // 返回JToken本身
                    }
                }

                // 如果JsonData中没有，查找ObjectReferences
                if (ObjectReferences.TryGetValue(key, out object obj))
                {
                    return obj;
                }

                return null; // 都没找到
            }
            set
            {
                if (value == null)
                {
                    // null值优先存储到JsonData中
                    JsonData[key] = JValue.CreateNull();
                    // 同时从ObjectReferences中移除
                    ObjectReferences.Remove(key);
                }
                else if (IsSerializableType(value))
                {
                    // 可序列化类型存储到JsonData
                    JsonData[key] = JToken.FromObject(value);
                    // 从ObjectReferences中移除
                    ObjectReferences.Remove(key);
                }
                else
                {
                    // Unity对象和其他复杂对象存储到ObjectReferences
                    ObjectReferences[key] = value;
                    // 从JsonData中移除
                    JsonData.Remove(key);
                }
            }
        }

        /// <summary>
        /// 检查指定键是否存在于JsonData或ObjectReferences中
        /// </summary>
        /// <param name="key">要检查的键</param>
        /// <returns>是否存在该键</returns>
        public bool ContainsKey(string key)
        {
            return JsonData.ContainsKey(key) || ObjectReferences.ContainsKey(key);
        }

        /// <summary>
        /// 尝试获取值（统一的获取方法）
        /// </summary>
        /// <param name="key">键</param>
        /// <param name="value">输出的值</param>
        /// <returns>是否找到该键</returns>
        public bool TryGetValue(string key, out object value)
        {
            // 优先从JsonData获取
            if (JsonData.TryGetValue(key, out JToken token))
            {
                switch (token.Type)
                {
                    case JTokenType.String:
                        value = token.Value<string>();
                        return true;
                    case JTokenType.Integer:
                        value = token.Value<long>();
                        return true;
                    case JTokenType.Float:
                        value = token.Value<double>();
                        return true;
                    case JTokenType.Boolean:
                        value = token.Value<bool>();
                        return true;
                    case JTokenType.Null:
                        value = null;
                        return true;
                    default:
                        value = token;
                        return true;
                }
            }

            // 从ObjectReferences获取
            return ObjectReferences.TryGetValue(key, out value);
        }

        /// <summary>
        /// 尝试获取指定类型的值
        /// </summary>
        /// <typeparam name="T">期望的值类型</typeparam>
        /// <param name="key">键</param>
        /// <param name="value">输出的值</param>
        /// <returns>是否找到该键且类型匹配</returns>
        public bool TryGetValue<T>(string key, out T value)
        {
            if (TryGetValue(key, out object obj))
            {
                if (obj is T typedValue)
                {
                    value = typedValue;
                    return true;
                }

                // 尝试类型转换
                try
                {
                    value = (T)Convert.ChangeType(obj, typeof(T));
                    return true;
                }
                catch
                {
                    // 转换失败
                }
            }

            value = default(T);
            return false;
        }

        /// <summary>
        /// 移除指定键的值（从JsonData和ObjectReferences中都移除）
        /// </summary>
        /// <param name="key">要移除的键</param>
        /// <returns>是否移除了任何值</returns>
        public bool Remove(string key)
        {
            bool removedFromJson = JsonData.Remove(key);
            bool removedFromObjects = ObjectReferences.Remove(key);
            return removedFromJson || removedFromObjects;
        }

        /// <summary>
        /// 判断对象是否为可序列化的基本类型
        /// </summary>
        /// <param name="value">要检查的对象</param>
        /// <returns>是否为可序列化类型</returns>
        private static bool IsSerializableType(object value)
        {
            if (value == null) return true;

            var type = value.GetType();

            // Unity对象类型不可序列化
            if (typeof(UnityEngine.Object).IsAssignableFrom(type))
                return false;

            // 基本类型可序列化
            if (type.IsPrimitive || type == typeof(string) || type == typeof(decimal))
                return true;

            // 枚举可序列化
            if (type.IsEnum)
                return true;

            // DateTime可序列化
            if (type == typeof(DateTime) || type == typeof(DateTimeOffset))
                return true;

            // Guid可序列化
            if (type == typeof(Guid))
                return true;

            // 数组和集合（如果元素是可序列化的）
            if (type.IsArray)
            {
                var elementType = type.GetElementType();
                return elementType != null && IsSerializableType(Activator.CreateInstance(elementType));
            }

            // 尝试序列化测试（谨慎使用，可能有性能影响）
            try
            {
                JToken.FromObject(value);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}