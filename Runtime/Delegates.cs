using System;
using UnityEngine;

namespace NeroWeNeed.ActionGraph {


    public unsafe struct ConfigDataHandle : IEquatable<ConfigDataHandle> {
        public void* value;

        public bool Equals(ConfigDataHandle other) {
            return other.value == value;
        }


        public static implicit operator void*(ConfigDataHandle handle) => handle.value;
        public static implicit operator IntPtr(ConfigDataHandle handle) => (IntPtr)handle.value;
    }
    public struct ConfigDataLength : IEquatable<ConfigDataLength> {
        public int value;

        public bool Equals(ConfigDataLength obj) {
            return value == obj.value;
        }
        public static implicit operator int(ConfigDataLength length) => length.value;
    }


}