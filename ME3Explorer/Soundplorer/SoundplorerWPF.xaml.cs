﻿using ByteSizeLib;
using FontAwesome.WPF;
using KFreonLib.Debugging;
using ME3Explorer.Packages;
using ME3Explorer.SharedUI;
using ME3Explorer.SharedUI.Interfaces;
using ME3Explorer.Unreal;
using ME3Explorer.Unreal.Classes;
using ME3Explorer.WwiseBankEditor;
using Microsoft.Win32;
using Microsoft.WindowsAPICodePack.Dialogs;
using NAudio.Wave;
using StreamHelpers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Xml.Linq;

namespace ME3Explorer.Soundplorer
{
    /// <summary>
    /// Interaction logic for SoundplorerWPF.xaml
    /// </summary>
    public partial class SoundplorerWPF : WPFBase, IBusyUIHost
    {
        public static readonly string SoundplorerDataFolder = System.IO.Path.Combine(App.AppDataFolder, @"Soundplorer\");
        private const string RECENTFILES_FILE = "RECENTFILES";
        public List<string> RFiles;
        private string LoadedISBFile;
        private string LoadedAFCFile;
        BackgroundWorker backgroundScanner;
        public ObservableCollectionExtended<object> BindedItemsList { get; set; } = new ObservableCollectionExtended<object>();

        public bool AudioFileLoaded => Pcc != null || LoadedISBFile != null || LoadedAFCFile != null;

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set => SetProperty(ref _isBusy, value);
        }

        private bool _isBusyTaskbar;
        public bool IsBusyTaskbar
        {
            get => _isBusyTaskbar;
            set => SetProperty(ref _isBusyTaskbar, value);
        }

        private string _busyText;
        public string BusyText
        {
            get => _busyText;
            set => SetProperty(ref _busyText, value);
        }

        private string _taskbarText;
        public string TaskbarText
        {
            get => _taskbarText;
            set => SetProperty(ref _taskbarText, value);
        }


        public SoundplorerWPF()
        {
            TaskbarText = "Open a file to view sound-related exports/data";
            InitializeComponent();

            LoadRecentList();
            RefreshRecent(false);
        }

        private void LoadRecentList()
        {
            Recents_MenuItem.IsEnabled = false;
            RFiles = new List<string>();
            RFiles.Clear();
            string path = SoundplorerDataFolder + RECENTFILES_FILE;
            if (File.Exists(path))
            {
                string[] recents = File.ReadAllLines(path);
                foreach (string recent in recents)
                {
                    if (File.Exists(recent))
                    {
                        AddRecent(recent, true);
                    }
                }
            }
        }

        private void SaveRecentList()
        {
            if (!Directory.Exists(SoundplorerDataFolder))
            {
                Directory.CreateDirectory(SoundplorerDataFolder);
            }
            string path = SoundplorerDataFolder + RECENTFILES_FILE;
            if (File.Exists(path))
                File.Delete(path);
            File.WriteAllLines(path, RFiles);
        }

        public void RefreshRecent(bool propogate, List<string> recents = null)
        {
            if (propogate && recents != null)
            {
                foreach (var window in App.Current.Windows)
                {
                    if (window is SoundplorerWPF wpf && this != wpf)
                    {
                        wpf.RefreshRecent(false, RFiles);
                    }
                }
            }
            else if (recents != null)
            {
                //we are receiving an update
                RFiles = new List<string>(recents);
            }
            Recents_MenuItem.Items.Clear();
            if (RFiles.Count <= 0)
            {
                Recents_MenuItem.IsEnabled = false;
                return;
            }
            Recents_MenuItem.IsEnabled = true;


            foreach (string filepath in RFiles)
            {
                MenuItem fr = new MenuItem()
                {
                    Header = filepath.Replace("_", "__"),
                    Tag = filepath
                };
                fr.Click += RecentFile_click;
                Recents_MenuItem.Items.Add(fr);
            }
        }

        private void RecentFile_click(object sender, EventArgs e)
        {
            string s = ((MenuItem)sender).Tag.ToString();
            if (File.Exists(s))
            {
                LoadFile(s);
                AddRecent(s, false);
                SaveRecentList();
                RefreshRecent(true, RFiles);
            }
            else
            {
                MessageBox.Show("File does not exist: " + s);
            }
        }

        public void AddRecent(string s, bool loadingList)
        {
            RFiles = RFiles.Where(x => !x.Equals(s, StringComparison.InvariantCultureIgnoreCase)).ToList();
            if (loadingList)
            {
                RFiles.Add(s); //in order
            }
            else
            {
                RFiles.Insert(0, s); //put at front
            }
            if (RFiles.Count > 10)
            {
                RFiles.RemoveRange(10, RFiles.Count - 10);
            }
            Recents_MenuItem.IsEnabled = true;
        }


        private void OpenCommandBinding_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            OpenFileDialog d = new OpenFileDialog { Filter = "All supported files|*.pcc;*.u;*.sfm;*.upk;*.isb;*.afc|Package files|*.pcc;*.u;*.sfm;*.upk|ISACT Sound Bank files|*.isb|Audio File Cache (AFC)|*.afc" };
            bool? result = d.ShowDialog();
            if (result.HasValue && result.Value)
            {
                try
                {
                    LoadFile(d.FileName);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Unable to open file:\n" + ex.Message);
                }
            }
        }

        public void LoadFile(string fileName)
        {
            try
            {
                soundPanel.FreeAudioResources(); //stop playback
                StatusBar_GameID_Container.Visibility = Visibility.Collapsed;
                TaskbarText = $"Loading {System.IO.Path.GetFileName(fileName)} ({ByteSize.FromBytes(new FileInfo(fileName).Length)})";
                Dispatcher.Invoke(new Action(() => { }), DispatcherPriority.ContextIdle, null);

                StatusBar_GameID_Container.Visibility = Visibility.Visible;

                if (Pcc != null)
                {
                    Pcc.Release();
                    Pcc = null;
                }
                LoadedISBFile = null;

                if (System.IO.Path.GetExtension(fileName).ToLower() == ".isb")
                {
                    LoadedISBFile = fileName;
                    StatusBar_GameID_Text.Text = "ISB";
                    StatusBar_GameID_Text.Background = new SolidColorBrush(Colors.Navy);
                }
                else if (System.IO.Path.GetExtension(fileName).ToLower() == ".afc")
                {
                    LoadedAFCFile = fileName;
                    StatusBar_GameID_Text.Text = "AFC";
                    StatusBar_GameID_Text.Background = new SolidColorBrush(Colors.DarkOrchid);
                }
                else
                {
                    LoadMEPackage(fileName);
                    switch (Pcc.Game)
                    {
                        case MEGame.ME1:
                            StatusBar_GameID_Text.Text = "ME1";
                            StatusBar_GameID_Text.Background = new SolidColorBrush(Colors.Navy);
                            break;
                        case MEGame.ME2:
                            StatusBar_GameID_Text.Text = "ME2";
                            StatusBar_GameID_Text.Background = new SolidColorBrush(Colors.Maroon);
                            break;
                        case MEGame.ME3:
                            StatusBar_GameID_Text.Text = "ME3";
                            StatusBar_GameID_Text.Background = new SolidColorBrush(Colors.DarkSeaGreen);
                            break;
                        case MEGame.UDK:
                            StatusBar_GameID_Text.Text = "UDK";
                            StatusBar_GameID_Text.Background = new SolidColorBrush(Colors.IndianRed);
                            break;
                    }
                }


                if (LoadedISBFile != null)
                {
                    LoadISB();
                }
                else if (LoadedAFCFile != null)
                {
                    LoadAFC();
                }
                else
                {
                    LoadObjects();
                }
                Title = $"Soundplorer - {System.IO.Path.GetFileName(fileName)}";
                OnPropertyChanged(nameof(AudioFileLoaded));
                AddRecent(fileName, false);
                SaveRecentList();
                RefreshRecent(true, RFiles);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error:\n" + ex.Message);
            }
        }

        private async void LoadAFC()
        {
            BindedItemsList.ClearEx();
            IsBusyTaskbar = true;
            TaskbarText = $"Loading AFC: {System.IO.Path.GetFileName(LoadedAFCFile)}";
            if (backgroundScanner != null && backgroundScanner.IsBusy)
            {
                backgroundScanner.CancelAsync(); //cancel current operation
                while (backgroundScanner.IsBusy)
                {
                    //I am sorry for this. I truely am. 
                    //But it's the simplest code to get the job done while we wait for the thread to finish.
                    await Task.Delay(200);
                }
            }

            //Background thread as larger AFC parsing can lock up the UI for a few seconds.
            backgroundScanner = new BackgroundWorker();
            backgroundScanner.DoWork += LoadAFCFile;
            backgroundScanner.RunWorkerCompleted += LoadAFCFile_Completed;
            backgroundScanner.WorkerSupportsCancellation = true;
            backgroundScanner.RunWorkerAsync(LoadedAFCFile);
        }

        private void LoadAFCFile_Completed(object sender, RunWorkerCompletedEventArgs e)
        {
            if (e.Result is List<AFCFileEntry> result)
            {
                BindedItemsList.AddRange(result);
                backgroundScanner = new BackgroundWorker();
                backgroundScanner.DoWork += GetStreamTimes;
                backgroundScanner.RunWorkerCompleted += GetStreamTimes_Completed;
                backgroundScanner.WorkerReportsProgress = true;
                backgroundScanner.ProgressChanged += GetStreamTimes_ReportProgress;
                backgroundScanner.WorkerSupportsCancellation = true;
                backgroundScanner.RunWorkerAsync(result.Cast<object>().ToList());
            }
        }

        private void LoadAFCFile(object sender, DoWorkEventArgs e)
        {
            var entries = new List<AFCFileEntry>();
            using (FileStream fileStream = new FileStream((string)e.Argument, FileMode.Open, FileAccess.Read))
            {
                while (fileStream.Position < fileStream.Length - 4)
                {
                    int offset = (int)fileStream.Position;

                    TaskbarText = $"Loading AFC: {System.IO.Path.GetFileName(LoadedAFCFile)} ({(int)((fileStream.Position * 100.0) / fileStream.Length)}%)";
                    string readStr = fileStream.ReadStringASCII(4);
                    if (readStr != "RIFF")
                    {
                        fileStream.Seek(-3, SeekOrigin.Current);
                        continue;
                    }

                    //Found RIFF
                    int size = fileStream.ReadInt32();
                    fileStream.Seek(8, SeekOrigin.Current); //skip WAVE and fmt

                    short wwiseVersionMaybe = fileStream.ReadInt16();

                    fileStream.Seek(size - 10, SeekOrigin.Current);

                    var entry = new AFCFileEntry(LoadedAFCFile, offset, size + 8, wwiseVersionMaybe == 0x28);
                    entries.Add(entry);
                }
            }
            e.Result = entries;
            return;
        }

        private async void LoadISB()
        {
            BindedItemsList.ClearEx();
            IsBusyTaskbar = true;
            TaskbarText = $"Loading ISB: {System.IO.Path.GetFileName(LoadedISBFile)}";
            if (backgroundScanner != null && backgroundScanner.IsBusy)
            {
                backgroundScanner.CancelAsync(); //cancel current operation
                while (backgroundScanner.IsBusy)
                {
                    //I am sorry for this. I truely am. 
                    //But it's the simplest code to get the job done while we wait for the thread to finish.
                    await Task.Delay(200);
                }
            }

            //Background thread as larger ISB parsing can lock up the UI for a few seconds.
            backgroundScanner = new BackgroundWorker();
            backgroundScanner.DoWork += LoadISBFile;
            backgroundScanner.RunWorkerCompleted += LoadISBFile_Completed;
            backgroundScanner.WorkerSupportsCancellation = true;
            backgroundScanner.RunWorkerAsync(LoadedISBFile);
        }

        private void LoadISBFile_Completed(object sender, RunWorkerCompletedEventArgs e)
        {
            if (e.Result is ISBank result)
            {
                var entries = new List<ISACTFileEntry>(result.BankEntries.Where(x => x.DataAsStored != null).Select(x => new ISACTFileEntry(x)));
                BindedItemsList.AddRange(entries);
                backgroundScanner = new BackgroundWorker();
                backgroundScanner.DoWork += GetStreamTimes;
                backgroundScanner.RunWorkerCompleted += GetStreamTimes_Completed;
                backgroundScanner.WorkerReportsProgress = true;
                backgroundScanner.ProgressChanged += GetStreamTimes_ReportProgress;
                backgroundScanner.WorkerSupportsCancellation = true;
                backgroundScanner.RunWorkerAsync(entries.Cast<object>().ToList());
            }
        }

        private void LoadISBFile(object sender, DoWorkEventArgs e)
        {
            ISBank bank = new ISBank((string)e.Argument);
            e.Result = bank;
        }

        private void GetStreamTimes(object sender, DoWorkEventArgs e)
        {
            var ExportsToLoad = (List<object>)e.Argument;
            int i = 0;
            foreach (object se in ExportsToLoad)
            {
                if (backgroundScanner.CancellationPending)
                {
                    e.Cancel = true;
                    return;
                }
                backgroundScanner.ReportProgress((int)((i * 100.0) / BindedItemsList.Count));
                //Debug.WriteLine("Getting time for " + se.Export.UIndex);
                switch (se)
                {
                    case SoundplorerExport export:
                        export.LoadData();
                        break;
                    case ISACTFileEntry entry:
                        entry.LoadData();
                        break;
                    case AFCFileEntry aentry:
                        aentry.LoadData();
                        break;
                }
                i++;
            }
        }

        private async void LoadObjects(List<SoundplorerExport> exportsToReload = null)
        {
            if (exportsToReload == null)
            {
                BindedItemsList.Clear();
                BindedItemsList.AddRange(Pcc.Exports.Where(e => e.ClassName == "WwiseBank" || e.ClassName == "WwiseStream").Select(x => new SoundplorerExport(x)));
                //SoundExports_ListBox.ItemsSource = BindedExportsList; //todo: figure out why this is required and data is not binding
            }
            else
            {
                foreach (SoundplorerExport se in exportsToReload)
                {
                    se.SubText = "Refreshing";
                    se.NeedsLoading = true;
                    se.Icon = FontAwesomeIcon.Spinner;
                }
            }
            if (backgroundScanner != null && backgroundScanner.IsBusy)
            {
                backgroundScanner.CancelAsync(); //cancel current operation
                while (backgroundScanner.IsBusy)
                {
                    //I am sorry for this. I truely am. 
                    //But it's the simplest code to get the job done while we wait for the thread to finish.
                    await Task.Delay(200);
                }
            }


            backgroundScanner = new BackgroundWorker();
            backgroundScanner.DoWork += GetStreamTimes;
            backgroundScanner.RunWorkerCompleted += GetStreamTimes_Completed;
            backgroundScanner.WorkerReportsProgress = true;
            backgroundScanner.ProgressChanged += GetStreamTimes_ReportProgress;
            backgroundScanner.WorkerSupportsCancellation = true;
            backgroundScanner.RunWorkerAsync(exportsToReload != null ? exportsToReload.Cast<object>().ToList() : BindedItemsList.ToList());
            IsBusyTaskbar = true;
            //string s = i.ToString("d6") + " : " + e.ClassName + " : \"" + e.ObjectName + "\"";
        }

        private void GetStreamTimes_ReportProgress(object sender, ProgressChangedEventArgs e)
        {
            IsBusyTaskbar = true; //enforce spinner
            TaskbarText = "Parsing " + System.IO.Path.GetFileName(LoadedISBFile ?? LoadedAFCFile ?? Pcc.FileName) + " (" + e.ProgressPercentage + "%)";
        }

        private void GetStreamTimes_Completed(object sender, RunWorkerCompletedEventArgs e)
        {
            TaskbarText = System.IO.Path.GetFileName(LoadedISBFile ?? LoadedAFCFile ?? Pcc.FileName);
            IsBusyTaskbar = false;
        }

        private void SaveCommandBinding_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            Pcc.save();
        }

        private void SaveAsCommandBinding_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            SaveFileDialog d = new SaveFileDialog();
            string extension = System.IO.Path.GetExtension(Pcc.FileName);
            d.Filter = $"*{extension}|*{extension}";
            bool? result = d.ShowDialog();
            if (result.HasValue && result.Value)
            {
                Pcc.save(d.FileName);
                MessageBox.Show("Done");
            }
        }

        public override void handleUpdate(List<PackageUpdate> updates)
        {
            if (LoadedISBFile != null || LoadedAFCFile != null)
            {
                return; //we don't handle updates on ISB or AFC
            }

            List<SoundplorerExport> bindedListAsCasted = BindedItemsList.Cast<SoundplorerExport>().ToList();
            List<PackageChange> changes = updates.Select(x => x.change).ToList();
            bool importChanges = changes.Contains(PackageChange.Import) || changes.Contains(PackageChange.ImportAdd);
            bool exportNonDataChanges = changes.Contains(PackageChange.ExportHeader) || changes.Contains(PackageChange.ExportAdd);

            var loadedIndexes = bindedListAsCasted.Where(x => x.Export != null).Select(y => y.Export.Index).ToList();

            List<SoundplorerExport> exportsRequiringReload = new List<SoundplorerExport>();
            foreach (PackageUpdate pc in updates)
            {
                if (loadedIndexes.Contains(pc.index))
                {
                    SoundplorerExport sp = bindedListAsCasted.First(x => x.Export.Index == pc.index);
                    exportsRequiringReload.Add(sp);
                }
            }



            if (exportsRequiringReload.Any())
            {
                SoundplorerExport spExport = (SoundplorerExport)SoundExports_ListBox.SelectedItem;
                if (spExport == null)
                {
                    if (exportsRequiringReload.Contains(spExport))
                    {
                        soundPanel.FreeAudioResources(); //unload the current export
                    }
                }
                LoadObjects(exportsRequiringReload);

                if (spExport != null)
                {
                    soundPanel.LoadExport(spExport.Export); //reload
                }
            }
        }

        private void SoundExports_ListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            object selectedItem = SoundExports_ListBox.SelectedItem;
            SoundplorerExport spExport = selectedItem as SoundplorerExport;
            if (spExport == null)
            {
                soundPanel.UnloadExport();
            }
            ISACTFileEntry isEntry = selectedItem as ISACTFileEntry;
            if (isEntry == null)
            {
                soundPanel.UnloadISACTEntry();
            }

            AFCFileEntry aEntry = selectedItem as AFCFileEntry;
            if (aEntry == null)
            {
                soundPanel.UnloadAFCEntry();
            }


            if (isEntry != null) soundPanel.LoadISACTEntry(isEntry.Entry);
            if (spExport != null) soundPanel.LoadExport(spExport.Export);
            if (aEntry != null) soundPanel.LoadAFCEntry(aEntry);
        }

        private void Soundplorer_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (backgroundScanner != null && backgroundScanner.IsBusy)
            {
                backgroundScanner.CancelAsync();
            }
            soundPanel.FreeAudioResources();
        }

        private void ExtractWEMAsWaveFromBank_Clicked(object sender, RoutedEventArgs e)
        {
            SoundplorerExport spExport = (SoundplorerExport)SoundExports_ListBox.SelectedItem;
            ExtractBankToWav(spExport);
        }

        private void ExtractBankToWav(SoundplorerExport spExport, string location = null)
        {
            if (spExport != null && spExport.Export.ClassName == "WwiseBank")
            {
                WwiseBank wb = new WwiseBank(spExport.Export);
                List<(uint, int, int)> embeddedWEMFiles = wb.GetWEMFilesMetadata();
                if (embeddedWEMFiles.Count > 0)
                {
                    if (location == null)
                    {
                        var dlg = new CommonOpenFileDialog("Select output folder")
                        {
                            IsFolderPicker = true
                        };

                        if (dlg.ShowDialog() != CommonFileDialogResult.Ok)
                        {
                            return;
                        }
                        location = dlg.FileName;
                    }

                    byte[] data = wb.GetChunk("DATA");
                    if (embeddedWEMFiles.Count > 0)
                    {
                        foreach ((uint wemID, int offset, int size) singleWemMetadata in embeddedWEMFiles)
                        {
                            byte[] wemData = new byte[singleWemMetadata.size];
                            //copy WEM data to buffer. Add 0x8 to skip DATA and DATASIZE header for this block.
                            Buffer.BlockCopy(data, singleWemMetadata.offset + 0x8, wemData, 0, singleWemMetadata.size);
                            //check for RIFF header as some don't seem to have it and are not playable.
                            string wemHeader = "" + (char)wemData[0] + (char)wemData[1] + (char)wemData[2] + (char)wemData[3];
                            string wemName = $"{spExport.Export.ObjectName}_0x{singleWemMetadata.wemID:X8}";

                            if (wemHeader == "RIFF")
                            {
                                EmbeddedWEMFile wem = new EmbeddedWEMFile(wemData, wemName, spExport.Export.FileRef.Game); //will correct truncated stuff
                                Stream waveStream = soundPanel.getPCMStream(forcedWemFile: wem);
                                if (waveStream != null && waveStream.Length > 0)
                                {
                                    string outputname = wemName + ".wav";
                                    string outpath = System.IO.Path.Combine(location, outputname);
                                    using (var fileStream = File.Create(outpath))
                                    {
                                        waveStream.Seek(0, SeekOrigin.Begin);
                                        waveStream.CopyTo(fileStream);
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        private void ExtractBank_Clicked(object sender, RoutedEventArgs e)
        {
            SoundplorerExport spExport = (SoundplorerExport)SoundExports_ListBox.SelectedItem;
            if (spExport != null && spExport.Export.ClassName == "WwiseBank")
            {
                ExportBank(spExport);
            }
        }

        private void ExportBank(SoundplorerExport spExport)
        {
            SaveFileDialog d = new SaveFileDialog
            {
                Filter = "WwiseBank|*.bnk",
                FileName = spExport.Export.ObjectName + ".bnk",
                Title = "Select save location for bank file"
            };
            bool? res = d.ShowDialog();
            if (res.HasValue && res.Value)
            {
                //File.WriteAllBytes(d.FileName, spExport.Export.getBinaryData());
                File.WriteAllBytes(d.FileName, spExport.Export.Data);
                MessageBox.Show("Done.");
            }
        }

        private void CompactAFC_Clicked(object sender, RoutedEventArgs e)
        {
            var dlg = new CommonOpenFileDialog("Select mod's CookedPCConsole folder")
            {
                IsFolderPicker = true
            };

            if (dlg.ShowDialog() != CommonFileDialogResult.Ok)
            {
                return;
            }

            string[] afcFiles = System.IO.Directory.GetFiles(dlg.FileName, "*.afc");
            string[] pccFiles = System.IO.Directory.GetFiles(dlg.FileName, "*.pcc");

            if (afcFiles.Any() && pccFiles.Any())
            {
                string foldername = System.IO.Path.GetFileName(dlg.FileName);
                if (foldername.ToLower() == "cookedpcconsole")
                {
                    foldername = System.IO.Path.GetFileName(System.IO.Directory.GetParent(dlg.FileName).FullName);
                }
                string result = PromptDialog.Prompt(this, "Enter an AFC filename that all mod referenced items will be repointed to.\n\nCompacting AFC folder: " + foldername, "Enter an AFC filename");
                if (result != null)
                {
                    var regex = new Regex(@"^[a-zA-Z0-9_]+$");

                    if (regex.IsMatch(result))
                    {
                        BusyText = "Finding all referenced audio";
                        IsBusy = true;
                        IsBusyTaskbar = true;
                        soundPanel.FreeAudioResources(); // stop playing
                        BackgroundWorker afcCompactWorker = new BackgroundWorker();
                        afcCompactWorker.DoWork += CompactAFCBackgroundThread;
                        afcCompactWorker.RunWorkerCompleted += compactAFCBackgroundThreadCompleted;
                        afcCompactWorker.RunWorkerAsync((dlg.FileName, result));
                    }
                    else
                    {
                        MessageBox.Show("Only alphanumeric characters and underscores are allowed for the AFC filename.", "Error creating AFC");
                    }
                }
            }

        }

        private void compactAFCBackgroundThreadCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            IsBusy = false;
            IsBusyTaskbar = false;
        }

        private void CompactAFCBackgroundThread(object sender, DoWorkEventArgs e)
        {
            (string path, string NewAFCBaseName) = (ValueTuple<string, string>)e.Argument;

            string[] pccFiles = System.IO.Directory.GetFiles(path, "*.pcc");
            string[] afcFiles = System.IO.Directory.GetFiles(path, "*.afc").Select(x => System.IO.Path.GetFileNameWithoutExtension(x).ToLower()).ToArray();

            var ReferencedAFCAudio = new List<(string, int, int)>();

            int i = 1;
            foreach (string pccPath in pccFiles)
            {
                BusyText = $"Finding all referenced audio ({i}/{pccFiles.Length})";
                using (IMEPackage pack = MEPackageHandler.OpenMEPackage(pccPath))
                {
                    List<IExportEntry> wwiseStreamExports = pack.Exports.Where(x => x.ClassName == "WwiseStream").ToList();
                    foreach (IExportEntry exp in wwiseStreamExports)
                    {
                        var afcNameProp = exp.GetProperty<NameProperty>("Filename");
                        if (afcNameProp != null && afcFiles.Contains(afcNameProp.ToString().ToLower()))
                        {
                            string afcName = afcNameProp.ToString().ToLower();
                            int readPos = exp.Data.Length - 8;
                            int audioSize = BitConverter.ToInt32(exp.Data, exp.Data.Length - 8);
                            int audioOffset = BitConverter.ToInt32(exp.Data, exp.Data.Length - 4);
                            ReferencedAFCAudio.Add((afcName, audioSize, audioOffset));
                        }
                    }
                }
                i++;
            }
            ReferencedAFCAudio = ReferencedAFCAudio.Distinct().ToList();

            //extract referenced audio
            BusyText = "Extracting referenced audio";
            var extractedAudioMap = new Dictionary<(string, int, int), byte[]>();
            i = 1;
            foreach ((string afcName, int audioSize, int audioOffset) reference in ReferencedAFCAudio)
            {
                BusyText = $"Extracting referenced audio ({i} / {ReferencedAFCAudio.Count})";
                string afcPath = System.IO.Path.Combine(path, reference.afcName + ".afc");
                FileStream stream = new FileStream(afcPath, FileMode.Open, FileAccess.Read);
                stream.Seek(reference.audioOffset, SeekOrigin.Begin);
                var extractedAudio = new byte[reference.audioSize];
                stream.Read(extractedAudio, 0, reference.audioSize);
                stream.Close();
                extractedAudioMap[reference] = extractedAudio;
                i++;
            }

            var newAFCEntryPointMap = new Dictionary<(string, int, int), long>();
            i = 1;
            string newAfcPath = System.IO.Path.Combine(path, NewAFCBaseName + ".afc");
            if (File.Exists(newAfcPath))
            {
                File.Delete(newAfcPath);
            }
            FileStream newAFCStream = new FileStream(newAfcPath, FileMode.CreateNew, FileAccess.Write);

            foreach ((string, int, int) reference in ReferencedAFCAudio)
            {
                BusyText = $"Building new AFC file ({i} / {ReferencedAFCAudio.Count})";
                newAFCEntryPointMap[reference] = newAFCStream.Position; //save entry point in map
                newAFCStream.Write(extractedAudioMap[reference], 0, extractedAudioMap[reference].Length);
                i++;
            }
            newAFCStream.Close();
            extractedAudioMap = null; //clean out ram on next GC

            i = 1;
            foreach (string pccPath in pccFiles)
            {
                BusyText = $"Updating audio references ({i}/{pccFiles.Length})";
                using (IMEPackage pack = MEPackageHandler.OpenMEPackage(pccPath))
                {
                    bool shouldSave = false;
                    List<IExportEntry> wwiseStreamExports = pack.Exports.Where(x => x.ClassName == "WwiseStream").ToList();
                    foreach (IExportEntry exp in wwiseStreamExports)
                    {
                        var afcNameProp = exp.GetProperty<NameProperty>("Filename");
                        if (afcNameProp != null && afcFiles.Contains(afcNameProp.ToString().ToLower()))
                        {
                            string afcName = afcNameProp.ToString().ToLower();
                            int readPos = exp.Data.Length - 8;
                            int audioSize = BitConverter.ToInt32(exp.Data, exp.Data.Length - 8);
                            int audioOffset = BitConverter.ToInt32(exp.Data, exp.Data.Length - 4);
                            var key = (afcName, audioSize, audioOffset);
                            if (newAFCEntryPointMap.TryGetValue(key, out long newOffset))
                            {
                                //its a match
                                afcNameProp.Value = NewAFCBaseName;
                                Application.Current.Dispatcher.Invoke(() =>
                                {
                                    exp.WriteProperty(afcNameProp);
                                    byte[] newData = exp.Data;
                                    Buffer.BlockCopy(BitConverter.GetBytes((int)newOffset), 0, newData, newData.Length - 4, 4); //update AFC audio offset
                                    exp.Data = newData;
                                    if (exp.DataChanged)
                                    {
                                        //don't mark for saving if the data didn't acutally change (e.g. trying to compact a compacted AFC).
                                        shouldSave = true;
                                    }
                                });
                            }
                        }
                    }
                    if (shouldSave)
                    {
                        // Must run on the UI thread or the tool interop will throw an exception
                        // because we are on a background thread.
                        Application.Current.Dispatcher.Invoke(pack.save);
                    }
                }
                i++;
            }
            BusyText = "Rebuild complete";
            System.Threading.Thread.Sleep(2000);
        }

        private void ExportWav_Clicked(object sender, RoutedEventArgs e)
        {
            switch (SoundExports_ListBox.SelectedItem)
            {
                case SoundplorerExport spE:
                    ExportWave(spE);
                    break;
                case AFCFileEntry afE:
                    ExportWaveAFC(afE);
                    break;
            }
        }

        private void ExportWaveAFC(AFCFileEntry afE, string location = null)
        {
            bool silent = location != null;
            if (!silent)
            {
                string presetfilename = $"{System.IO.Path.GetFileNameWithoutExtension(afE.AFCPath)}_{afE.Offset}.wav";
                SaveFileDialog d = new SaveFileDialog
                {
                    Filter = "Wave PCM File|*.wav",
                    FileName = presetfilename
                };
                if (d.ShowDialog().Value)
                {
                    location = d.FileName;
                }
            }

            if (location != null)
            {
                using (Stream s = WwiseStream.CreateWaveStreamFromRaw(afE.AFCPath, afE.Offset, afE.DataSize, afE.ME2))
                {
                    using (var fileStream = File.Create(location))
                    {
                        s.Seek(0, SeekOrigin.Begin);
                        s.CopyTo(fileStream);
                    }
                }
            }
            if (!silent)
            {
                MessageBox.Show("Done.");
            }
        }


        private void ExportWave(SoundplorerExport spExport, string outputLocation = null)
        {
            if (spExport != null && spExport.Export.ClassName == "WwiseStream")
            {
                bool silent = outputLocation != null;
                if (outputLocation == null)
                {
                    SaveFileDialog d = new SaveFileDialog
                    {
                        Filter = "Wave PCM|*.wav",
                        FileName = spExport.Export.ObjectName + ".wav"
                    };
                    bool? res = d.ShowDialog();
                    if (res.HasValue && res.Value)
                    {
                        outputLocation = d.FileName;
                    }
                    else
                    {
                        return;
                    }
                }

                WwiseStream w = new WwiseStream(spExport.Export);
                Stream source = w.CreateWaveStream(w.getPathToAFC());
                if (source != null)
                {
                    using (var fileStream = File.Create(outputLocation))
                    {
                        source.Seek(0, SeekOrigin.Begin);
                        source.CopyTo(fileStream);
                    }
                    if (!silent)
                    {
                        MessageBox.Show("Done.");
                    }
                }
                else
                {
                    if (!silent)
                    {
                        MessageBox.Show("Error creating Wave file.\nThis might not be a supported codec or the AFC data may be incorrect.");
                    }
                }
            }
        }

        private void ExportRaw_Clicked(object sender, RoutedEventArgs e)
        {
            switch (SoundExports_ListBox.SelectedItem)
            {
                case SoundplorerExport spExport:
                    if (spExport != null && spExport.Export.ClassName == "WwiseStream")
                    {
                        SaveFileDialog d = new SaveFileDialog
                        {
                            Filter = "Wwise WEM|*.wem",
                            FileName = spExport.Export.ObjectName + ".wem"
                        };
                        bool? res = d.ShowDialog();
                        if (res.HasValue && res.Value)
                        {
                            WwiseStream w = new WwiseStream(spExport.Export);
                            if (w.ExtractRawFromSource(d.FileName, w.getPathToAFC()))
                            {
                                MessageBox.Show("Done.");
                            }
                            else
                            {
                                MessageBox.Show("Error extracting WEM file.\nMetadata for this raw data may be incorrect (e.g. too big for file).");
                            }
                        }
                    }
                    break;
                case AFCFileEntry afcEntry:
                    {
                        string presetfilename = $"{System.IO.Path.GetFileNameWithoutExtension(afcEntry.AFCPath)}_{afcEntry.Offset}.wem";

                        SaveFileDialog d = new SaveFileDialog
                        {
                            Filter = "Wwise WEM|*.wem",
                            FileName = presetfilename
                        };
                        bool? res = d.ShowDialog();
                        if (res.HasValue && res.Value)
                        {
                            if (WwiseStream.ExtractRawFromSource(d.FileName, afcEntry.AFCPath, afcEntry.DataSize, afcEntry.Offset))
                            {
                                MessageBox.Show("Done.");
                            }
                            else
                            {
                                MessageBox.Show("Error extracting AFC WEM file.");
                            }
                        }
                    }
                    break;
            }
        }

        private void ExportOgg_Clicked(object sender, RoutedEventArgs e)
        {
            if (SoundExports_ListBox.SelectedItem is SoundplorerExport spExport && spExport.Export.ClassName == "WwiseStream")
            {
                SaveFileDialog d = new SaveFileDialog
                {
                    Filter = "Ogg Vorbis|*.ogg",
                    FileName = spExport.Export.ObjectName + ".ogg"
                };
                bool? res = d.ShowDialog();
                if (res.HasValue && res.Value)
                {
                    WwiseStream w = new WwiseStream(spExport.Export);
                    string riffOutputFile = System.IO.Path.Combine(Directory.GetParent(d.FileName).FullName, System.IO.Path.GetFileNameWithoutExtension(d.FileName)) + ".dat";

                    if (w.ExtractRawFromSource(riffOutputFile, w.getPathToAFC()))
                    {
                        MemoryStream oggStream = WwiseStream.ConvertRIFFToWWwiseOGG(riffOutputFile, spExport.Export.FileRef.Game == MEGame.ME2);
                        //string outputOggPath = 
                        if (oggStream != null)// && File.Exists(outputOggPath))
                        {
                            oggStream.Seek(0, SeekOrigin.Begin);
                            using (FileStream fs = new FileStream(d.FileName, FileMode.OpenOrCreate))
                            {
                                oggStream.CopyTo(fs);
                                fs.Flush();
                            }
                            MessageBox.Show("Done.");
                        }
                        else
                        {
                            MessageBox.Show("Error extracting Ogg file.\nMetadata for the raw data may be incorrect (e.g. header specifies data is longer than it actually is).");
                        }
                    }
                }
            }

            if (SoundExports_ListBox.SelectedItem is AFCFileEntry afE)
            {
                string presetfilename = $"{System.IO.Path.GetFileNameWithoutExtension(afE.AFCPath)}_{afE.Offset}.ogg";
                SaveFileDialog d = new SaveFileDialog
                {
                    Filter = "Ogg Vorbis|*.ogg",
                    FileName = presetfilename
                };
                bool? res = d.ShowDialog();
                if (res.HasValue && res.Value)
                {
                    string riffOutputFile = System.IO.Path.Combine(Directory.GetParent(d.FileName).FullName, System.IO.Path.GetFileNameWithoutExtension(d.FileName)) + ".dat";

                    if (WwiseStream.ExtractRawFromSource(riffOutputFile, afE.AFCPath, afE.DataSize, afE.Offset))
                    {
                        MemoryStream oggStream = WwiseStream.ConvertRIFFToWWwiseOGG(riffOutputFile, afE.ME2);
                        //string outputOggPath = 
                        if (oggStream != null)// && File.Exists(outputOggPath))
                        {
                            oggStream.Seek(0, SeekOrigin.Begin);
                            using (FileStream fs = new FileStream(d.FileName, FileMode.OpenOrCreate))
                            {
                                oggStream.CopyTo(fs);
                                fs.Flush();
                            }
                            MessageBox.Show("Done.");
                        }
                        else
                        {
                            MessageBox.Show("Error extracting and converting to Ogg file.");
                        }
                    }
                }
            }
        }

        private void CloneAndReplace_Clicked(object sender, RoutedEventArgs e)
        {
            if (SoundExports_ListBox.SelectedItem is SoundplorerExport)
            {
                CloneAndReplace(false);
            }
        }

        private void CloneAndReplace(bool fromWave)
        {
            string result = PromptDialog.Prompt(this, "Enter a new object name for the cloned item.", "Cloned export name");
            if (result != null)
            {
                SoundplorerExport spExport = (SoundplorerExport)SoundExports_ListBox.SelectedItem;
                if (spExport != null && spExport.Export.ClassName == "WwiseStream")
                {
                    IExportEntry clone = spExport.Export.Clone();
                    clone.idxObjectName = clone.FileRef.FindNameOrAdd(result);
                    spExport.Export.FileRef.addExport(clone);
                    SoundplorerExport newExport = new SoundplorerExport(clone);
                    BindedItemsList.Add(newExport);
                    var reloadList = new List<SoundplorerExport> { newExport };
                    SoundExports_ListBox.ScrollIntoView(newExport);
                    SoundExports_ListBox.UpdateLayout();
                    if (SoundExports_ListBox.ItemContainerGenerator.ContainerFromItem(newExport) is ListBoxItem item)
                    {
                        item.Focus();
                    }
                    if (fromWave)
                    {
                        soundPanel.ReplaceAudioFromWwiseOgg(forcedExport: clone);
                    }
                    else
                    {
                        soundPanel.ReplaceAudioFromWave(forcedExport: clone);
                    }
                    LoadObjects(reloadList);
                }
            }
        }

        private void ReplaceAudio_Clicked(object sender, RoutedEventArgs e)
        {
            if (SoundExports_ListBox.SelectedItem is SoundplorerExport spExport)
            {
                soundPanel.ReplaceAudioFromWwiseOgg(forcedExport: spExport.Export);
            }
        }

        private void Window_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop) &&
                // Note that you can have more than one file.
                e.Data.GetData(DataFormats.FileDrop) is string[] files)
            {

                // Assuming you have one file that you care about, pass it off to whatever
                // handling code you have defined.
                LoadFile(files[0]);
            }
        }

        /// <summary>
        /// Convert files to Wwise-encoded Oggs. Requires Wwise to be installed.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void ConvertFolderToWwise_Clicked(object sender, RoutedEventArgs e)
        {
            if (Directory.Exists(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "TemplateProject")))
            {
                await Soundpanel.TryDeleteDirectory(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "TemplateProject"));
            }

            //Verify Wwise is installed with the correct version
            string wwisePath = Soundpanel.GetWwiseCLIPath(false);
            if (wwisePath == null) return; //abort. getpath is not silent so it will show dialogs before this is reached.
            var dlg = new CommonOpenFileDialog("Select folder containing .wav files") { IsFolderPicker = true };
            if (dlg.ShowDialog() != CommonFileDialogResult.Ok) { return; }

            string[] filesToConvert = Directory.GetFiles(dlg.FileName, "*.wav");
            if (!filesToConvert.Any())
            {
                MessageBox.Show("The selected folder does not contain any .wav files for converting.");
                return;
            }

            string convertedFolder = await soundPanel.RunWwiseConversion(wwisePath, dlg.FileName);
            MessageBox.Show("Done. Converted ogg files have been placed into:\n" + convertedFolder);
        }

        private void ExtractAllAudio_Clicked(object sender, RoutedEventArgs e)
        {
            var dlg = new CommonOpenFileDialog("Select output folder")
            {
                IsFolderPicker = true
            };

            if (dlg.ShowDialog() != CommonFileDialogResult.Ok)
            {
                return;
            }
            var location = dlg.FileName;

            IsBusy = true;
            BusyText = "Extracting audio";
            var itemsToExport = new List<object>(BindedItemsList);
            var exportWorker = new BackgroundWorker();
            exportWorker.DoWork += delegate
            {
                foreach (object o in itemsToExport)
                {
                    switch (o)
                    {
                        case SoundplorerExport sp when sp.Export.ClassName == "WwiseStream":
                            {
                                string outfile = System.IO.Path.Combine(location, sp.Export.ObjectName + ".wav");
                                ExportWave(sp, outfile);
                                break;
                            }
                        case SoundplorerExport sp when sp.Export.ClassName == "WwiseBank":
                            {
                                ExtractBankToWav(sp, location);
                                break;
                            }
                        case ISACTFileEntry ife:
                            {
                                string outfile = System.IO.Path.Combine(location, System.IO.Path.GetFileNameWithoutExtension(ife.Entry.FileName) + ".wav");
                                MemoryStream ms = ife.Entry.GetWaveStream();
                                File.WriteAllBytes(outfile, ms.ToArray());
                                break;
                            }
                        case AFCFileEntry afE:
                            {
                                string presetfilename = $"{System.IO.Path.GetFileNameWithoutExtension(afE.AFCPath)}_{afE.Offset}.wav";
                                ExportWaveAFC(afE, System.IO.Path.Combine(location, presetfilename));
                                break;
                            }
                    }
                }
            };
            exportWorker.RunWorkerCompleted += delegate
            {
                IsBusy = false;
                MessageBox.Show("Done.");
            };
            exportWorker.RunWorkerAsync();
        }

        private void ReplaceAudioFromWav_Clicked(object sender, RoutedEventArgs e)
        {
            if (SoundExports_ListBox.SelectedItem is SoundplorerExport spExport)
            {
                soundPanel.ReplaceAudioFromWave(forcedExport: spExport.Export);
            }
        }

        private void CloneAndReplaceFromWav_Clicked(object sender, RoutedEventArgs e)
        {
            SoundplorerExport spExport = (SoundplorerExport)SoundExports_ListBox.SelectedItem;
            if (spExport != null)
            {
                CloneAndReplace(true);
            }
        }

        private void SoundExportItem_KeyDown(object sender, KeyEventArgs e)
        {
            if (e is KeyEventArgs ke)
            {
                if (ke.Key == Key.Space)
                {
                    if (soundPanel.CanStartPlayback(null))
                    {
                        soundPanel.StartOrPausePlaying();
                    }
                    ke.Handled = true;
                }
                if (ke.Key == Key.Escape)
                {
                    soundPanel.StopPlaying();
                    ke.Handled = true;
                }
            }
        }

        private void ReverseEndiannessOfIDs_Clicked(object sender, RoutedEventArgs e)
        {
            ReverseEndianDisplayOfIDs_MenuItem.IsChecked = !ReverseEndianDisplayOfIDs_MenuItem.IsChecked;
            Properties.Settings.Default.SoundplorerReverseIDDisplayEndianness = ReverseEndianDisplayOfIDs_MenuItem.IsChecked;
            Properties.Settings.Default.Save();
        }

        private void OpenInWwiseBankEditor_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            var editor = new WwiseEditor();
            editor.LoadFile(Pcc.FileName);
            editor.Show();
        }

        private void SoundExports_ListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if ((e.OriginalSource as FrameworkElement)?.DataContext is SoundplorerExport)
            {
                soundPanel.StopPlaying();
                soundPanel.StartOrPausePlaying();
            }
        }

        private void DebugWriteBankToFileRebuild_Clicked(object sender, RoutedEventArgs e)
        {
            if (SoundExports_ListBox.SelectedItem is SoundplorerExport spExport)
            {
                if (spExport.Export.ClassName == "WwiseBank")
                {
                    WwiseBank wb = new WwiseBank(spExport.Export);
                    List<(uint, int, int)> embeddedWEMFiles = wb.GetWEMFilesMetadata();
                    byte[] data = wb.GetChunk("DATA");
                    int i = 0;
                    if (embeddedWEMFiles.Count > 0)
                    {
                        var AllWems = new List<EmbeddedWEMFile>();
                        foreach ((uint wemID, int offset, int size) singleWemMetadata in embeddedWEMFiles)
                        {
                            var wemData = new byte[singleWemMetadata.size];
                            //copy WEM data to buffer. Add 0x8 to skip DATA and DATASIZE header for this block.
                            Buffer.BlockCopy(data, singleWemMetadata.offset + 0x8, wemData, 0, singleWemMetadata.size);
                            //check for RIFF header as some don't seem to have it and are not playable.
                            string wemHeader = "" + (char)wemData[0] + (char)wemData[1] + (char)wemData[2] + (char)wemData[3];

                            string wemId = singleWemMetadata.wemID.ToString("X8");
                            string wemName = "Embedded WEM 0x" + wemId;// + "(" + singleWemMetadata.Item1 + ")";

                            EmbeddedWEMFile wem = new EmbeddedWEMFile(wemData, $"{i}: {wemName}", spExport.Export.FileRef.Game, singleWemMetadata.wemID);
                            AllWems.Add(wem);
                            i++;
                        }
                        wb.UpdateDataChunk(AllWems);
                        ExportBank(spExport);
                    }
                }
            }
        }

        private void ExtractISACTAsRaw_Clicked(object sender, RoutedEventArgs e)
        {
            if (SoundExports_ListBox.SelectedItem is ISACTFileEntry spExport)
            {
                ExportISACTEntryRaw(spExport);
            }
        }

        private void ExtractISACTAsWave_Clicked(object sender, RoutedEventArgs e)
        {

        }

        private static void ExportISACTEntryRaw(ISACTFileEntry spExport)
        {
            SaveFileDialog d = new SaveFileDialog
            {
                Filter = "ISACT Audio|*.isa",
                FileName = spExport.Entry.FileName + ".isa",
                Title = "Select save location for ISACT file"
            };

            bool? res = d.ShowDialog();
            if (res.HasValue && res.Value)
            {
                //File.WriteAllBytes(d.FileName, spExport.Export.getBinaryData());
                File.WriteAllBytes(d.FileName, spExport.Entry.DataAsStored);
                MessageBox.Show("Done.");
            }
        }
    }

    public class AFCFileEntry : NotifyPropertyChangedBase
    {
        public bool ME2;

        private string _afcpath;
        public string AFCPath
        {
            get => _afcpath;
            set => SetProperty(ref _afcpath, value);
        }

        private int _datasize;
        public int DataSize
        {
            get => _datasize;
            set => SetProperty(ref _datasize, value);
        }

        private int _offset;
        public int Offset
        {
            get => _offset;
            set => SetProperty(ref _offset, value);
        }

        private bool _needsLoading;
        public bool NeedsLoading
        {
            get => _needsLoading;
            set => SetProperty(ref _needsLoading, value);
        }

        private FontAwesomeIcon _icon;
        public FontAwesomeIcon Icon
        {
            get => _icon;
            set => SetProperty(ref _icon, value);
        }

        private string _timeString;
        public string SubText
        {
            get => _timeString;
            set => SetProperty(ref _timeString, value);
        }

        private string _displayString;
        public string DisplayString
        {
            get => _displayString;
            set => SetProperty(ref _displayString, value);
        }

        public AFCFileEntry(string afcpath, int offset, int size, bool ME2)
        {
            AFCPath = afcpath;
            DataSize = size;
            this.ME2 = ME2;
            Offset = offset;
            SubText = "Calculating length";
            Icon = FontAwesomeIcon.Spinner;
            NeedsLoading = true;
            UpdateDisplay();
        }

        private void UpdateDisplay()
        {
            DisplayString = $"AFC Entry @ 0x{Offset:X6}";
        }

        public void LoadData()
        {
            Stream waveStream = WwiseStream.CreateWaveStreamFromRaw(AFCPath, Offset, DataSize, ME2);
            if (waveStream != null)
            {
                //Check it is RIFF
                byte[] riffHeaderBytes = new byte[4];
                waveStream.Read(riffHeaderBytes, 0, 4);
                string wemHeader = "" + (char)riffHeaderBytes[0] + (char)riffHeaderBytes[1] + (char)riffHeaderBytes[2] + (char)riffHeaderBytes[3];
                if (wemHeader == "RIFF")
                {
                    waveStream.Position = 0;
                    WaveFileReader wf = new WaveFileReader(waveStream);
                    SubText = wf.TotalTime.ToString(@"mm\:ss\:fff");
                }
                else
                {
                    SubText = "Error getting length, may be unsupported";
                }
            }
            NeedsLoading = false;
            Icon = FontAwesomeIcon.VolumeUp;
        }
    }

    public class ISACTFileEntry : NotifyPropertyChangedBase
    {
        public ISBankEntry Entry { get; set; }

        private bool _needsLoading;
        public bool NeedsLoading
        {
            get => _needsLoading;
            set => SetProperty(ref _needsLoading, value);
        }

        private FontAwesomeIcon _icon;
        public FontAwesomeIcon Icon
        {
            get => _icon;
            set => SetProperty(ref _icon, value);
        }

        private string _timeString;
        public string SubText
        {
            get => _timeString;
            set => SetProperty(ref _timeString, value);
        }

        private string _displayString;
        public string DisplayString
        {
            get => _displayString;
            set => SetProperty(ref _displayString, value);
        }

        public ISACTFileEntry(ISBankEntry entry)
        {
            this.Entry = entry;
            SubText = "Calculating stream length";
            Icon = FontAwesomeIcon.Spinner;
            NeedsLoading = true;
            UpdateDisplay();
        }

        private void UpdateDisplay()
        {
            DisplayString = Entry.FileName;
        }

        public void LoadData()
        {
            if (Entry.DataAsStored != null)
            {
                //Debug.WriteLine("getting time for " + Entry.FileName + " Ogg: " + Entry.isOgg);
                TimeSpan? time = Entry.GetLength();
                if (time != null)
                {
                    //here backslash must be present to tell that parser colon is
                    //not the part of format, it just a character that we want in output
                    SubText = time.Value.ToString(@"mm\:ss\:fff");
                }
                else
                {
                    SubText = "Error getting length, may be unsupported";
                }
            }
            else
            {
                SubText = "Sound stub only";
            }
            NeedsLoading = false;
            Icon = FontAwesomeIcon.VolumeUp;
        }
    }

    public class SoundplorerExport : NotifyPropertyChangedBase
    {
        public IExportEntry Export { get; set; }

        private bool _needsLoading;
        public bool NeedsLoading
        {
            get => _needsLoading;
            set => SetProperty(ref _needsLoading, value);
        }

        private FontAwesomeIcon _icon;
        public FontAwesomeIcon Icon
        {
            get => _icon;
            set => SetProperty(ref _icon, value);
        }

        private string _timeString;
        public string SubText
        {
            get => _timeString;
            set => SetProperty(ref _timeString, value);
        }

        private string _displayString;
        public string DisplayString
        {
            get => _displayString;
            set => SetProperty(ref _displayString, value);
        }
        public SoundplorerExport(IExportEntry export)
        {
            this.Export = export;
            if (Export.ClassName == "WwiseStream")
            {
                SubText = "Calculating stream length";
            }
            else
            {
                SubText = "Calculating number of embedded WEMs";
            }
            Icon = FontAwesomeIcon.Spinner;
            NeedsLoading = true;
            UpdateDisplay();
        }

        private void UpdateDisplay()
        {
            int paddingSize = Export.FileRef.ExportCount.ToString().Length;
            DisplayString = Export.UIndex.ToString("d" + paddingSize) + ": " + Export.ObjectName;
        }

        public void LoadData()
        {
            if (Export.ClassName == "WwiseStream")
            {
                WwiseStream w = new WwiseStream(Export);
                string afcPath = w.getPathToAFC();
                if (afcPath == "")
                {
                    SubText = "Could not find AFC";
                }
                else
                {
                    TimeSpan? time = w.GetSoundLength();
                    if (time != null)
                    {
                        //here backslash must be present to tell that parser colon is
                        //not the part of format, it just a character that we want in output
                        SubText = time.Value.ToString(@"mm\:ss\:fff");
                    }
                    else
                    {
                        SubText = "Error getting length, may be unsupported";
                    }
                }
                NeedsLoading = false;
                Icon = FontAwesomeIcon.VolumeUp;
            }
            if (Export.ClassName == "WwiseBank")
            {
                WwiseBank wb = new WwiseBank(Export);
                List<(uint, int, int)> embeddedWEMFiles = wb.GetWEMFilesMetadata();
                SubText = $"{embeddedWEMFiles.Count} embedded WEM{(embeddedWEMFiles.Count != 1 ? "s" : "")}";
                NeedsLoading = false;
                Icon = FontAwesomeIcon.University;
            }

        }
    }
}
