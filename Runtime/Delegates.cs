using System;
using UnityEngine;

namespace NeroWeNeed.ActionGraph {


    public unsafe struct ConfigHandle {
        public void* value;
        public static implicit operator void*(ConfigHandle handle) => handle.value;
        public static implicit operator IntPtr(ConfigHandle handle) => (IntPtr)handle.value;
    }

}