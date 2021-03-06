﻿using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using KFreonLib.MEDirectories;
using Newtonsoft.Json;

namespace ME3Explorer
{
    public static class ME3TalkFiles
    {
        public static List<TalkFile> tlkList = new List<TalkFile>();

        public static readonly string LoadedTLKsPath = App.AppDataFolder + "ME3LoadedTLKs.JSON";

        public static void LoadTlkData(string fileName)
        {
            if (File.Exists(fileName))
            {
                TalkFile tlk = new TalkFile();
                tlk.LoadTlkData(fileName);
                tlkList.Add(tlk);
            }
        }

        public static void ReloadTLKData()
        {
            tlkList = new List<TalkFile>();
            LoadSavedTlkList();
        }


        public static string findDataById(int strRefID, bool withFileName = false)
        {
            string s = "No Data";
            foreach (TalkFile tlk in tlkList)
            {
                s = tlk.findDataById(strRefID);
                if (s != "No Data")
                {
                    if (withFileName)
                    {
                        s += " (" + tlk.name + ")";
                    }
                    return s;
                }
            }
            return s;
        }

        public static void LoadSavedTlkList()
        {
            if (File.Exists(LoadedTLKsPath))
            {
                List<string> files = JsonConvert.DeserializeObject<List<string>>(File.ReadAllText(LoadedTLKsPath));
                foreach (string filePath in files)
                {
                    LoadTlkData(filePath);
                }
            }
            else
            {
                string tlkPath = ME3Directory.cookedPath + "BIOGame_INT.tlk";
                LoadTlkData(tlkPath);
            }
        }

        public static void SaveTLKList()
        {
            File.WriteAllText(LoadedTLKsPath, JsonConvert.SerializeObject(tlkList.Select(x => x.path)));
        }

        public static void addTLK(string fileName)
        {
            LoadTlkData(fileName);
            SaveTLKList();
        }

        public static void removeTLK(int index)
        {
            tlkList.RemoveAt(index);
            SaveTLKList();
        }

        public static void moveTLKUp(int index)
        {
            TalkFile tlk = tlkList[index];
            tlkList.RemoveAt(index);
            tlkList.Insert(index - 1, tlk);
            SaveTLKList();
        }

        public static void moveTLKDown(int index)
        {
            TalkFile tlk = tlkList[index];
            tlkList.RemoveAt(index);
            tlkList.Insert(index + 1, tlk);
            SaveTLKList();
        }
    }
}
