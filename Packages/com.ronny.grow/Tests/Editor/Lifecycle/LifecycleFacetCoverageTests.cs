// SPDX-License-Identifier: MIT

using System;
using System.Linq;
using Grow.Core.Events;
using Grow.Kernel.Lifecycle;
using Grow.Kernel.Lifecycle.Impl;
using NUnit.Framework;

namespace Grow.Tests.Editor.Lifecycle {
    /// <summary>
    /// Structural guard: every <see cref="UpdateFlag"/> must have a manager event, a facet
    /// interface and a hook on both <see cref="ILifecycle"/> and <see cref="ILifecycleUpdateHooks"/>.
    /// </summary>
    [Category("Grow.Lifecycle")]
    public class LifecycleFacetCoverageTests {
        private static UpdateFlag[] AllFlags() {
            return Enum.GetValues(typeof(UpdateFlag))
                .Cast<UpdateFlag>()
                .Where(flag => flag != UpdateFlag.None)
                .OrderBy(flag => (int)flag)
                .ToArray();
        }

        private static string FacetInterfaceName(UpdateFlag flag) {
            var name = flag.ToString();
            var stem = name == nameof(UpdateFlag.Update)
                ? string.Empty
                : name.Substring(0, name.Length - "Update".Length);
            return $"I{stem}Updatable";
        }

        [Test]
        public void EveryUpdateFlag_HasManagerEvent() {
            foreach (var flag in AllFlags()) {
                var property = typeof(ILifecycleManager).GetProperty(flag.ToString());
                Assert.IsNotNull(property, $"{flag} has no ILifecycleManager event.");
                Assert.AreEqual(typeof(GrowEvent), property.PropertyType,
                    $"{flag} must be a parameterless GrowEvent.");
            }
        }

        [Test]
        public void EveryUpdateFlag_HasFacetInterface() {
            var assembly = typeof(ILifecycle).Assembly;

            foreach (var flag in AllFlags()) {
                var facetName = FacetInterfaceName(flag);
                var facet = assembly.GetType($"Grow.Kernel.Lifecycle.{facetName}");
                Assert.IsNotNull(facet, $"{flag} has no {facetName} facet.");
                Assert.IsTrue(facet.IsInterface, $"{facetName} must be an interface.");
            }
        }

        [Test]
        public void EveryUpdateFlag_HasLifecycleAndHookMethods() {
            foreach (var flag in AllFlags()) {
                var methodName = $"On{flag}";
                Assert.IsNotNull(typeof(ILifecycle).GetMethod(methodName),
                    $"ILifecycle is missing {methodName}.");
                Assert.IsNotNull(typeof(ILifecycleUpdateHooks).GetMethod(methodName),
                    $"ILifecycleUpdateHooks is missing {methodName}.");
            }
        }

        [Test]
        public void AllFacetInterfaces_AreDeclared() {
            var names = new[] {
                "IInitializable",
                "IStartable",
                "IPreUpdatable",
                "IUpdatable",
                "IFixedUpdatable",
                "ILateUpdatable",
                "IPostUpdatable",
                "IDeinitializable",
                "IAppEventAware",
                "IManagedService"
            };

            foreach (var name in names) {
                var type = typeof(ILifecycle).Assembly.GetType($"Grow.Kernel.Lifecycle.{name}");
                Assert.IsNotNull(type, $"Missing facet {name}.");
                Assert.IsTrue(type.IsInterface, $"{name} must be an interface.");
            }
        }

        [Test]
        public void UpdateFlag_Values_AreStable() {
            var declared = string.Join(", ", AllFlags().Select(flag => flag.ToString()));

            Assert.AreEqual("Update, LateUpdate, FixedUpdate, PreUpdate, PostUpdate", declared,
                "Adding or removing a phase requires updating the facets, manager events, hooks and " +
                "UpdaterBinding together.");
        }

        [Test]
        public void FacetNames_FollowTheNamingRule() {
            foreach (var flag in AllFlags()) {
                var name = FacetInterfaceName(flag);
                Assert.IsTrue(name.StartsWith("I") && name.EndsWith("Updatable"),
                    $"{flag} maps to {name}, which breaks the I*Updatable rule.");
            }
        }
    }
}
