// SPDX-License-Identifier: MIT

namespace Grow.Kernel.Ioc.Interface {
    /// <summary>A factory used by transient and singleton registrations.</summary>
    /// <typeparam name="T">Service contract type.</typeparam>
    public interface IServiceFactory<T> where T : class {
        /// <summary>Creates a service for <paramref name="key"/>.</summary>
        T GetService(string key);
    }
}
