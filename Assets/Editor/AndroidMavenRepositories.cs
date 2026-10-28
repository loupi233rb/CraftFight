#if UNITY_ANDROID
using System;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor.Android;
using UnityEngine;

/// <summary>Use reachable Maven mirrors when Unity generates this project's Android build.</summary>
public sealed class AndroidMavenRepositories : IPostGenerateGradleAndroidProject
{
    private const string Marker = "// CraftFight Maven mirrors";
    public int callbackOrder => 1000;

    public void OnPostGenerateGradleAndroidProject(string path)
    {
        string settingsPath = Path.Combine(path, "settings.gradle");
        if (!File.Exists(settingsPath))
            settingsPath = Path.GetFullPath(Path.Combine(path, "..", "settings.gradle"));
        if (!File.Exists(settingsPath))
            throw new FileNotFoundException("Cannot locate the generated Android settings.gradle.", settingsPath);
        string settings = File.ReadAllText(settingsPath);
        string updated = AddMirrors(settings);
        if (updated != settings)
            File.WriteAllText(settingsPath, updated);
        Debug.Log("Android build: configured Google/Maven Central mirrors in " + settingsPath);
    }

    internal static string AddMirrors(string settings)
    {
        if (settings.Contains(Marker)) return settings;
        var blocks = new Regex(@"(?m)^([ \t]*)repositories[ \t]*\{[ \t]*\r?$");
        if (blocks.Matches(settings).Count < 2)
            throw new InvalidOperationException("Expected plugin and dependency repository sections in Android settings.gradle.");
        return blocks.Replace(settings, match =>
        {
            string indent = match.Groups[1].Value + "    ";
            return match.Value.TrimEnd('\r') + "\n" + indent + Marker + "\n" +
                indent + "maven { url 'https://maven.aliyun.com/repository/google' }\n" +
                indent + "maven { url 'https://maven.aliyun.com/repository/public' }";
        });
    }
}
#endif