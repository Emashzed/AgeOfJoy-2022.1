using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System.IO.Compression;

public class CabinetDBAdmin {

    private static void emptyDir(string path) {
        if (! Directory.Exists(path)) {
            return;
        }

        DirectoryInfo di = new DirectoryInfo(path);
        foreach (FileInfo file in di.GetFiles()) {
            file.Delete(); 
        }
        foreach (DirectoryInfo dir in di.GetDirectories()) {
            dir.Delete(true); 
        }
        return;
    }

    private static void DecompressFile(string path, string destPath)
    {
        ConfigManager.WriteConsole($"[DecompressFile] from {path} to {destPath}");
        // using FileStream compressedFileStream = File.Open(path, FileMode.Open);
        // using FileStream outputFileStream = File.Create(destPath);
        // using var decompressor = new GZipStream(compressedFileStream, CompressionMode.Decompress);
        // decompressor.CopyTo(outputFileStream);
        ZipFile.ExtractToDirectory(path, destPath);
    }
    //load the contents of the zip file and move them to the database cabinet directory. Deletes the original zip file.
    public void loadCabinetFromZip(string path) {
        // string path = $"{ConfigManager.Cabinets}/{cabZipFileName}";
        string cabZipFileName = Path.GetFileNameWithoutExtension(path);
        string pathDest = $"{ConfigManager.CabinetsDB}/{cabZipFileName}/";
        if (!Directory.Exists(pathDest)) {
            Directory.CreateDirectory(pathDest);
        }
        else {
            emptyDir(pathDest);
        }

        // Object.ZipUtility.UncompressFromZip(path, null, $"{ConfigManager.CabinetsDB}/{cabZipFileName}");
        DecompressFile(path, $"{ConfigManager.CabinetsDB}/{cabZipFileName}/");
        File.Delete(path);
    }

    public void loadCabinets() {
        string[] files = Directory.GetFiles(ConfigManager.Cabinets, "*.zip");
        foreach (string file in files) {
            if (File.Exists(file)) {
                try {
                    loadCabinetFromZip(file);
                }
                catch (System.Exception e) {
                    ConfigManager.WriteConsole($"ERROR decompressing Cabinet {file} Exception: {e}");
                }
            }
        }
        return;
    }

}
