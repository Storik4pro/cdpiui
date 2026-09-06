using System;
using System.Collections.Generic;
using System.Text;

// Source - https://stackoverflow.com/a/75995697
// Posted by m1o2
// Retrieved 2026-07-18, License - CC BY-SA 4.0

namespace System.Diagnostics.CodeAnalysis;

/// <summary>
/// Specifies that this constructor sets all required members for the current type, and callers
/// do not need to set any required members themselves.
/// </summary>
[AttributeUsage(AttributeTargets.Constructor, AllowMultiple = false, Inherited = false)]
#if SYSTEM_PRIVATE_CORELIB
    public
#else
internal
#endif
    sealed class SetsRequiredMembersAttribute : Attribute
{ }


