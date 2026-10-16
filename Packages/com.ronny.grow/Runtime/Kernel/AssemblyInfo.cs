// SPDX-License-Identifier: MIT

using System.Runtime.CompilerServices;

// The lifecycle machine is internal so that GrowBoot stays the only public way in; the test
// assembly still needs to drive it, and to invalidate it between sessions.
[assembly: InternalsVisibleTo("Grow.Tests.Editor")]
