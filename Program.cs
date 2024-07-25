using RT.PropellerApi;
using RT.Util;

namespace Propeller.KTANE
{
    internal class Program
    {
        static void Main(string[] args)
        {
            PropellerUtil.RunStandalone(PathUtil.AppPathCombine("settings.json"), new KtaneManualRenderer());
        }
    }
}
