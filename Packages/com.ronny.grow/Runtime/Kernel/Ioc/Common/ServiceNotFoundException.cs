// SPDX-License-Identifier: MIT

using System;

namespace Grow.Kernel.Ioc.Common {
    /// <summary>Thrown by <c>GetRequiredService</c> when the exact identity is not registered.</summary>
    public sealed class ServiceNotFoundException : InvalidOperationException {
        public ServiceNotFoundException(Type serviceType, string key)
            : base(BuildMessage(serviceType, key)) {
            ServiceType = serviceType;
            Key = key ?? string.Empty;
        }

        /// <summary>The requested service type.</summary>
        public Type ServiceType { get; }

        /// <summary>The requested key; empty means the keyless registration.</summary>
        public string Key { get; }

        private static string BuildMessage(Type serviceType, string key) {
            var name = serviceType?.FullName ?? serviceType?.Name ?? "<null>";
            return string.IsNullOrEmpty(key)
                ? $"Service not registered: {name}"
                : $"Service not registered: {name} (key=\"{key}\")";
        }
    }
}
