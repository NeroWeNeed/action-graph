using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

namespace NeroWeNeed.ActionGraph {
    [Serializable]
    [JsonConverter(typeof(ActionIdConverter))]
    public struct ActionId : IEquatable<ActionId> {
        public const string NullActionName = "None";
        public const string UnknownActionName = "Unknown Action";
        public string guid;
        [JsonIgnore]
        public bool IsCreated { get => !string.IsNullOrEmpty(guid); }
        public static ActionId Create() => new ActionId { guid = Guid.NewGuid().ToString("N") };

        public override bool Equals(object obj) {
            return obj is ActionId id &&
                   guid == id.guid;
        }

        public bool Equals(ActionId other) {
            return this.guid == other.guid || (string.IsNullOrWhiteSpace(this.guid) && string.IsNullOrWhiteSpace(other.guid));
        }

        public override int GetHashCode() {
            return -1324198676 + EqualityComparer<string>.Default.GetHashCode(guid);
        }

        public override string ToString() {
            return guid;
        }

        public static bool operator ==(ActionId self, ActionId other) => self.Equals(other);
        public static bool operator !=(ActionId self, ActionId other) => !self.Equals(other);
    }

    public class ActionIdConverter : JsonConverter<ActionId> {
        public override ActionId ReadJson(JsonReader reader, Type objectType, ActionId existingValue, bool hasExistingValue, JsonSerializer serializer) {
            var value = (string)reader.Value;
            if (hasExistingValue) {
                existingValue.guid = value;
                return existingValue;
            }
            else {

                return new ActionId { guid = value };
            }
        }

        public override void WriteJson(JsonWriter writer, ActionId value, JsonSerializer serializer) {
            writer.WriteValue(value.guid);
        }
    }
}