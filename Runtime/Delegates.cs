using System;
using System.Runtime.CompilerServices;
using Unity.Burst;
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
    public unsafe struct ConfigDataOriginHandle : IEquatable<ConfigDataOriginHandle> {
        public void* value;

        public bool Equals(ConfigDataOriginHandle other) {
            return other.value == value;
        }

        public static implicit operator void*(ConfigDataOriginHandle handle) => handle.value;
        public static implicit operator IntPtr(ConfigDataOriginHandle handle) => (IntPtr)handle.value;
    }
    public struct ConfigDataLength : IEquatable<ConfigDataLength> {
        public int value;

        public bool Equals(ConfigDataLength obj) {
            return value == obj.value;
        }
        public static implicit operator int(ConfigDataLength length) => length.value;
    }
    [HideInInspector]
    public unsafe delegate void FieldOperation(IntPtr config, IntPtr output);

    [BurstCompile]
    public unsafe static class ConfigHandleExtensions {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [BurstCompile]
        public static ConfigDataHandle CreateFromOffset(this ConfigDataHandle handle, int offset) {
            return new ConfigDataHandle { value = IntPtr.Add((IntPtr)handle.value, offset).ToPointer() };
        }
    }
}