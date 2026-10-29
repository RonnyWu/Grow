// SPDX-License-Identifier: MIT

using System.Runtime.CompilerServices;

// The settings pages stay internal; the optional Lens package and the test assemblies are
// explicit friends instead of turning the page shell into public API.
[assembly: InternalsVisibleTo("Grow.Tests.Editor")]
[assembly: InternalsVisibleTo("Grow.Lens.Editor")]
[assembly: InternalsVisibleTo("Grow.Lens.Tests.Editor")]
