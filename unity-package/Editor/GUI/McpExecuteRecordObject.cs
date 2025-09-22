using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
namespace UnityMcp.Tools
{
    [FilePath("Library/McpExecuteRecordObject.asset", FilePathAttribute.Location.ProjectFolder)]
    public class McpExecuteRecordObject : ScriptableSingleton<McpExecuteRecordObject>
    {
        public List<McpExecuteRecord> records = new List<McpExecuteRecord>();
        [System.Serializable]
        public class McpExecuteRecord
        {
            public string name;
            public string cmd;
            public string result;
            public string error;
            public string timestamp;
            public bool success;
            public double duration; // 执行时间（毫秒）
            public string source; // 记录来源："MCP Client" 或 "Debug Window"
        }
        public void addRecord(string name, string cmd, string result, string error)
        {
            records.Add(new McpExecuteRecord()
            {
                name = name,
                cmd = cmd,
                result = result,
                error = error,
                timestamp = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                success = string.IsNullOrEmpty(error),
                duration = 0,
                source = "Legacy"
            });
        }

        public void addRecord(string name, string cmd, string result, string error, double duration, string source)
        {
            records.Add(new McpExecuteRecord()
            {
                name = name,
                cmd = cmd,
                result = result,
                error = error,
                timestamp = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                success = string.IsNullOrEmpty(error),
                duration = duration,
                source = source
            });
        }
        public void clearRecords()
        {
            records.Clear();
        }
        public void saveRecords()
        {
            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();
        }
        public void loadRecords()
        {
            records = new List<McpExecuteRecord>();
        }
    }
}