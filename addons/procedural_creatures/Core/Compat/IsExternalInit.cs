// Procedural Pixel Creatures - compatibility shims for runtimes older than .NET 5 (Unity's Mono and IL2CPP).
//
// C# 9 'init' accessors need this marker type. .NET 5+ ships it; Unity's class libraries do not. It is
// internal, so it never clashes with a copy defined by another assembly of the host project.

#if !NET5_0_OR_GREATER
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit { }
}
#endif
