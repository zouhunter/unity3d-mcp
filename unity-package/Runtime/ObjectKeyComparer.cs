using System;
using System.Collections.Generic;
using System.Linq;

namespace UnityMcp.Tools
{
    // ---------------- 键比较器：字符串走 StringComparer，其它类型使用自身相等语义 ----------------
    public sealed class ObjectKeyComparer : IEqualityComparer<object>
    {
        private readonly StringComparer _sc;
        public ObjectKeyComparer(StringComparer sc) => _sc = sc;
        public new bool Equals(object? x, object? y)
        {
            if (ReferenceEquals(x, y)) return true;
            if (x is null || y is null) return false;
            if (x is string sx && y is string sy) return _sc.Equals(sx, sy);
            return x.Equals(y);
        }
        public int GetHashCode(object obj)
        {
            if (obj is string s) return _sc.GetHashCode(s);
            return obj.GetHashCode();
        }
    }
}
