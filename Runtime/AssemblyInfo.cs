using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Fynite.Tests.Editor")]
[assembly: InternalsVisibleTo("Fynite.Tests.Runtime")]

// The debugger window reads the loop registry without any of it becoming public API.
[assembly: InternalsVisibleTo("Fynite.Editor")]
