// SPDX-License-Identifier: MIT

namespace Grow.Kernel.Modules {
    /// <summary>Kind of module assembly failure.</summary>
    public enum ModuleFailureKind {
        /// <summary>A declared dependency type has no registered module.</summary>
        MissingDependency,

        /// <summary>A declared dependency type matches more than one module.</summary>
        AmbiguousDependency,

        /// <summary>A dependency failed or is in a cycle, so this module was skipped.</summary>
        DependencyNotReady,

        /// <summary>The module is part of a dependency cycle.</summary>
        DependencyCycle,

        /// <summary>A module lifecycle hook threw and was isolated.</summary>
        HookException,

        /// <summary>A container-driven managed-service hook threw and was isolated.</summary>
        ManagedServiceHookException,

        /// <summary>An <see cref="IBootLoadable.Load"/> pass threw and aborted the boot.</summary>
        LoadException
    }

    /// <summary>One structured assembly failure: subject, kind, phase and reason.</summary>
    public readonly struct ModuleFailure {
        /// <summary>Module name or managed-service type name.</summary>
        public string Name { get; }

        /// <summary>Failure kind.</summary>
        public ModuleFailureKind Kind { get; }

        /// <summary>Phase in which the failure was recorded.</summary>
        public ModulePhase Phase { get; }

        /// <summary>Human-readable reason.</summary>
        public string Reason { get; }

        /// <summary>Creates a failure record.</summary>
        public ModuleFailure(string name, ModuleFailureKind kind, ModulePhase phase, string reason) {
            Name = name ?? string.Empty;
            Kind = kind;
            Phase = phase;
            Reason = reason ?? string.Empty;
        }

        /// <summary>Formats as <c>Name [Kind] @Phase: Reason</c>.</summary>
        public override string ToString() {
            return $"{Name} [{Kind}] @{Phase}: {Reason}";
        }
    }
}
