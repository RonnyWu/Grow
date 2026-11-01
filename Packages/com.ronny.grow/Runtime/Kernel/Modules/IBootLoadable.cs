// SPDX-License-Identifier: MIT

using System.Collections;

namespace Grow.Kernel.Modules {
    /// <summary>
    /// Optional asynchronous preparation phase for a module, driven between
    /// <see cref="IModule.OnInitialize"/> and <see cref="IModule.OnStart"/>.
    /// </summary>
    /// <remarks>
    /// Implement this interface when a module must prepare something that takes more than one frame
    /// (a scene, configuration tables, an asset catalog) before the kernel reaches
    /// <see cref="Entry.GrowState.Ready"/>. <see cref="IModule"/> is unchanged, so a module with
    /// nothing to load simply does not implement this interface and pays no cost.
    /// <para>
    /// <see cref="Load"/> runs once per bootstrap, serially, in the same dependency order as the
    /// other hooks. Later modules can resolve services registered by earlier ones. The returned
    /// enumerator drives the boot barrier:
    /// </para>
    /// <list type="bullet">
    /// <item><description><c>null</c> — nothing to wait for; the module is complete.</description></item>
    /// <item><description><c>yield return null</c> — wait one frame.</description></item>
    /// <item><description>
    /// <c>yield return someAsyncOperation</c> — wait until <c>isDone</c> is true.
    /// </description></item>
    /// <item><description>
    /// Any other value — treated as <c>null</c> and reported once per module in the editor. Nested
    /// enumerators and coroutines are not supported.
    /// </description></item>
    /// </list>
    /// <para>
    /// A load that throws fails the boot: the module failure is recorded, the composition rolls back
    /// and <see cref="Entry.GrowBoot.State"/> reports <see cref="Entry.GrowState.Failed"/> once. A
    /// module that wants to degrade instead of failing must catch its own exception and report the
    /// degraded state through its own surface; the kernel does not decide which modules are
    /// optional.
    /// </para>
    /// <para>
    /// <see cref="Load"/> runs on the main thread inside the module registry's lock. Do not call
    /// <see cref="GrowModules.Register"/>, <see cref="GrowModules.Reset"/> or
    /// <see cref="GrowModules.Shutdown"/> from it, and do not expect another thread to observe
    /// services registered mid-load.
    /// </para>
    /// </remarks>
    public interface IBootLoadable {
        /// <summary>
        /// Prepares the module; called once per bootstrap, after <see cref="IModule.OnInitialize"/>
        /// and before any <see cref="IModule.OnStart"/>.
        /// </summary>
        /// <param name="context">The same module context passed to the other hooks.</param>
        /// <returns>
        /// An enumerator that ends when the module is ready, or <c>null</c> for a synchronous
        /// module.
        /// </returns>
        IEnumerator Load(IModuleContext context);

        /// <summary>
        /// Suggested progress in the 0..1 range while <see cref="Load"/> is running.
        /// </summary>
        /// <remarks>
        /// The kernel clamps the value and keeps the overall progress monotonic; it never requires
        /// an exact measurement. A module with nothing to report returns 0.
        /// </remarks>
        float LoadProgress { get; }
    }
}
