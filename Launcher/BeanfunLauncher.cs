using System.Diagnostics;
using Beanfun.Beanfun;

namespace Beanfun.Launcher;

public static class BeanfunLauncher
{
    public static void StartGame(Game game, GameAccount gameAccount, string password)
    {
        var gameDir = @"D:\Games\MapleStory\";
        var exeName = @"MapleStory.exe";
        var argsTemplate = "tw.login.maplestory.beanfun.com 8484 BeanFun {0} {1}";
        var args = string.Format(argsTemplate, gameAccount.Id, password);
        Debug.WriteLine(args);
        var info = new ProcessStartInfo
        {
            FileName = gameDir + exeName,
            Arguments = args,
            WorkingDirectory = gameDir
        };
        Process.Start(info);
    }
}