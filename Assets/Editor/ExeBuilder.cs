using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Compila el juego para Windows en Builds/Windows (no se sube a git).
// Menú: Herramientas > Compilar para Windows (o batch: -executeMethod ExeBuilder.Build).
public static class ExeBuilder
{
    [MenuItem("Herramientas/Compilar para Windows")]
    public static void Build()
    {
        PlayerSettings.companyName = "LICENE";
        PlayerSettings.productName = "Laboratorio Cooperativo LICENE";
        PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
        PlayerSettings.runInBackground = true; // Sigue sincronizando aunque cambies de ventana.

        Directory.CreateDirectory("Builds/Windows");
        string path = "Builds/Windows/Laboratorio_Cooperativo_LICENE.exe";
        BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
            locationPathName = path,
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None,
        });

        Debug.Log($"ExeBuilder: {report.summary.result} -> {path} ({report.summary.totalSize / 1048576f:0.0} MB, {report.summary.totalTime.TotalMinutes:0.0} min)");
        if (Application.isBatchMode) EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
    }
}
