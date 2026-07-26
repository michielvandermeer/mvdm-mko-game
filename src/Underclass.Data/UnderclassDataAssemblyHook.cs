namespace Underclass.Data;

/// <summary>
///    A handle on this assembly, for the migration runner to discover the embedded schema through. Exists so
///    that callers say <c>typeof(UnderclassDataAssemblyHook).Assembly</c> rather than reaching for a type that
///    happens to live here today.
/// </summary>
public static class UnderclassDataAssemblyHook;
