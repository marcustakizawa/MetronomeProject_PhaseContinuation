using UnityEditor;
using ChangeComposer.Parsing;

public static class ParserSelfTestMenu
{
    [MenuItem("ChangeComposer/Run Parser Self-Test")]
    private static void Run()
    {
        TextParser_v2.RunSelfTest();
    }
}