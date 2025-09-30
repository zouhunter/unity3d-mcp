using System.Collections.Generic;
using UnityEngine;

namespace UnityMCP.Model
{
    public class UIDefineRuleObject : ScriptableObject
    {
        public string link_url;
        public string optimize_rule_path;
        public string img_save_to;
        public string prototype_pic;
        public int image_scale = 3;
        [Multiline(5)]
        public string descriptions;
        public List<string> modify_records = new List<string>();
        public List<NodeRenameInfo> node_names = new List<NodeRenameInfo>();
        public List<NodeSpriteInfo> node_sprites = new List<NodeSpriteInfo>();
    }

    [System.Serializable]
    public class NodeRenameInfo
    {
        public string id;
        public string name;
        public string originName;
    }
    [System.Serializable]
    public class NodeSpriteInfo
    {
        public string id;
        public string fileName;
        public Sprite sprite;
    }
}