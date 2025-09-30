using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UnityMcp.Tools
{
    public class MethodKey
    {
        public string Key;
        public string Desc;
        public bool Optional;
        public MethodKey(string key, string desc, bool optional)
        {
            Key = key;
            Desc = desc;
            Optional = optional;
        }
    }
}
