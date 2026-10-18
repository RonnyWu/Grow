// SPDX-License-Identifier: MIT

using System.Runtime.CompilerServices;

// The settings pages are internal so they do not become a public API before a second editor
// consumer exists; tests still exercise their registration and metadata.
[assembly: InternalsVisibleTo("Grow.Tests.Editor")]
