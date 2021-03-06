﻿using ME3Explorer.Packages;
using ME3Explorer.SharedUI;
using ME3Explorer.SharedUI.Interfaces;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using static ME3Explorer.TlkManagerNS.TLKManagerWPF;

namespace ME3Explorer.TlkManagerNS
{
    /// <summary>
    /// Interaction logic for TLKManagerWPF_ExportReplaceDialog.xaml
    /// </summary>
    public partial class TLKManagerWPF_ExportReplaceDialog : NotifyPropertyChangedWindowBase, IBusyUIHost
    {
        public ICommand ReplaceSelectedTLK { get; private set; }
        public ICommand ExportSelectedTLK { get; private set; }

        public ObservableCollectionExtended<LoadedTLK> TLKSources { get; set; } = new ObservableCollectionExtended<LoadedTLK>();


        #region Busy variables
        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set => SetProperty(ref _isBusy, value);
        }

        private string _busyText;
        public string BusyText
        {
            get => _busyText;
            set => SetProperty(ref _busyText, value);
        }
        #endregion

        public TLKManagerWPF_ExportReplaceDialog(List<LoadedTLK> loadedTLKs)
        {
            TLKSources.AddRange(loadedTLKs);
            DataContext = this;
            LoadCommands();
            InitializeComponent();
        }

        private void LoadCommands()
        {
            ReplaceSelectedTLK = new RelayCommand(ReplaceTLK, TLKSelected);
            ExportSelectedTLK = new RelayCommand(ExportTLK, TLKSelected);
        }

        private void ExportTLK(object obj)
        {
            LoadedTLK tlk = TLKList.SelectedItem as LoadedTLK;
            if (tlk != null)
            {
                SaveFileDialog saveFileDialog = new SaveFileDialog
                {
                    Filter = "XML Files (*.xml)|*.xml"
                };
                if (saveFileDialog.ShowDialog() == true)
                {
                    BusyText = "Exporting TLK to XML";
                    IsBusy = true;
                    var loadingWorker = new BackgroundWorker();

                    if (tlk.exportNumber != 0)
                    {
                        //ME1
                        loadingWorker.DoWork += delegate
                        {
                            using (ME1Package pcc = MEPackageHandler.OpenME1Package(tlk.tlkPath))
                            {
                                ME1Explorer.Unreal.Classes.TalkFile talkfile = new ME1Explorer.Unreal.Classes.TalkFile(pcc, tlk.exportNumber);
                                talkfile.saveToFile(saveFileDialog.FileName);
                            }
                        };
                    }
                    else
                    {
                        //ME2,ME3
                        loadingWorker.DoWork += delegate
                        {
                            TalkFile tf = new TalkFile();
                            tf.LoadTlkData(tlk.tlkPath);
                            tf.DumpToFile(saveFileDialog.FileName);
                        };
                    }
                    loadingWorker.RunWorkerCompleted += delegate
                    {
                        IsBusy = false;
                    };
                    loadingWorker.RunWorkerAsync();
                }
            }
        }

        private void ReplaceTLK(object obj)
        {
            LoadedTLK tlk = TLKList.SelectedItem as LoadedTLK;
            if (tlk != null)
            {
                OpenFileDialog openFileDialog = new OpenFileDialog
                {
                    Multiselect = false,
                    Filter = "XML Files (*.xml)|*.xml"
                };
                if (openFileDialog.ShowDialog() == true)
                {
                    BusyText = "Converting XML to TLK";
                    IsBusy = true;
                    var replacingWork = new BackgroundWorker();

                    if (tlk.exportNumber != 0)
                    {
                        //ME1
                        replacingWork.DoWork += delegate
                        {
                            ME1Explorer.HuffmanCompression compressor = new ME1Explorer.HuffmanCompression();
                            compressor.LoadInputData(openFileDialog.FileName);
                            using (ME1Package pcc = MEPackageHandler.OpenME1Package(tlk.tlkPath))
                            {
                                compressor.replaceTlkwithFile(pcc, tlk.exportNumber - 1); //Code uses 0 based
                            };
                        };
                    }
                    else
                    {
                        //ME2,ME3

                        replacingWork.DoWork += delegate
                        {
                            HuffmanCompression hc = new HuffmanCompression();
                            hc.LoadInputData(openFileDialog.FileName, false);
                            hc.SaveToTlkFile(tlk.tlkPath);
                        };
                    }
                    replacingWork.RunWorkerCompleted += delegate
                    {
                        IsBusy = false;
                    };
                    replacingWork.RunWorkerAsync();
                }
            }
        }

        private bool TLKSelected(object obj)
        {
            return TLKList.SelectedItem != null;
        }
    }
}
