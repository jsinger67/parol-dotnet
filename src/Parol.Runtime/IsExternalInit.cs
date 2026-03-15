#if NETSTANDARD2_0
namespace System.Runtime.CompilerServices;

// Required for record/init support when targeting netstandard2.0.
internal static class IsExternalInit
{
}
#endif
