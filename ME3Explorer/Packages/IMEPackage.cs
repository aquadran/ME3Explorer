﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;

namespace ME3Explorer.Packages
{
    public enum MEGame
    {
        ME1 = 1,
        ME2,
        ME3,
        UDK
    }

    public enum ArrayType
    {
        Object,
        Name,
        Enum,
        Struct,
        Bool,
        String,
        Float,
        Int,
        Byte,
    }

    [DebuggerDisplay("PropertyInfo | {type} , parent: {reference}, transient: {transient}")]
    public class PropertyInfo
    {
        public Unreal.PropertyType type { get; set; }
        public string reference;
        public bool transient;
    }

    public class ClassInfo
    {
        public Dictionary<string, PropertyInfo> properties;
        public string baseClass;
        //Relative to BIOGame
        public string pccPath;
        public int exportIndex;

        public ClassInfo()
        {
            properties = new Dictionary<string, PropertyInfo>();
        }
    }

    public interface IMEPackage : IDisposable
    {
        bool IsCompressed { get; }
        bool CanReconstruct { get; }
        bool IsModified { get; }
        int NameCount { get; }
        int ExportCount { get; }
        int ImportCount { get; }
        int ImportOffset { get; }
        IReadOnlyList<IExportEntry> Exports { get; }
        IReadOnlyList<ImportEntry> Imports { get; }
        IReadOnlyList<string> Names { get; }
        MEGame Game { get; }
        string FileName { get; }
        DateTime LastSaved { get; }
        long FileSize { get; }

        //reading
        bool isExport(int index);
        bool isName(int index);
        /// <summary>
        ///     gets Export or Import entry, from unreal index
        /// </summary>
        /// <param name="index">unreal index</param>
        IEntry getEntry(int index);
        /// <summary>
        /// Gets an export based on it's 0 based index in the export list. (Not unreal indexing)
        /// </summary>
        /// <param name="index">0-based index in the export list</param>
        /// <returns></returns>
        IExportEntry getExport(int index);

        /// <summary>
        /// Gets an export based on it's unreal based index in the export list.
        /// </summary>
        /// <param name="index">unreal-based index in the export list</param>
        /// <returns></returns>
        IExportEntry getUExport(int uindex);

        ImportEntry getImport(int index);
        int findName(string nameToFind);
        /// <summary>
        ///     gets Export or Import name, from unreal index
        /// </summary>
        /// <param name="index">unreal index</param>
        string getObjectName(int index);
        string getNameEntry(int index);

        /// <summary>
        ///     gets Export or Import class, from unreal index
        /// </summary>
        /// <param name="index">unreal index</param>
        string getObjectClass(int index);

        //editing
        void addName(string name);
        int FindNameOrAdd(string name);
        void replaceName(int index, string newName);
        void addExport(IExportEntry exportEntry);
        void addImport(ImportEntry importEntry);
        /// <summary>
        ///     exposed so that the property import function can restore the namelist after a failure.
        ///     please don't use it anywhere else.
        /// </summary>
        void setNames(List<string> list);

        string FollowLink(int Link);

        //saving
        void save();
        void save(string path);
        byte[] getHeader();
        ObservableCollection<GenericWindow> Tools { get; }
        void RegisterTool(GenericWindow tool);
        void Release(System.Windows.Window wpfWindow = null, System.Windows.Forms.Form winForm = null);
        event EventHandler noLongerOpenInTools;
        void RegisterUse();
        event EventHandler noLongerUsed;
        string GetEntryString(int index);
    }
}