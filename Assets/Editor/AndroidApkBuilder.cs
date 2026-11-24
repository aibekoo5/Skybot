using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Tools → Cloud Strike → Build Android APK
// Batch: Unity.exe -batchmode -projectPath <SkyBot> -executeMethod AndroidApkBuilder.Build -logFile Logs/apk_build.log -quit
// Нәтиже: Builds/CloudStrike.apk (Build Settings-тегі қосулы сахналар, ARM64, IL2CPP).
public static class AndroidApkBuilder
{
    const string OutFile = "Builds/CloudStrike.apk";

    [MenuItem("Tools/Cloud Strike/Build Android APK")]
    public static void Build()
    {
        string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        if (scenes.Length == 0) { Fail("No enabled scenes in Build Settings"); return; }

        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);

        EditorUserBuildSettings.buildAppBundle = false;               // .aab емес, .apk
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;

        Directory.CreateDirectory(Path.GetDirectoryName(OutFile));
        var options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = OutFile,
            target = BuildTarget.Android,
            targetGroup = BuildTargetGroup.Android,
            options = BuildOptions.None,
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        BuildSummary sum = report.summary;
        string msg = $"[APK] {sum.result}: {Path.GetFullPath(OutFile)}  {sum.totalSize / (1024f * 1024f):0.0} MB, " +
                     $"{sum.totalErrors} errors, {sum.totalTime.TotalMinutes:0.0} min, scenes: {string.Join(", ", scenes)}";

        if (sum.result == BuildResult.Succeeded) Debug.Log(msg);
        else Fail(msg);

        if (Application.isBatchMode) EditorApplication.Exit(sum.result == BuildResult.Succeeded ? 0 : 1);
    }

    static void Fail(string msg)
    {
        Debug.LogError(msg);
        if (Application.isBatchMode) EditorApplication.Exit(1);
    }
}
