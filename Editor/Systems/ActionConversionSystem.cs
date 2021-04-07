using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.SqlServer.Server;
using NeroWeNeed.ActionGraph.Editor.Graph;
using NeroWeNeed.ActionGraph.Editor.Schema;
using NeroWeNeed.Commons;
using NeroWeNeed.Commons.Editor;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using UnityEditor;
using UnityEngine;



namespace NeroWeNeed.ActionGraph.Editor.Systems {
/*     [UpdateInGroup(typeof(GameObjectConversionGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.GameObjectConversion)] */
    public class ActionConversionSystem : GameObjectConversionSystem {
        protected override void OnUpdate() {
/*             var operations = ProjectUtility.GetOrCreateProjectAsset<FieldOperationSchema>();
            var actions = ProjectUtility.GetOrCreateProjectAsset<ActionSchema>();
            var actionAssets                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                             = AssetDatabase.FindAssets($"t:{nameof(ActionAsset)}").OrderBy(guid => guid).Select(guid => AssetDatabase.LoadAssetAtPath<ActionAsset>(AssetDatabase.GUIDToAssetPath(guid))).ToArray();
            Entities.ForEach((ActionIndex indexObj) =>
            {
                if (indexObj.definitionAssets != null) {
                    foreach (var definition in indexObj.definitionAssets.NotNull()) {
                        if (definition.delegateType.IsCreated) {
                            typeof(ActionConversionSystem)
                            .GetMethod(nameof(Convert), BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance)
                            .MakeGenericMethod(definition.delegateType).Invoke(this, new object[] { actions, operations, actionAssets.Where(a => a.actionId == definition.id).ToArray(), definition, indexObj });
                        }
                    }
                }
            }); */
        }
/*         protected void Convert<TAction>(ActionSchema actionSchema, FieldOperationSchema operationSchema, ActionAsset[] actionAssets, ActionDefinitionAsset actionDefinitionAsset, ActionIndex indexObj) where TAction : Delegate {
            var buffer = DstEntityManager.AddBuffer<Action<TAction>>(GetPrimaryEntity(indexObj));
            foreach (var actionAsset in actionAssets) {
                DeclareAssetDependency(indexObj.gameObject, actionAsset);
                var blob = Create<TAction>(actionAsset.CreateModel(), actionSchema, operationSchema, actionDefinitionAsset);
                BlobAssetStore.AddUniqueBlobAsset(ref blob);
                buffer.Add(blob);
            }
        } */
    }

}