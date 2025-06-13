using System.IO;
using UnityEngine;

public static class UserPaths {
    public static string ProjectRoot => Directory.GetParent(Application.dataPath).FullName;
    // public static string ProjectRoot => Application.streamingAssetsPath;

    public const string VRM_FOLDER = "00_vrm";
    public const string VRMA_FOLDER = "00_vrma";
    public const string IMG_FOLDER = "00_img";
    public static string LocalizationPath => GetFullPath("localization.json"); 
    public static string ConfigPath => GetFullPath("config.json");

    // ファイル名のみを渡すと、プロジェクトルート直下のフルパスを返す
    public static string GetFullPath(string fileName) {
        return Path.GetFullPath($"{ProjectRoot}/{fileName}");
    }

    // 指定フォルダ内のファイルのフルパスを返すメソッド
    public static string GetFolderFilePath(string folder, string fileName) {
        return Path.GetFullPath($"{ProjectRoot}/{folder}/{fileName}");
    }

    // VRMAフォルダのファイルパスを取得する専用メソッド
    public static string GetVRMAFilePath(string fileName) {
        return GetFolderFilePath(VRMA_FOLDER, fileName);
    }

    // IMGフォルダのファイルパスを取得する専用メソッド
    public static string GetIMGFilePath(string fileName) {
        return GetFolderFilePath(IMG_FOLDER, fileName);
    }

    // VRMフォルダのファイルパスを取得する専用メソッド
    public static string GetVRMFilePath(string fileName) {
        return GetFolderFilePath(VRM_FOLDER, fileName);
    }
}
