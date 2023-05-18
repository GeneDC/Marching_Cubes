
using System.Runtime.CompilerServices;

public class MathUtils
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Remap(float source, float sourceFrom, float sourceTo, float targetFrom, float targetTo)
    {
        return targetFrom + (source - sourceFrom) * (targetTo - targetFrom) / (sourceTo - sourceFrom);
    }
}
