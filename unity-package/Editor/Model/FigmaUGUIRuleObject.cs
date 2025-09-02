using System.Collections.Generic;
using UnityEngine;

namespace UnityMCP.Model
{
    public class FigmaUGUIRuleObject : ScriptableObject
    {
        public string link_url;
        public string picture_url;
        public int picture_level;
        public List<string> build_steps = new List<string>();
        public List<ComponentInfo> preferred_components = new List<ComponentInfo>();
        [Multiline(10)]
        public string extra_description;
        public List<string> modify_records = new List<string>();
    }

    [System.Serializable]
    public class ComponentInfo
    {
        public string component_name;
        public string component_menu_path;
    }
}