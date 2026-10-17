// SPDX-License-Identifier: MIT

using System;

namespace Grow.Kernel.Ioc.Common {
    /// <summary>Registration identity: service type plus an ordinal key.</summary>
    internal readonly struct ServiceKey : IEquatable<ServiceKey> {
        public ServiceKey(Type type, string key) {
            Type = type ?? throw new ArgumentNullException(nameof(type));
            Key = key ?? string.Empty;
        }

        public Type Type { get; }
        public string Key { get; }
        public bool IsKeyless => Key.Length == 0;

        public bool Equals(ServiceKey other) {
            return Type == other.Type && string.Equals(Key, other.Key, StringComparison.Ordinal);
        }

        public override bool Equals(object obj) {
            return obj is ServiceKey other && Equals(other);
        }

        public override int GetHashCode() {
            unchecked {
                return (Type.GetHashCode() * 397) ^ Key.GetHashCode();
            }
        }

        public override string ToString() {
            var name = Type.FullName ?? Type.Name;
            return IsKeyless ? name : $"{name}[\"{Key}\"]";
        }
    }
}
