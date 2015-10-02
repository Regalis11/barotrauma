﻿using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace Subsurface
{
    public class SaveUtil
    {
        private const string SaveFolder = "Data/Saves/";

        public delegate void ProgressDelegate(string sMessage);

        public static void SaveGame(string fileName)
        {
            fileName = SaveFolder + fileName;

            string tempPath = Path.Combine(SaveFolder, "temp");
            
            if (Directory.Exists(tempPath))
            {
                Directory.Delete(tempPath, true);                
            }

            Directory.CreateDirectory(tempPath);
            //Directory.CreateDirectory(Path.GetDirectoryName(filePath) + "\\temp");            

            try
            {
                if (Submarine.Loaded!=null)
                {
                    Submarine.Loaded.SaveAs(Path.Combine(tempPath, "map.gz"));
                }
                else
                {
                    File.Copy(GameMain.GameSession.Submarine.FilePath, Path.Combine(tempPath, "map.gz"));
                }
            }
            catch (Exception e)
            {
                DebugConsole.ThrowError("Error saving submarine", e);
            }

            try
            {
                GameMain.GameSession.Save(Path.Combine(tempPath, "gamesession.xml"));
            }

            catch (Exception e)
            {
                DebugConsole.ThrowError("Error saving gamesession", e);
            }
            
            try
            {
                CompressDirectory(tempPath, fileName+".save", null);
            }

            catch (Exception e)
            {
                DebugConsole.ThrowError("Error compressing save file", e);
            }
        }

        public static void LoadGame(string fileName)
        {
            string filePath = Path.Combine(SaveFolder, fileName+".save");
            string tempPath = Path.Combine(SaveFolder, "temp");

            DecompressToDirectory(filePath, tempPath, null);

            Submarine selectedMap = new Submarine(Path.Combine(tempPath, "map.gz"), "");// Submarine.Load();
            GameMain.GameSession = new GameSession(selectedMap, fileName, Path.Combine(tempPath, "gamesession.xml"));

            //Directory.Delete(tempPath, true);
        }

        public static XDocument LoadGameSessionDoc(string fileName)
        {
            string filePath = Path.Combine(SaveFolder, fileName + ".save");

            string tempPath = Path.Combine(SaveFolder, "temp");

            DecompressToDirectory(filePath, tempPath, null);

            return ToolBox.TryLoadXml(Path.Combine(tempPath, "gamesession.xml"));            
        }

        public static void DeleteSave(string fileName)
        {
            fileName = Path.Combine(SaveFolder, fileName + ".save");

            try
            {
                File.Delete(fileName);
            }

            catch (Exception e)
            {
                DebugConsole.ThrowError("ERROR: deleting save file ''"+fileName+" failed.", e);
            }

        }

        public static string[] GetSaveFiles()
        {
            if (!Directory.Exists(SaveFolder))
            {
                DebugConsole.ThrowError("Save folder ''" + SaveFolder + " not found! Attempting to create a new folder");
                try
                {
                    Directory.CreateDirectory(SaveFolder);
                }
                catch (Exception e)
                {
                    DebugConsole.ThrowError("Failed to create the folder ''" + SaveFolder + "''!", e);
                }
            }

            string[] files = Directory.GetFiles(SaveFolder, "*.save");

            for (int i = 0; i < files.Length; i++)
            {
                files[i] = Path.GetFileNameWithoutExtension(files[i]);
            }

            return files;
        }

        public static string CreateSavePath(string fileName="save")
        {
            if (!Directory.Exists(SaveFolder))
            {
                DebugConsole.ThrowError("Save folder ''"+SaveFolder+"'' not found. Created new folder");
                Directory.CreateDirectory(SaveFolder);
            }

            string extension = ".save";
            string pathWithoutExtension = Path.Combine(SaveFolder, fileName);

            int i = 0;
            while (File.Exists(pathWithoutExtension + i + extension))
            {
                i++;
            }

            return fileName + i;
        }

        public static void CompressStringToFile(string fileName, string value)
        {
            // A.
            // Write string to temporary file.
            string temp = Path.GetTempFileName();
            File.WriteAllText(temp, value);

            // B.
            // Read file into byte array buffer.
            byte[] b;
            using (FileStream f = new FileStream(temp, FileMode.Open))
            {
                b = new byte[f.Length];
                f.Read(b, 0, (int)f.Length);
            }

            // C.
            // Use GZipStream to write compressed bytes to target file.
            using (FileStream f2 = new FileStream(fileName, FileMode.Create))
            using (GZipStream gz = new GZipStream(f2, CompressionMode.Compress, false))
            {
                gz.Write(b, 0, b.Length);
            }
        }

        public static Stream DecompressFiletoStream(string fileName)
        {
            if (!File.Exists(fileName))
            {
                DebugConsole.ThrowError("File ''"+fileName+" doesn't exist!");
                return null;
            }

            using (FileStream originalFileStream = new FileStream(fileName, FileMode.Open))
            {
                MemoryStream decompressedFileStream = new MemoryStream();
                
                using (GZipStream decompressionStream = new GZipStream(originalFileStream, CompressionMode.Decompress))
                {
                    decompressionStream.CopyTo(decompressedFileStream);
                    return decompressedFileStream;
                }				
            }
        }
        
        public static void CompressFile(string sDir, string sRelativePath, GZipStream zipStream)
        {
            //Compress file name
            char[] chars = sRelativePath.ToCharArray();
            zipStream.Write(BitConverter.GetBytes(chars.Length), 0, sizeof(int));
            foreach (char c in chars)
                zipStream.Write(BitConverter.GetBytes(c), 0, sizeof(char));

            //Compress file content
            byte[] bytes = File.ReadAllBytes(Path.Combine(sDir, sRelativePath));
            zipStream.Write(BitConverter.GetBytes(bytes.Length), 0, sizeof(int));
            zipStream.Write(bytes, 0, bytes.Length);
        }

        public static bool DecompressFile(string sDir, GZipStream zipStream, ProgressDelegate progress)
        {
            //Decompress file name
            byte[] bytes = new byte[sizeof(int)];
            int Readed = zipStream.Read(bytes, 0, sizeof(int));
            if (Readed < sizeof(int))
                return false;

            int iNameLen = BitConverter.ToInt32(bytes, 0);
            bytes = new byte[sizeof(char)];
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < iNameLen; i++)
            {
                zipStream.Read(bytes, 0, sizeof(char));
                char c = BitConverter.ToChar(bytes, 0);
                sb.Append(c);
            }
            string sFileName = sb.ToString();
            if (progress != null)
                progress(sFileName);

            //Decompress file content
            bytes = new byte[sizeof(int)];
            zipStream.Read(bytes, 0, sizeof(int));
            int iFileLen = BitConverter.ToInt32(bytes, 0);

            bytes = new byte[iFileLen];
            zipStream.Read(bytes, 0, bytes.Length);

            string sFilePath = Path.Combine(sDir, sFileName);
            string sFinalDir = Path.GetDirectoryName(sFilePath);
            if (!Directory.Exists(sFinalDir))
                Directory.CreateDirectory(sFinalDir);

            using (FileStream outFile = new FileStream(sFilePath, FileMode.Create, FileAccess.Write, FileShare.None))
                outFile.Write(bytes, 0, iFileLen);

            return true;
        }

        public static void CompressDirectory(string sInDir, string sOutFile, ProgressDelegate progress)
        {
            string[] sFiles = Directory.GetFiles(sInDir, "*.*", SearchOption.AllDirectories);
            int iDirLen = sInDir[sInDir.Length - 1] == Path.DirectorySeparatorChar ? sInDir.Length : sInDir.Length + 1;

            using (FileStream outFile = new FileStream(sOutFile, FileMode.Create, FileAccess.Write, FileShare.None))
            using (GZipStream str = new GZipStream(outFile, CompressionMode.Compress))
                foreach (string sFilePath in sFiles)
                {
                    string sRelativePath = sFilePath.Substring(iDirLen);
                    if (progress != null)
                        progress(sRelativePath);
                    CompressFile(sInDir, sRelativePath, str);
                }
        }

        public static void DecompressToDirectory(string sCompressedFile, string sDir, ProgressDelegate progress)
        {
            using (FileStream inFile = new FileStream(sCompressedFile, FileMode.Open, FileAccess.Read, FileShare.None))
            using (GZipStream zipStream = new GZipStream(inFile, CompressionMode.Decompress, true))
                while (DecompressFile(sDir, zipStream, progress)) ;
        }
    }
}
