using RT.PropellerApi;
using RT.Util;

namespace KtaneManualRenderPropeller
{
    internal class Program
    {
        static void Main(string[] args)
        {
            PropellerUtil.RunStandalone(PathUtil.AppPathCombine("settings.json"), new ManualRenderModule());
        }
    }
}
