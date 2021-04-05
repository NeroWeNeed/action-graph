using System;
using System.Collections.Generic;
using System.Reflection;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace NeroWeNeed.ActionGraph.Editor {
    public static class FunctionListUtility {
        public static BlobAssetReference<FunctionList<TFunction>> Create<TFunction, TElement>(TElement[] elements, Func<TElement, MethodInfo> methodInfoProvider, Allocator allocator = Allocator.Persistent) where TFunction : Delegate {
            using var builder = new BlobBuilder(Allocator.Temp);
            ref FunctionList<TFunction> functionList = ref builder.ConstructRoot<FunctionList<TFunction>>();
            var functionListValues = builder.Allocate(ref functionList.value, elements.Length);
            int successfulCompilations = 0;
            for (int i = 0; i < elements.Length; i++) {
                var methodInfo = methodInfoProvider.Invoke(elements[i]);
                try {
                    functionListValues[i] = BurstCompiler.CompileFunctionPointer((TFunction)methodInfo.CreateDelegate(typeof(TFunction)));
                    successfulCompilations++;
                }
                catch (Exception exception) {
                    Debug.LogError($"Error Compiling Function Pointer {typeof(TFunction).AssemblyQualifiedName} from {methodInfo.DeclaringType}::{methodInfo.Name}: {exception.Message}");
                    functionListValues[i] = default;
                }
            }
            if (successfulCompilations != elements.Length) {
                Debug.LogError($"Error Compiling {elements.Length - successfulCompilations} Field Operations");
            }
            return builder.CreateBlobAssetReference<FunctionList<TFunction>>(allocator);
        }
    }
    public interface IFunctionListElement {
        public MethodInfo Call { get; }
    }
}