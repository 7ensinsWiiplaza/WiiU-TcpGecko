using AMS.Profile;
using GeckoApp.external;
using GeckoApp.Properties;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;

using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

namespace GeckoApp
{
    public partial class MainForm : Form
    {
        [DllImport("User32.dll", CharSet = CharSet.Auto, EntryPoint = "SendMessage")]
        protected static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("User32.dll")]
        public extern static int GetScrollInfo(IntPtr hWnd, int fnBar, ref ScrollInfo lpsi);

        [DllImport("User32.dll")]
        public extern static int SetScrollInfo(IntPtr hWnd, int fnBar, ref ScrollInfo lpsi, bool bRedraw);

        [StructLayout(LayoutKind.Sequential)]
        public struct ScrollInfo
        {
            public uint cbSize;
            public uint fMask;
            public int nMin;
            public int nMax;
            public uint nPage;
            public int nPos;
            public int nTrackPos;
        }

;

        public string SendingFilePath = string.Empty;

        private TCPGecko gecko;
        private MemSearch search;
        private MemoryViewer viewer;
        private Breakpoints bpHandler;
        private Disassembly disassembler;
        private WatchList watcher;
        private FSA fsa;

        private ExceptionHandler exceptionHandling;

        private WatchDialog addWatchDialog;
        private ValueInput watchValueInput;

        private List<UInt32> multiPokeAddr;

        private String gamename;
        private String gametitle;
        private bool GameNameStored;
        private Xml SettingsFile;

        private CodeController GCTCodeContents;

        private List<Control> WasAlreadyDisabled;
        private List<SearchComparisonInfo> searchComparisons;

        private TabPage TabLock;
        private AddressTextBox AddressContextMenuOwner;
        private HistoryTextBox HistoryContextMenuOwner;

        private NoteSheets notes;

        private GCTWizard codeWizard;

        private TextWriter BPStepLogWriter;

        private bool Connecting;
        private bool SteppingOut;
        private bool SearchingDisassembly;


        public MainForm()
        {
            InitializeComponent();
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            this.Icon = Icon.ExtractAssociatedIcon(Assembly.GetEntryAssembly().Location);

            int i;
            SettingsFile = new Xml("gecko.xml");
            SettingsFile.RootName = "gecko";

            MainControl.TabPages.Remove(GCTPage);
            MainControl.TabPages.Remove(BreakpointPage);
            MainControl.TabPages.Remove(shotPage);

            gamename = string.Empty;
            gecko = new TCPGecko(Settings.Default.hostname, 7331);
            gecko.chunkUpdate += transfer;
            hostTextBox.DataBindings.Add("Text", gecko, "Host", false, DataSourceUpdateMode.OnPropertyChanged);

            exceptionHandling = new ExceptionHandler(this);

            if(!Directory.Exists("DumpHistory"))
                Directory.CreateDirectory("DumpHistory");

            search = new MemSearch(gecko,
                                   SearchResults,
                                   PrvPage,
                                   NxtPage,
                                   ResList,
                                   UpDownSearchResultPage,
                                   exceptionHandling);

            viewer = new MemoryViewer(gecko,
                                      ValidMemory.ValidAreas[0].low,
                                      memViewGrid,
                                      memViewPAddress,
                                      memViewPValue,
                                      MemViewFPValue,
                                      exceptionHandling);

#if MONO
            disassembler = new Disassembly(gecko, "./vdappc", DisAssBox, DisScroll,
                DisRegion, AsAddress, AsText, exceptionHandling);
#else
            disassembler = new Disassembly(gecko,
                                           "vdappc.exe",
                                           DisAssBox,
                                           DisScroll,
                                           DisRegion,
                                           AsAddress,
                                           AsText,
                                           exceptionHandling);
#endif

            bpHandler = new Breakpoints(gecko,
                                        BPList,
                                        this,
                                        disassembler,
                                        BPDiss,
                                        BPClassic,
                                        BPCondList,
                                        exceptionHandling);
            foreach(String reg in BPList.longRegNames)
                BPConditionRegSelect.Items.Add(reg.Trim());
            BPConditionRegSelect.Items.Add("VoA");

            BPConditionRegSelect.SelectedIndex = 0;
            BPConditionCompare.SelectedIndex = 0;

            bpHandler.BPSkip += BPSkipped;

            watcher = new WatchList(gecko, WatchList, WatchIntervalSet, exceptionHandling);
            addWatchDialog = null;
            watchValueInput = null;

            fsa = new FSA(gecko, FSATreeView, extractFsaToolStripMenuItem, FSACodeData, exceptionHandling);

            GCTCodeContents = new CodeController(GCTCodeList, GCTCodeValues);
            GCTCodeContents.codesModified += GCTModified;

            bpHandler.BPStop += BPStopped;

            for(i = 0; i < ValidMemory.ValidAreas.Length; i++)
            {
                memRange.Items.Add(GlobalFunctions.toHex(ValidMemory.ValidAreas[i].id, 2));
                MemViewARange.Items.Add(GlobalFunctions.toHex(ValidMemory.ValidAreas[i].id, 2));
                ToolsDumpRegions.Items.Add(GlobalFunctions.toHex(ValidMemory.ValidAreas[i].id, 2));
            }

            codeWizard = new GCTWizard(GCTCodeContents);

            memRange.SelectedIndex = 0;
            MemViewARange.SelectedIndex = 0;
            MemViewShowMode.SelectedIndex = 0;
            MemViewSearchType.SelectedIndex = 0;
            ToolsDumpRegions.SelectedIndex = 0;

            comboBoxSearchDataType.SelectedIndex = 2;

            comboBoxDisplayType.SelectedIndex = 0;

            WasAlreadyDisabled = new List<Control>();
            searchComparisons = new List<SearchComparisonInfo>();
            searchComparisons.Add(new SearchComparisonInfo());
            comboBoxComparisonType.SelectedIndex = 0;
            comboBoxComparisonRHS.SelectedIndex = 0;
            buttonCancelSearch.Enabled = false;
            SteppingOut = false;
            buttonUndoSearch.Enabled = search.CanUndo();

            TabLock = null;
            BPStepLogWriter = null;

            comboBoxPokeOperation.SelectedIndex = 0;

            SetComboboxValue("Screenshots", "Format", 0, ImgFormat);
            SetComboboxValue("Screenshots", "Sizing", 0, ShotSizingType);

            int value = SettingsFile.GetValue("Screenshots", "JPEGQuality", 85);
            if(value < 0 || value > 100)
                value = 85;
            JPGQual.Value = value;

            multiPokeAddr = new List<UInt32>();

            FormStop(false);
            CTCPGecko.Enabled = true;

            codesModified = false;

            AbtText.Text = "tcpGecko dotNET Beta 0.1 by Chadderz\n" +
                "based on gecko dotNET Beta 0.63 by Link and dcx2\n\n" +
                "Special thanks to:\n\n" +
                "kenobi: for original WiiRd GUI!\n" +
                "Nuke: for the USB Gecko!\n" +
                "brkirch: for continuing Gecko OS!\n" +
                "Y.S.: for the original code handler!\n" +
                "Team Twiizers for bringing homebrew to the Wii\n" +
                "DevKitPro team: No homebrew without them!\n" +
                "Frank Wille: vdappc developer!\n" +
                "various beta testers!\n" +
                "and you!";

            notes = new NoteSheets();

            checkBoxAlwaysOnTop.Checked = Settings.Default.AlwaysOnTop;
            numericUpDownFPS.Value = Settings.Default.FPS;
            BPAddress.Text = Settings.Default.BPAddr;
            memViewAValue.Text = Settings.Default.MemViewAddr;
            BPType.SelectedIndex = Settings.Default.BPType;
            checkBoxBPNext.Checked = Settings.Default.BPNext;
            checkBoxPauseCodes.Checked = Settings.Default.PauseCodes;
            Size = Settings.Default.LastSize;
            int oldSplitter = Settings.Default.LastSplitterSize;
            MainControl.SelectedTab = BreakpointPage;
            MainControl.SelectedTab = searchPage;
            splitContainerRegASM.SplitterDistance = oldSplitter;
            toolStripTextBoxMemViewFontSize.Text = Settings.Default.MemViewFontSize.ToString();
            toolStripTextBoxMemViewFontSize_KeyDown(null, new KeyEventArgs(Keys.Enter));
            viewFloatsInHexToolStripMenuItem.Checked = Settings.Default.ViewFloatsInHex;
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            VerifyCodesAreSaved();
            watcher.StopThread();

            Connecting = false;
            Settings.Default.Save();
        }


        private void SetComboboxValue(String section, String entry, int defaultValue, ComboBox box)
        {
            int maxIndex = box.Items.Count;
            int value = SettingsFile.GetValue(section, entry, defaultValue);
            if(value < 0 || value >= maxIndex)
                value = defaultValue;
            box.SelectedIndex = value;
        }

        public void FormStop(bool enable)
        {
            MainControl.Enabled = true;

            EnableMainControls(enable);

            PGame.Enabled = enable;
            RGame.Enabled = enable;
            CTCPGecko.Enabled = enable;
            DisconnectButton.Enabled = enable;
            OpenNotePad.Enabled = enable;
            if(!enable && notes != null && notes.Visible)
                notes.Close();
        }

        public void CryError()
        {
            FormStop(false);
            gecko.Disconnect();
            StatusCap.Text = "An error occured. Please reconnect!";
            progressBar.Value = 0;
            PCent.Text = "0%";
            CTCPGecko.Enabled = true;
            ResetSearch();
        }

        private void transfer(UInt32 address,
                              UInt32 currentchunk,
                              UInt32 allchunks,
                              UInt32 transferred,
                              UInt32 length,
                              bool okay,
                              bool dump)
        {
            if(length <= 1024)
                return;
            int percent;
            if(search.blockDump)
            {
                double received = (double)(search.blocksDumpedSize + transferred);
                percent = (int)Math.Round(received * 100 / (double)search.totalBlockSize);
                if(percent < 100)
                {
                    StatusCap.Text = "Performing block dump (block: " +
                        search.blockID.ToString() +
                        "/" +
                        search.blockCount.ToString() +
                        "; range:" +
                        GlobalFunctions.toHex(search.blockStart) +
                        "-" +
                        GlobalFunctions.toHex(search.blockEnd) +
                        ")";
                } else
                    StatusCap.Text = "Transfer completed!";
            } else
            {
                percent = (int)Math.Round(((double)transferred) / ((double)length) * 100);
                if(dump && percent < 100)
                    StatusCap.Text = "Dumping data (" + address.ToString("x8") + ")";
                else if(percent < 100)
                    StatusCap.Text = "Sending data (" + address.ToString("x8") + ")";
                else
                    StatusCap.Text = "Transfer completed!";
            }
            PCent.Text = percent.ToString() + "%";
            progressBar.Value = percent;
            Application.DoEvents();
        }

        public void ResetSearch()
        {
            Search.Text = "Search";
            comboBoxComparisonRHS.Items[1] = "Unknown value";
            ResSrch.Enabled = false;
            search.Reset();
            buttonUndoSearch.Enabled = search.CanUndo();
            SearchHistoryUpdownsReset();
        }

        private String fixString(String input, int length)
        {
            String parse = input;
            if(parse.Length > length)
                parse =
                    parse.Substring(parse.Length - length, length);

            while(parse.Length < length)
                parse = "0" + parse;

            return parse;
        }

        private void MainForm_Shown(object sender, EventArgs e)
        {
        }


        public void DisconnectButton_Click(object sender, EventArgs e)
        {
            FormStop(false);
            try
            {
                gecko.Disconnect();
            } catch
            {
            }
            StatusCap.Text = "Connection has been closed!";
            progressBar.Value = 0;
            PCent.Text = "0%";
            CTCPGecko.Enabled = true;
        }

        private bool UnknownStatus()
        {
            try
            {
                WiiStatus stat = gecko.status();
                return (stat == WiiStatus.Unknown);
            } catch
            {
                return true;
            }
        }

        public void CTCPGecko_Click(object sender, EventArgs e)
        {
            if(Connecting)
            {
                Connecting = false;
                CTCPGecko.Text = "Connect to Gecko";
                return;
            }

            bool retry = true;
            bool success = false;
            int attempt = 0;

            if(gecko.connected)
            {
                StatusCap.Text = "Disconnecting!";
                try
                {
                    gecko.Disconnect();
                } catch
                {
                }
                Application.DoEvents();
                Thread.Sleep(500);
            }

            while(retry && !success)
            {
                attempt++;
                StatusCap.Text = "Connection attempt: " + attempt.ToString();
                Application.DoEvents();
                try
                {
                    if(!gecko.Connect())
                        throw new Exception();
                    int failAttempt = 0;
                    Connecting = true;
                    CTCPGecko.Text = "Cancel Connection";
                    hostTextBox.Enabled = false;
                    while(UnknownStatus())
                    {
                        gecko.sendfail();
                        failAttempt++;
                        if(failAttempt > 10 || !Connecting)
                        {
                            if(!Connecting)
                            {
                                retry = false;
                            }
                            Connecting = false;
                            throw new Exception();
                        }
                        Application.DoEvents();
                    }
                    Connecting = false;

                    if(gecko.status() == WiiStatus.Loader)
                    {
                        DialogResult dr = MessageBox.Show("No game has been loaded yet!\nGecko dotNET requires a running game!\n\nShould a game be automatically loaded!",
                                                          "Gecko dotNET",
                                                          MessageBoxButtons.YesNoCancel,
                                                          MessageBoxIcon.Warning);
                        if(dr == DialogResult.Yes)
                            gecko.Hook();
                        if(dr == DialogResult.Cancel)
                        {
                            Close();
                            return;
                        }
                        while(gecko.status() == WiiStatus.Loader)
                        {
                            StatusCap.Text = "Waiting for game!";
                            Application.DoEvents();
                            Thread.Sleep(100);
                        }
                    }

                    success = true;
                } catch
                {
                    if(attempt % 3 != 0)
                        continue;
                    retry =
                        MessageBox.Show("Connection to the TCP Gecko has failed!\n" +
                            "Do you want to retry?",
                                        "Connection issue",
                                        MessageBoxButtons.YesNo,
                                        MessageBoxIcon.Warning) ==
                        DialogResult.Yes;
                }
            }

            EnableMainControls(success);
            ResSrch.Enabled = false;
            RGame.Enabled = success;
            PGame.Enabled = success;
            OpenNotePad.Enabled = success;
            hostTextBox.Enabled = !success;

            try
            {
                if(success)
                {
                    Settings.Default.hostname = gecko.Host;
                    Settings.Default.Save();

                    CTCPGecko.Text = "Reconnect to Gecko";
                    StatusCap.Text = "Ready!";

                    UInt32 title_type, title_id, os_ver;
                    switch(os_ver = gecko.OsVersionRequest())
                    {
                        case 400:
                            title_type = gecko.peek(0x1000ecb0);
                            title_id = gecko.peek(0x1000ecb4);
                            break;

                        case 500:
                            title_type = gecko.peek(0x10013010);
                            title_id = gecko.peek(0x10013014);
                            break;

                        case 532:
                            title_type = gecko.peek(0x100136D0);
                            title_id = gecko.peek(0x100136D4);
                            break;

                        case 550:
                        case 551:
                            title_type = gecko.peek(0x10013C10);
                            title_id = gecko.peek(0x10013C14);
                            break;

                        default:
                            title_type = 0;
                            title_id = 0;
                            break;
                    }
                    String rname = title_type.ToString("X8") + "-" + title_id.ToString("X8");

                    ValidMemory.setDataUpper(gecko);

                    bool gamenameChanged = gamename != rname;
                    gamename = rname;
                    try
                    {
                        using(StreamReader reader = new StreamReader("gamelist.txt"))
                        {
                            while(!reader.EndOfStream)
                            {
                                string line = reader.ReadLine();

                                if(line.StartsWith(rname))
                                {
                                    gametitle = line.Substring(rname.Length + 1);
                                    break;
                                }
                            }
                        }
                    } catch
                    {
                        gametitle = rname;
                    }

                    this.Text = "Gecko dotNET (" + gametitle;
                    this.Text += ") Handler v";
                    this.Text += os_ver / 100;
                    this.Text += ".";
                    this.Text += (os_ver / 10) % 10;
                    this.Text += ".";
                    this.Text += os_ver % 10;

                    if(gamenameChanged)
                    {
                        GCTLoadCodes();
                    }

                    GameNameStored = false;
                    DisconnectButton.Enabled = true;

                    PopulateThreads(os_ver);
                } else
                {
                    DisconnectButton.Enabled = false;
                    CTCPGecko.Text = "Connect to Gecko";
                    StatusCap.Text = "No TCP Gecko connection available!";

                    this.Text = "Gecko dotNET";
                }
            } catch(ETCPGeckoException exc)
            {
                exceptionHandling.HandleException(exc);
            }
        }

        private List<string> ThreadNames = new List<string>();
        private List<bool> ThreadRunning = new List<bool>();
        private List<uint> ThreadAddress = new List<uint>();

        private void PopulateThreads(uint os_ver)
        {
            ThreadNames.Clear();
            ThreadRunning.Clear();
            ThreadAddress.Clear();
            ThreadGridView.Rows.Clear();
            ThreadDisplayComboBox.Items.Clear();

            uint tempThreadAddress = gecko.peek(0xffffffe0);
            uint temp;
            while((temp = gecko.peek(tempThreadAddress + 0x390)) != 0)
            {
                tempThreadAddress = temp;
            }
            while((temp = gecko.peek(tempThreadAddress + 0x38C)) != 0)
            {
                AddThread(tempThreadAddress);
                tempThreadAddress = temp;
            }
            AddThread(tempThreadAddress);
        }

        private void AddThread(uint tempThreadAddress)
        {
            MemoryStream ms = new MemoryStream();
            uint nameLocation = gecko.peek(tempThreadAddress + 0x5C0);
            string name;
            if(nameLocation == 0)
            {
                name = tempThreadAddress.ToString("X8");
            } else
            {
                gecko.Dump(nameLocation, nameLocation + 0x100, ms);
                name = new string(Encoding.ASCII.GetChars(ms.ToArray()));
                if(name.Contains("\0"))
                {
                    name = name.Remove(name.IndexOf("\0"));
                }
            }

            ThreadDisplayComboBox.Items.Add(name);

            ThreadNames.Add(name);
            ThreadAddress.Add(tempThreadAddress);
            uint threadState = gecko.peek(tempThreadAddress + 0x328);
            if(threadState > 0xFFFFFF)
            {
                ThreadRunning.Add(false);
                ThreadGridView.Rows.Add(name, "Paused", tempThreadAddress.ToString("X8"));
            } else
            {
                ThreadRunning.Add(true);
                ThreadGridView.Rows.Add(name, "Running", tempThreadAddress.ToString("X8"));
            }
        }

        private void PGame_Click(object sender, EventArgs e)
        {
            try
            {
                WiiStatus status = gecko.status();
                bpHandler.ClearLogIndent();

                if(checkBoxFPS.Checked || !checkBoxBPNext.Checked)
                {
                    gecko.Resume();
                    Thread.Sleep(1);
                    gecko.Pause();
                    Thread.Sleep(100);

                    if(MainControl.SelectedTab == MemView)
                    {
                        viewer.Update();
                    }
                } else
                {
                    uint BPAddress;
                    if(!addressTextBoxBPNext.IsValidGet(out BPAddress))
                    {
                        BPAddress = 0x800018A8;
                    }
                    if(bpHandler.SetBreakpoint(BPAddress, BreakpointType.Execute, true))
                    {
                        bpHandler.BreakpointNext = true;
                        BPMode(true);
                    }
                }

                PGame.Text = "Next frame";

                if(checkBoxBPNext.Checked)
                {
                    if(bpHandler.BreakpointNext)
                    {
                        RGame.Text = "Cancel";
                    }
                }
            } catch(ETCPGeckoException exc)
            {
                exceptionHandling.HandleException(exc);
            }
        }

        private void RGame_Click(object sender, EventArgs e)
        {
            PGame.Text = "Pause game";
            RGame.Text = "Run game";

            try
            {
                if(bpHandler.BreakpointNext)
                {
                    BPCancel_Click(sender, e);
                }
                gecko.Resume();
            } catch(ETCPGeckoException exc)
            {
                exceptionHandling.HandleException(exc);
            }
        }

        private void OpenNotePad_Click(object sender, EventArgs e)
        {
            notes.Show(gamename);
        }


        private void memRange_SelectedIndexChanged(object sender, EventArgs e)
        {
            int id = memRange.SelectedIndex;
            memStart.Text =
                Convert.ToString(ValidMemory.ValidAreas[id].low, 16).ToUpper();
            memEnd.Text =
                Convert.ToString(ValidMemory.ValidAreas[id].high, 16).ToUpper();
        }

        private void comboBoxComparisonRHS_SelectedIndexChanged(object sender, EventArgs e)
        {
            bool enable = comboBoxComparisonRHS.SelectedIndex == 0 || comboBoxComparisonRHS.SelectedIndex == 3;
            textBoxComparisonValue.Enabled = enable;
            string comboBoxText = (string)comboBoxComparisonRHS.Items[1];
            if(comboBoxText.Equals("Unknown Value") && comboBoxComparisonRHS.SelectedIndex == 1)
            {
                comboBoxComparisonType.Enabled = false;
            } else
            {
                comboBoxComparisonType.Enabled = true;
            }

            if(!enable)
            {
                if(comboBoxComparisonType.Items.Count <= 6)
                {
                    comboBoxComparisonType.Items.Add("Different by");
                    comboBoxComparisonType.Items.Add("Different by less than");
                    comboBoxComparisonType.Items.Add("Different by more than");
                }

                if(comboBoxComparisonType.SelectedIndex >= 6 && comboBoxComparisonType.Enabled)
                {
                    textBoxComparisonValue.Enabled = true;
                }
            }
            if(enable)
            {
                if(comboBoxComparisonType.SelectedIndex >= 6)
                    comboBoxComparisonType.SelectedIndex = 0;
                while(comboBoxComparisonType.Items.Count > 6)
                    comboBoxComparisonType.Items.RemoveAt(6);
            }
            searchComparisons[SearchGroupIndex].searchType = GetCmpRHS();
        }

        private void ValueLength_SelectedIndexChanged(object sender, EventArgs e)
        {
            int length;
            switch(comboBoxSearchDataType.SelectedIndex)
            {
                case 0:
                    length = 2;
                    break;
                case 1:
                    length = 4;
                    break;
                default:
                    length = 8;
                    break;
            }
            textBoxComparisonValue.MaxLength = length;
            textBoxComparisonValue.Text = fixString(textBoxComparisonValue.Text, length);
        }

        private void cmpType_SelectedIndexChanged(object sender, EventArgs e)
        {
            bool enable = comboBoxComparisonType.SelectedIndex >= 6;
            bool enable2 = comboBoxComparisonRHS.SelectedIndex == 0 || comboBoxComparisonRHS.SelectedIndex == 3;
            textBoxComparisonValue.Enabled = enable || enable2;
            if(enable)
            {
            }
            searchComparisons[SearchGroupIndex].comparisonType = GetCmpType();
        }

        private void Search_Click(object sender, EventArgs e)
        {
            SearchSize size;
            ComparisonType cType;
            bool useDifference;

            switch(comboBoxSearchDataType.SelectedIndex)
            {
                case 0:
                    size = SearchSize.Bit8;
                    break;
                case 1:
                    size = SearchSize.Bit16;
                    break;
                case 2:
                    size = SearchSize.Bit32;
                    break;
                case 3:
                    size = SearchSize.Single;
                    break;
                default:
                    size = SearchSize.Bit32;
                    break;
            }

            switch(comboBoxComparisonRHS.SelectedIndex)
            {
                case 3:
                    break;
                case 2:
                    break;
                case 1:
                    break;
                default:
                    break;
            }

            cType = GetCmpType();

            useDifference = (cType == ComparisonType.DifferentBy ||
                cType == ComparisonType.DifferentByLess ||
                cType == ComparisonType.DifferentByMore);

            UInt32 lAddress = 0;
            UInt32 hAddress = 0;
            UInt32 lValue = 0;

            if(!GlobalFunctions.tryToHex(memStart.Text, out lAddress))
            {
                MessageBox.Show("Start address invalid!");
                return;
            }

            if(!GlobalFunctions.tryToHex(memEnd.Text, out hAddress))
            {
                MessageBox.Show("End address invalid!");
                return;
            }

            if(lAddress > hAddress)
            {
                MessageBox.Show("Start and End addresses backwards!");
                return;
            }

            if(!GlobalFunctions.tryToHex(textBoxComparisonValue.Text, out lValue) && textBoxComparisonValue.Enabled)
            {
                MessageBox.Show("Search value invalid!");
                return;
            }

            if(useDifference)
            {
            }

            if(!ValidMemory.validRange(lAddress, hAddress))
            {
                MessageBox.Show("Memory range invalid!");
                return;
            }

            try
            {
                FormStop(false);
                TabLock = searchPage;
                buttonCancelSearch.Enabled = true;
                buttonCancelSearch.BringToFront();
                bool WasRunning = (gecko.status() == WiiStatus.Running);
                gecko.SafePause();
                bool success = search.SearchRefactored(lAddress, hAddress, searchComparisons, size);

                if(WasRunning)
                {
                    gecko.SafeResume();
                }

                FormStop(true);
                buttonCancelSearch.Enabled = false;
                buttonCancelSearch.SendToBack();
                buttonUndoSearch.Enabled = search.CanUndo();
                TabLock = null;

                if(success)
                {
                    Search.Text = "Refine";
                    ResSrch.Enabled = true;
                    search.SaveSearchToIndex(search.DumpNum);
                    SearchHistoryUpdownsInc();
                } else
                {
                    ResetSearch();
                }
            } catch(Exception ex)
            {
                Logger.WriteException(ex);
                CryError();
            }
        }

        private SearchType GetCmpRHS()
        {
            SearchType sType;
            switch(comboBoxComparisonRHS.SelectedIndex)
            {
                case 1:
                    sType = SearchType.Unknown;
                    break;
                case 2:
                    sType = SearchType.Old;
                    break;
                case 3:
                    sType = SearchType.Diff;
                    break;
                default:
                    sType = SearchType.Exact;
                    break;
            }

            return sType;
        }

        private void SetCmpRHS(SearchType sType)
        {
            switch(sType)
            {
                case SearchType.Unknown:
                    comboBoxComparisonRHS.SelectedIndex = 1;
                    break;
                case SearchType.Old:
                    comboBoxComparisonRHS.SelectedIndex = 2;
                    break;
                case SearchType.Diff:
                    comboBoxComparisonRHS.SelectedIndex = 3;
                    break;
                default:
                    comboBoxComparisonRHS.SelectedIndex = 0;
                    break;
            }
        }

        private ComparisonType GetCmpType()
        {
            ComparisonType cType;
            switch(comboBoxComparisonType.SelectedIndex)
            {
                case 1:
                    cType = ComparisonType.NotEqual;
                    break;
                case 2:
                    cType = ComparisonType.Lower;
                    break;
                case 3:
                    cType = ComparisonType.LowerEqual;
                    break;
                case 4:
                    cType = ComparisonType.Greater;
                    break;
                case 5:
                    cType = ComparisonType.GreaterEqual;
                    break;
                case 6:
                    cType = ComparisonType.DifferentBy;
                    break;
                case 7:
                    cType = ComparisonType.DifferentByLess;
                    break;
                case 8:
                    cType = ComparisonType.DifferentByMore;
                    break;
                default:
                    cType = ComparisonType.Equal;
                    break;
            }

            return cType;
        }

        private void SetCmpType(ComparisonType cType)
        {
            switch(cType)
            {
                case ComparisonType.NotEqual:
                    comboBoxComparisonType.SelectedIndex = 1;
                    break;
                case ComparisonType.Lower:
                    comboBoxComparisonType.SelectedIndex = 2;
                    break;
                case ComparisonType.LowerEqual:
                    comboBoxComparisonType.SelectedIndex = 3;
                    break;
                case ComparisonType.Greater:
                    comboBoxComparisonType.SelectedIndex = 4;
                    break;
                case ComparisonType.GreaterEqual:
                    comboBoxComparisonType.SelectedIndex = 5;
                    break;
                case ComparisonType.DifferentBy:
                    comboBoxComparisonType.SelectedIndex = 6;
                    break;
                case ComparisonType.DifferentByLess:
                    comboBoxComparisonType.SelectedIndex = 7;
                    break;
                case ComparisonType.DifferentByMore:
                    comboBoxComparisonType.SelectedIndex = 8;
                    break;
                default:
                    comboBoxComparisonType.SelectedIndex = 0;
                    break;
            }
        }

        private void UpdateValueTypeDropDown()
        {
            if(numericUpDownNewSearchIndex.Value == 0)
            {
                comboBoxComparisonRHS.Items[1] = "Unknown value";
            } else
            {
                comboBoxComparisonRHS.Items[1] = "New column (" + numericUpDownNewSearchIndex.Value + ")";
            }

            if(numericUpDownOldSearchIndex.Value == 0)
            {
                while(comboBoxComparisonRHS.Items.Count > 2) comboBoxComparisonRHS.Items.RemoveAt(2);
            } else
            {
                String oldCol = "Old column (" + numericUpDownOldSearchIndex.Value + ")";

                if(comboBoxComparisonRHS.Items.Count < 3)
                {
                    comboBoxComparisonRHS.Items.Add(oldCol);
                } else
                {
                    comboBoxComparisonRHS.Items[2] = oldCol;
                }
            }
        }

        private void SearchHistoryUpdownsInc()
        {
            numericUpDownOldSearchIndex.ValueChanged -= numericUpDownOldSearchIndex_ValueChanged;
            numericUpDownNewSearchIndex.ValueChanged -= numericUpDownNewSearchIndex_ValueChanged;

            numericUpDownOldSearchIndex.Value = numericUpDownNewSearchIndex.Value;
            numericUpDownNewSearchIndex.Value = Convert.ToDecimal(search.DumpNum);

            numericUpDownOldSearchIndex.ValueChanged += numericUpDownOldSearchIndex_ValueChanged;
            numericUpDownNewSearchIndex.ValueChanged += numericUpDownNewSearchIndex_ValueChanged;

            UpdateValueTypeDropDown();
        }

        private void SearchHistoryUpdownsReset()
        {
            numericUpDownOldSearchIndex.Value = 0;
            numericUpDownNewSearchIndex.Value = 0;
            UpdateValueTypeDropDown();
        }

        private void PkAddress_Click(object sender, EventArgs e)
        {
            if(SearchResults.SelectedRows.Count == 0)
                return;
            if(SearchResults.SelectedRows.Count == 1)
            {
                StringResult item = search.GetResult(SearchResults.SelectedRows[0].Index);
                PAddress.Text = item.SAddress;
                if(item.SOldValue != String.Empty)
                {
                    PValue.Text = item.SOldValue;
                } else
                {
                    PValue.Text = item.SValue;
                }
            } else
            {
                multiPokeAddr.Clear();
                PAddress.ClearHistory();
                UInt32 address;
                StringResult item = search.GetResult(SearchResults.SelectedRows[0].Index);
                for(int i = 0; i < SearchResults.SelectedRows.Count; i++)
                {
                    address = search.GetAddress(SearchResults.SelectedRows[i].Index);
                    multiPokeAddr.Add(address);
                    PAddress.AddAddressToHistory(address);
                }
                PAddress.Text = "MP";
                PValue.Text = item.SValue;
            }
        }

        private void makeCode_Click(object sender, EventArgs e)
        {
            if(SearchResults.SelectedRows.Count == 0)
                return;
            List<UInt32> addresses = new List<UInt32>();
            UInt32 address;
            int i;
            for(i = 0; i < SearchResults.SelectedRows.Count; i++)
            {
                address = search.GetAddress(SearchResults.SelectedRows[i].Index);
                addresses.Add(address);
            }

            addresses.Sort();

            CodeContent nCode = new CodeContent();
            UInt32 cAddressR = 0x80000000;
            UInt32 rAddressR;
            UInt32 offset;
            bool firstLine = false;
            UInt32 add;
            switch(search.searchSize)
            {
                case SearchSize.Bit8:
                    add = 0;
                    break;

                case SearchSize.Bit16:
                    add = 0x02000000;
                    break;

                default:
                    add = 0x04000000;
                    break;
            }

            int nCodeId = GCTCodeContents.Count;
            String name;
            if(!InputBox.Show("Code name", "Insert code name", "New code", out name))
            {
                name = "New code " + (nCodeId + 1).ToString();
            }
            for(i = 0; i < addresses.Count; i++)
            {
                rAddressR = addresses[i] & 0xFE000000;
                if(firstLine && cAddressR != rAddressR && cAddressR != 0x80000000)
                    nCode.addLine(0xE0000000, 0x80008000);
                if(cAddressR != rAddressR)
                    if(rAddressR != 0x80000000)
                        nCode.addLine(0x42000000, rAddressR);
                cAddressR = rAddressR;

                offset = addresses[i] + add - rAddressR;
                nCode.addLine(offset, search.GetNewValueFromAddress(addresses[i]));

                firstLine = true;
            }
            if(cAddressR != 0x80000000)
                nCode.addLine(0xE0000000, 0x80008000);
            GCTCodeContents.AddCode(nCode, name);

            GCTCodeList.Items[nCodeId].Selected = true;
            MainControl.SelectedTab = GCTPage;
        }

        private void PButton_Click(object sender, EventArgs e)
        {
            Byte tag = Byte.Parse(((Button)sender).Tag.ToString());

            TextBox aBox, vBox;
            bool allowMulti;
            switch(tag)
            {
                case 1:
                    aBox = memViewPAddress;
                    vBox = memViewPValue;
                    allowMulti = false;
                    break;

                default:
                    aBox = PAddress;
                    vBox = PValue;
                    allowMulti = true;
                    break;
            }

            UInt32 addr = 0;
            UInt32 value;
            UInt16 Val16;
            Byte Val8;
            UInt32 Val32;
            String AText = aBox.Text;
            String VText = vBox.Text;

            bool multipoke = false;

            if(AText != "MP")
            {
                if(!GlobalFunctions.tryToHex(AText, out addr))
                {
                    MessageBox.Show("Invalid address");
                    return;
                }
                multipoke = false;
            } else if(allowMulti)
            {
                if(PAddress.GetHistoryCount() == 0)
                {
                    MessageBox.Show("No multipoke data availible!");
                    return;
                }
                multipoke = true;
            } else
            {
                MessageBox.Show("Multipoke not usable in this poke box!");
                return;
            }

            if(!multipoke && !ValidMemory.validAddress(addr))
            {
                MessageBox.Show("Address is not within valid memory!");
                return;
            }

            if(!GlobalFunctions.tryToHex(VText, out value))
            {
                MessageBox.Show("Invalid address");
                return;
            }

            uint currentValue;
            if(!multipoke && VText.Length > 4 && tag == 1)
            {
                currentValue = gecko.peek(addr);

                switch(comboBoxPokeOperation.SelectedIndex)
                {
                    case 7:
                        value = currentValue / value;
                        break;

                    case 6:
                        value = currentValue * value;
                        break;

                    case 5:
                        value = currentValue - value;
                        break;

                    case 4:
                        value = currentValue + value;
                        break;

                    case 3:
                        value = currentValue ^ value;
                        break;

                    case 2:
                        value = currentValue & value;
                        break;

                    case 1:
                        value = currentValue | value;
                        break;

                    case 0:
                    default:
                        currentValue = value;
                        break;
                }
            }

            try
            {
                int MultiPokeCount = PAddress.GetHistoryCount();
                if(VText.Length > 4)
                {
                    Val32 = value;
                    if(!multipoke)
                    {
                        addr = addr & 0xFFFFFFFC;
                        aBox.Text = Convert.ToString(addr, 16);
                    }
                    if(!multipoke)
                        gecko.poke32(addr, Val32);
                    else
                        for(int i = 0; i < MultiPokeCount; i++)
                            gecko.poke32(PAddress.GetHistoryuint(i), Val32);
                } else if(VText.Length > 2)
                {
                    Val16 = (UInt16)value;
                    if(!multipoke)
                    {
                        addr = addr & 0xFFFFFFFE;
                        aBox.Text = Convert.ToString(addr, 16);
                    }
                    if(!multipoke)
                        gecko.poke16(addr, Val16);
                    else
                        for(int i = 0; i < MultiPokeCount; i++)
                            gecko.poke16(PAddress.GetHistoryuint(i), Val16);
                } else
                {
                    Val8 = (Byte)value;
                    if(!multipoke)
                        gecko.poke08(addr, Val8);
                    else
                        for(int i = 0; i < MultiPokeCount; i++)
                            gecko.poke08(PAddress.GetHistoryuint(i), Val8);
                }

                if(tag == 1)
                {
                    Thread.Sleep(100);
                    viewer.Update(true);
                }
            } catch(ETCPGeckoException exc)
            {
                exceptionHandling.HandleException(exc);
            }
        }

        private void ResSrch_Click(object sender, EventArgs e)
        {
            if(MessageBox.Show("Are you sure you want to start a new search?",
                               "Attention",
                               MessageBoxButtons.YesNo,
                               MessageBoxIcon.Warning) !=
                DialogResult.Yes)
                return;
            ResetSearch();
        }

        private void PAddress_KeyPress(object sender, KeyPressEventArgs e)
        {
            if((Byte)e.KeyChar == 13)
            {
                PValue.Focus();
                e.Handled = true;
            }
        }

        private void PValue_KeyPress(object sender, KeyPressEventArgs e)
        {
            if((Byte)e.KeyChar == 13)
            {
                PkAddress_Click(sender, e);
                e.Handled = true;
            }
        }

        private void BpSAddress_Click(object sender, EventArgs e)
        {
            if(SearchResults.SelectedRows.Count != 1)
                return;

            StringResult foundCode = search.GetResult(SearchResults.SelectedRows[0].Index);
            BPAddress.Text = foundCode.SAddress;

            if(BPType.SelectedIndex == 3)
            {
                BPType.SelectedIndex = 2;
            }

            MainControl.SelectedTab = BreakpointPage;
        }

        private void ShowInDiss_Click(object sender, EventArgs e)
        {
            if(SearchResults.SelectedRows.Count != 1)
                return;

            UInt32 address = search.GetAddress(SearchResults.SelectedRows[0].Index);
            disassembler.DissToBox(address);
            MainControl.SelectedTab = DisPage;
        }

        private void ShowInMemView_Click(object sender, EventArgs e)
        {
            if(SearchResults.SelectedRows.Count != 1)
                return;
            UInt32 mAddress = search.GetAddress(SearchResults.SelectedRows[0].Index);
            CenteredMemViewSelection(sender, e, mAddress);
        }

        private void CenteredMemView(object sender, EventArgs e, UInt32 mAddress)
        {
            int oldSelectedRow = memViewGrid.CurrentCell.RowIndex;
            int oldSelectedCol = memViewGrid.CurrentCell.ColumnIndex;

            CenteredMemViewSelection(sender, e, mAddress);

            memViewGrid.CurrentCell = memViewGrid[oldSelectedCol, oldSelectedRow];
        }

        private void CenteredMemViewSelection(object sender, EventArgs e, UInt32 mAddress)
        {
            if(!ValidMemory.validAddress(mAddress)) return;

            UInt32 tAddress = (mAddress & 0xFFFFFFF0) - 0x70;
            tAddress = Math.Max(tAddress, ValidMemory.ValidAreas[ValidMemory.rangeCheckId(mAddress)].low);
            tAddress = Math.Min(tAddress, ValidMemory.ValidAreas[ValidMemory.rangeCheckId(mAddress)].high - 0x100);
            UInt32 offset = mAddress - tAddress;

            MemViewARange.SelectedIndexChanged -= MemViewARange_SelectedIndexChanged;
            MemViewARange.SelectedIndex = ValidMemory.rangeCheckId(mAddress);
            MemViewARange.SelectedIndexChanged += MemViewARange_SelectedIndexChanged;

            memViewAValue.Text = GlobalFunctions.toHex(mAddress);

            MainControl.SelectedTab = MemView;
            if(memViewGrid.Rows.Count == 0)
            {
                viewer.Update();
            }

            memViewGrid.Rows[(int)offset / 0x10].Cells[((int)mAddress & 0xF) / 4 + 1].Selected = true;

            tAddress &= 0xFFFFFFFC;
            viewer.address = tAddress;
            viewer.Update();
        }

        private void showInWatchList_Click(object sender, EventArgs e)
        {
            List<UInt32> addresses = new List<UInt32>();
            UInt32 address;
            int i;
            for(i = 0; i < SearchResults.SelectedRows.Count; i++)
            {
                address = search.GetAddress(SearchResults.SelectedRows[i].Index);
                addresses.Add(address);
            }

            addresses.Sort();

            int valLength = PValue.MaxLength;
            WatchDataSize ws;
            switch(valLength)
            {
                case 1:
                    ws = WatchDataSize.Bit8;
                    break;
                case 2:
                    ws = WatchDataSize.Bit16;
                    break;
                default:
                    ws = WatchDataSize.Bit32;
                    break;
            }

            foreach(UInt32 watchadd in addresses)
            {
                watcher.AddWatch(GlobalFunctions.toHex(watchadd), new UInt32[] { watchadd }, ws);
            }

            MainControl.SelectedTab = WatchTab;
        }


        private void tabPage2_Enter(object sender, EventArgs e)
        {
            UInt32 Address;
            if(gecko.connected)
            {
                if(memViewAValue.IsValidGet(out Address))
                {
                    CenteredMemViewSelection(sender, e, Address);
                }
                toolStripTextBoxMemViewFontSize_KeyDown(null, new KeyEventArgs(Keys.Enter));
            }
        }

        private void MemViewARange_SelectedIndexChanged(object sender, EventArgs e)
        {
            UInt32 oldValue, oldRange;
            double percent = 0;
            if(memViewAValue.IsValidGet(out oldValue))
            {
                int index = ValidMemory.rangeCheckId(oldValue);
                oldRange = ValidMemory.ValidAreas[index].high - ValidMemory.ValidAreas[index].low;
                percent = (oldValue - ValidMemory.ValidAreas[index].low) / (double)(oldRange);
            }
            UInt32 newRange = ValidMemory.ValidAreas[MemViewARange.SelectedIndex].high -
                ValidMemory.ValidAreas[MemViewARange.SelectedIndex].low;
            UInt32 sAddress = ValidMemory.ValidAreas[MemViewARange.SelectedIndex].low + (uint)(percent * newRange);
            memViewAValue.Text = GlobalFunctions.toHex(sAddress);
            viewer.address = sAddress;
            if(MainControl.SelectedTab == MemView)
                CenteredMemViewSelection(sender, e, sAddress);
        }

        private void MemViewUpdate_Click(object sender, EventArgs e)
        {
            UInt32 vAddress;
            if(memViewAValue.IsValidGet(out vAddress))
            {
                CenteredMemViewSelection(sender, e, vAddress);
            }
        }

        private void MemViewShowMode_SelectedIndexChanged(object sender, EventArgs e)
        {
            MemoryViewMode vMode;
            switch(MemViewShowMode.SelectedIndex)
            {
                case 0:
                    vMode = MemoryViewMode.Hex;
                    break;

                case 1:
                    vMode = MemoryViewMode.ASCII;
                    break;

                case 2:
                    vMode = MemoryViewMode.ANSI;
                    break;

                case 3:
                    vMode = MemoryViewMode.Unicode;
                    break;

                case 4:
                    vMode = MemoryViewMode.Single;
                    break;

                case 5:
                    vMode = MemoryViewMode.AutoZero;
                    break;

                case 6:
                    vMode = MemoryViewMode.AutoDot;
                    break;

                default:
                    vMode = MemoryViewMode.AutoDot;
                    break;
            }
            viewer.viewMode = vMode;
            if(MainControl.SelectedTab == MemView)
                viewer.Update();
        }

        private void MemViewAutoUp_Click(object sender, EventArgs e)
        {
            if(MemViewAutoUp.Checked)
            {
                DateTime start = DateTime.Now;
                DateTime now;
                TimeSpan sub;
                int msec, odps;
                double dps;
                int dumpcount = 0;
                while(MemViewAutoUp.Checked)
                {
                    UInt32 addr = viewer.selectedAddress;
                    viewer.Update(true);
                    dumpcount++;
                    now = DateTime.Now;
                    sub = now - start;
                    if(sub.Seconds >= 1)
                    {
                        msec = (sub.Seconds * 1000 + sub.Milliseconds);
                        dps = (double)dumpcount * 1000.0 / (double)msec;
                        odps = (int)Math.Round(dps);
                        MemViewAutoUp.Text = "Auto update (" + odps.ToString() + " dps)";
                        start = DateTime.Now;
                        dumpcount = 0;
                    }
                    Thread.Sleep(10);
                    Application.DoEvents();
                }
                MemViewAutoUp.Text = "Auto update";
            } else
            {
                MemViewAutoUp.Text = "Auto update";
            }
        }

        private void MemViewScrollbar_ValueChanged(object sender, EventArgs e)
        {
            UInt32 vAddress;
            if(!GlobalFunctions.tryToHex(memViewAValue.Text, out vAddress))
            {
                MessageBox.Show("Invalid input");
                return;
            }
            if(!ValidMemory.validAddress(vAddress))
            {
                MessageBox.Show("Invalid address");
                return;
            }
            vAddress &= 0xFFFFFFFC;
            if(MemViewScrollbar.Value == 0)
                vAddress += 0x100;
            else if(MemViewScrollbar.Value == 2)
                vAddress -= 0x100;
            else
                return;

            CenteredMemViewSelection(sender, e, vAddress);

            MemViewScrollbar.ValueChanged -= MemViewScrollbar_ValueChanged;
            MemViewScrollbar.Value = 1;
            MemViewScrollbar.ValueChanged += MemViewScrollbar_ValueChanged;
        }

        private void goBackToolStripMenuItem_Click(object sender, EventArgs e)
        {
            viewer.GoBack();
            CenteredMemViewSelection(sender, e, (viewer.address & 0xfffffff0) + 0x70);
        }

        private void goForwardToolStripMenuItem_Click(object sender, EventArgs e)
        {
            viewer.GoForward();
            CenteredMemViewSelection(sender, e, (viewer.address & 0xfffffff0) + 0x70);
        }

        private void memViewSetBP_Click(object sender, EventArgs e)
        {
            uint byteOffset;
            GlobalFunctions.tryToHex(address.HeaderText.Trim(), out byteOffset);
            BPAddress.Text = GlobalFunctions.toHex(viewer.selectedAddress + (byteOffset & 0x3));

            if(BPType.SelectedIndex == 3)
            {
                BPType.SelectedIndex = 2;
            }
            MainControl.SelectedTab = BreakpointPage;
        }

        private void memViewAddToWatch_Click(object sender, EventArgs e)
        {
            UInt32 vAdd = viewer.selectedAddress;
            watcher.AddWatch(GlobalFunctions.toHex(vAdd), new UInt32[] { vAdd }, WatchDataSize.Bit32);
            MainControl.SelectedTab = WatchTab;
        }

        private void memViewAddGCTCode_Click(object sender, EventArgs e)
        {
            try
            {
                UInt32 vAdd = viewer.selectedAddress;
                UInt32 cRegion = vAdd & 0xFE000000;
                UInt32 value = gecko.peek(vAdd);
                vAdd = vAdd - cRegion + 0x04000000;
                int nCodeId = GCTCodeContents.Count;

                String name;
                if(!InputBox.Show("Code name", "Insert code name", "New code", out name))
                {
                    name = "New code " + (nCodeId + 1).ToString();
                }
                CodeContent nCode = new CodeContent();
                bool addlines = false;
                if(cRegion != 0x80000000)
                {
                    addlines = true;
                    nCode.addLine(0x42000000, cRegion);
                }
                nCode.addLine(vAdd, value);
                if(addlines)
                    nCode.addLine(0xE0000000, 0x80008000);

                GCTCodeContents.AddCode(nCode, name);
                MainControl.SelectedTab = GCTPage;
            } catch(ETCPGeckoException exc)
            {
                exceptionHandling.HandleException(exc);
            }
        }

        private void memViewUpload_Click(object sender, EventArgs e)
        {
            if(openBinary.ShowDialog() == DialogResult.OK)
            {
                UInt32 vAdd = viewer.selectedAddress;
                FileStream fs = new FileStream(openBinary.FileName, FileMode.Open, FileAccess.Read);
                fs.Position = 0;
                UInt32 endAdd = vAdd + (UInt32)fs.Length;
                if(!ValidMemory.validAddress(endAdd))
                {
                    MessageBox.Show("File too large to be uploaded to this address!");
                    fs.Close();
                    return;
                }
                try
                {
                    gecko.Upload(vAdd, endAdd, fs);
                } catch(ETCPGeckoException exc)
                {
                    exceptionHandling.HandleException(exc);
                }
                fs.Close();
            }
        }

        private void MemViewSearchPerfom_Click(object sender, EventArgs e)
        {
            if(viewer.Searching)
            {
                viewer.Searching = false;
                return;
            }

            String sString = MemViewSearchString.Text;
            bool hex = MemViewSearchType.SelectedIndex == 4;
            bool caseSensitive = (MemViewSearchType.SelectedIndex % 2 == 1) || hex;
            bool unicode = (MemViewSearchType.SelectedIndex >= 2);

            byte[] stringBytes;
            if(unicode)
            {
                stringBytes = Encoding.Unicode.GetBytes(sString);
            } else
            {
                stringBytes = Encoding.ASCII.GetBytes(sString);
            }

            if(hex)
            {
                sString = Regex.Replace(sString.ToUpper(), "[^0-9A-F]", String.Empty);
                if(!GlobalFunctions.tryToHex(sString, out stringBytes))
                {
                    return;
                }
            }

            viewer.Searching = true;
            MemViewSearchPerfom.Text = "Cancel";
            viewer.SearchString(stringBytes, caseSensitive, unicode, hex);
            viewer.Searching = false;
            MemViewSearchPerfom.Text = "Search";
            CenteredMemViewSelection(sender, e, viewer.address);
        }


        private void BPMode(bool mode)
        {
            bool enable = !mode;

            MemViewAutoUp.Checked = false;

            PGame.Enabled = enable;
            CTCPGecko.Enabled = enable;

            EnableMainControls(enable);

            BPCancel.Enabled = !enable;

            if(!enable)
            {
                TabLock = MainControl.SelectedTab;
            } else
            {
                TabLock = null;
            }

            if(enable && bpHandler.BreakpointNext)
            {
                RGame.Text = "Run game";
                bpHandler.BreakpointNext = false;

                if(MainControl.SelectedTab == MemView)
                {
                    viewer.Update();
                }
            }

            if(enable)
            {
                UpdateShowMemColor();

                UpdateBPCondValue();

                if(checkBoxLogSteps.Checked)
                {
                    BPStepLogWriter.WriteLine();
                    BPStepLogWriter.WriteLine(bpHandler.GetStepLog());
                    BPStepLogWriter.Flush();
                }
            }
        }

        private void UpdateShowMemColor()
        {
            switch(bpHandler.BranchState)
            {
                case ConditionalBranchState.Taken:
                    buttonShowMem.ForeColor = Color.LightGreen;
                    buttonShowMem.Text = "Taken";
                    break;

                case ConditionalBranchState.NotTaken:
                    buttonShowMem.ForeColor = Color.Red;
                    buttonShowMem.Text = "Not Taken";
                    break;

                default:
                    buttonShowMem.ForeColor = Color.Black;
                    buttonShowMem.Text = "Show Mem";
                    break;
            }
        }

        private void BPStopped(bool hit)
        {
            BPMode(false);
        }

        private void BPFire_Click(object sender, EventArgs e)
        {
            BreakpointType bptp;

            BPSkipCount.Text = "0";

            switch(BPType.SelectedIndex)
            {
                case 0:
                    bptp = BreakpointType.Read;
                    break;

                case 1:
                    bptp = BreakpointType.Write;
                    break;

                case 2:
                    bptp = BreakpointType.ReadWrite;
                    break;

                default:
                    bptp = BreakpointType.Execute;
                    break;
            }

            bool exact = BPExact.Checked;

            UInt32 bAddress;
            if(!GlobalFunctions.tryToHex(BPAddress.Text, out bAddress))
            {
                MessageBox.Show("Invalid input");
                return;
            }
            if(!ValidMemory.validAddress(bAddress))
            {
                MessageBox.Show("Invalid address");
                return;
            }

            bpHandler.ClearLogIndent();

            if(bpHandler.SetBreakpoint(bAddress, bptp, exact))
                BPMode(true);
        }

        private void BPCancel_Click(object sender, EventArgs e)
        {
            bpHandler.CancelBreakpoint();
            PGame.Text = "Pause Game";
        }

        private void BPStepButton_Click(object sender, EventArgs e)
        {
            try
            {
                if(gecko.status() == WiiStatus.Breakpoint)
                {
                    gecko.Step();
                    Thread.Sleep(100);
                    bpHandler.GetRegisters();
                    UpdateShowMemColor();
                    UpdateBPCondValue();

                    if(checkBoxLogSteps.Checked)
                    {
                        BPStepLogWriter.WriteLine(bpHandler.GetStepLog());

                        BPStepLogWriter.Flush();
                    }
                }
            } catch(ETCPGeckoException exc)
            {
                exceptionHandling.HandleException(exc);
            }
        }

        private void BPStepOverButton_Click(object sender, EventArgs e)
        {
            try
            {
                if(gecko.status() == WiiStatus.Breakpoint)
                {
                    if(bpHandler.stepOver)
                    {
                        if(bpHandler.SetBreakpoint(bpHandler.hitAddress + 4, BreakpointType.Execute, true))
                        {
                            BPMode(true);
                        }
                        bpHandler.DecIndent();
                    } else
                    {
                        BPStepButton_Click(sender, e);
                    }
                }
            } catch(ETCPGeckoException exc)
            {
                exceptionHandling.HandleException(exc);
            }
        }

        private void BPSkipped(int skipCount)
        {
            BPSkipCount.Text = skipCount.ToString();
        }

        private void BPConditionAdd_Click(object sender, EventArgs e)
        {
            UInt32 value;
            if(!GlobalFunctions.tryToHex(BPCondValue.Text, out value))
            {
                MessageBox.Show("Invalid value!");
                return;
            }
            int register = BPConditionRegSelect.SelectedIndex;
            if(register < 0)
            {
                MessageBox.Show("Invalid register!");
                return;
            }
            BreakpointComparison condition;
            switch(BPConditionCompare.SelectedIndex)
            {
                case 0:
                    condition = BreakpointComparison.Equal;
                    break;
                case 1:
                    condition = BreakpointComparison.NotEqual;
                    break;
                case 2:
                    condition = BreakpointComparison.GreaterEqual;
                    break;
                case 3:
                    condition = BreakpointComparison.Greater;
                    break;
                case 4:
                    condition = BreakpointComparison.LowerEqual;
                    break;
                default:
                    condition = BreakpointComparison.Lower;
                    break;
            }

            BreakpointCondition cond;

            int index = BPCondList.SelectedIndex;

            if(index > -1)
            {
                cond = new BreakpointCondition(register,
                                               value,
                                               condition,
                                               bpHandler.conditions.GetIndexedConditionGroup(index));
                bpHandler.conditions.Insert(index, cond);
            } else
            {
                cond = new BreakpointCondition(register, value, condition, 1);
                bpHandler.conditions.Add(cond);
            }

            BPCondList.SelectedIndex = index;
        }

        private void BPCondDel_Click(object sender, EventArgs e)
        {
            List<int> indices = new List<int>();
            for(int i = BPCondList.SelectedItems.Count - 1; i >= 0; i--)
            {
                indices.Add(BPCondList.SelectedIndices[i]);
            }
            BPCondList.ClearSelected();
            foreach(int index in indices)
            {
                bpHandler.conditions.Delete(index);
            }
        }

        private void BPCondClear_Click(object sender, EventArgs e)
        {
            BPCondList.ClearSelected();
            bpHandler.conditions.Clear();
        }


        private void DisPage_Enter(object sender, EventArgs e)
        {
            if(gecko.connected)
            {
                disassembler.DissToBox();
            }
        }

        private void DisUpDown_ValueChanged(object sender, EventArgs e)
        {
            if(DisUpDown.Value == 1)
                return;
            if(DisUpDown.Value == 2)
                disassembler.Decrease();
            if(DisUpDown.Value == 0)
                disassembler.Increase();

            DisUpDown.Value = 1;
        }

        private void DisUpdateBtn_Click(object sender, EventArgs e)
        {
            UInt32 vAddress;
            if(!GlobalFunctions.tryToHex(DisRegion.Text, out vAddress))
            {
                MessageBox.Show("Invalid input");
                return;
            }
            if(!ValidMemory.validAddress(vAddress))
            {
                MessageBox.Show("Invalid address");
                return;
            }

            vAddress &= 0xFFFFFFFC;
            disassembler.DissToBox(vAddress);
        }

        private void DisRegion_KeyPress(object sender, KeyPressEventArgs e)
        {
            if((Byte)e.KeyChar == 13)
            {
                DisUpdateBtn_Click(sender, e);
                e.Handled = true;
            }
        }

        private void Assemble_Click(object sender, EventArgs e)
        {
            String assembly = AsText.Text;
            if(assembly == string.Empty)
            {
                MessageBox.Show("No assembly given");
                return;
            }

            string potentialAddress = String.Empty;
            if(assembly.Length > 8)
            {
                potentialAddress = assembly.Substring(0, 8);
            }

            uint address;

            if(GlobalFunctions.tryToHex(potentialAddress, out address))
            {
                assembly = assembly.Substring(8);
                AsText.Text = assembly;
                AsAddress.Text = GlobalFunctions.toHex((long)address);
            }

            UInt32 vAddress;
            if(!GlobalFunctions.tryToHex(AsAddress.Text, out vAddress))
            {
                MessageBox.Show("Invalid input");
                return;
            }
            if(!ValidMemory.validAddress(vAddress))
            {
                MessageBox.Show("Invalid address");
                return;
            }
            vAddress &= 0xFFFFFFFC;

            string oldLine = disassembler.Disassemble(vAddress, 1)[0];

            oldLine = Regex.Replace(oldLine, ":[^\t]*\t", " ");

            oldLine = Regex.Replace(oldLine, "\t", " ");

            AsText.AddTextToHistory(oldLine);

            disassembler.Assemble(vAddress, assembly);
        }

        private void DisAssSetBP_Click(object sender, EventArgs e)
        {
            BPAddress.Text = GlobalFunctions.toHex(disassembler.disAddress);
            BPType.SelectedIndex = 3;
            MainControl.SelectedTab = BreakpointPage;
        }

        private void disAssContextMenu_Opening(object sender, CancelEventArgs e)
        {
            if(!ValidMemory.validAddress(disassembler.disAddress))
            {
                e.Cancel = true;
            }
        }

        private void DisAssPoke_Click(object sender, EventArgs e)
        {
            PAddress.Text = GlobalFunctions.toHex(disassembler.disAddress);
            try
            {
                PValue.Text = GlobalFunctions.toHex(gecko.peek(disassembler.disAddress));
            } catch(ETCPGeckoException exc)
            {
                exceptionHandling.HandleException(exc);
            }
            MainControl.SelectedTab = searchPage;
        }

        private void disAssGCTCode_Click(object sender, EventArgs e)
        {
            try
            {
                UInt32 address = disassembler.disAddress;
                UInt32 value = gecko.peek(address);
                CodeContent nCode = new CodeContent();
                UInt32 memReg = address & 0xFE000000;
                bool addDelimiters = false;
                if(memReg != 0x80000000)
                    addDelimiters = true;
                if(addDelimiters)
                    nCode.addLine(0x42000000, memReg);
                address = address - memReg + 0x04000000;
                nCode.addLine(address, value);
                if(addDelimiters)
                    nCode.addLine(0xE0000000, 0x80008000);
                int nCodeId = GCTCodeContents.Count;
                String name;
                if(!InputBox.Show("Code name", "Insert code name", "New code", out name))
                {
                    name = "New code " + (nCodeId + 1).ToString();
                }
                GCTCodeContents.AddCode(nCode, name);

                GCTCodeList.Items[nCodeId].Selected = true;
                MainControl.SelectedTab = GCTPage;
            } catch(ETCPGeckoException exc)
            {
                exceptionHandling.HandleException(exc);
            }
        }


        private void shotPage_Enter(object sender, EventArgs e)
        {
            if(!GameNameStored)
            {
                ShotFilename.Text = gamename;
                GameNameStored = true;
            }
        }

        private ScreenshotSizingMode getSizingMode()
        {
            switch(ShotSizingType.SelectedIndex)
            {
                case 1:
                    return ScreenshotSizingMode.StretchToWidescreen;

                case 2:
                    return ScreenshotSizingMode.StretchToFullscreen;

                default:
                    return ScreenshotSizingMode.None;
            }
        }

        private bool createPreview(bool unpause, out Image shot)
        {
            Image screenshot;
            try
            {
                if(gecko.status() != WiiStatus.Running)
                    unpause = false;
                else
                    gecko.Pause();

                ScreenshotSizingMode sizing = getSizingMode();
                screenshot = gecko.Screenshot();

                if(sizing != ScreenshotSizingMode.None)
                    screenshot = Screenshots.resizeImage(screenshot, sizing);

                ScreenshotCapBox.Image = screenshot;

                if(unpause)
                    gecko.Resume();

                shot = screenshot;
                return true;
            } catch(ETCPGeckoException exc)
            {
                screenshot = new Bitmap(256, 256);
                shot = screenshot;
                exceptionHandling.HandleException(exc);
                return false;
            }
        }

        private void ShotGetFormat(out ScreenshotFormat format, out String extension)
        {
            switch(ImgFormat.SelectedIndex)
            {
                case 1:
                    format = ScreenshotFormat.BMP;
                    extension = ".bmp";
                    break;

                case 2:
                    format = ScreenshotFormat.JPEG;
                    extension = ".jpg";
                    SettingsFile.SetValue("Screenshots", "JPEGQuality", JPGQual.Value);
                    break;

                case 3:
                    format = ScreenshotFormat.TIFF;
                    extension = ".tif";
                    break;

                default:
                    format = ScreenshotFormat.PNG;
                    extension = ".png";
                    break;
            }
        }

        private void ShotGetFormat(out ScreenshotFormat format)
        {
            String ext;
            ShotGetFormat(out format, out ext);
        }

        private void ShotCapture_Click(object sender, EventArgs e)
        {
            Image screenshot;
            bool okay = createPreview(false, out screenshot);

            if(okay)
            {
                String fileName = ShotFilename.Text;
                if(fileName == string.Empty)
                    fileName = gamename;

                char delim = Path.DirectorySeparatorChar;

                if(!Directory.Exists("shots"))
                    Directory.CreateDirectory("shots");
                if(!Directory.Exists("shots" + delim + gamename))
                    Directory.CreateDirectory("shots" + delim + gamename);

                String fNameAppend = string.Empty;
                int i = 1;
                String finalFile = string.Empty;
                do
                {
                    fNameAppend = "-" + String.Format("{0:000}", i);
                    finalFile = "shots" + delim + gamename + delim + fileName + fNameAppend;
                    i++;
                } while (
                    File.Exists(finalFile + ".jpg") || File.Exists(finalFile + ".jpeg") ||
                    File.Exists(finalFile + ".tif") ||
                    File.Exists(finalFile + ".tiff") ||
                    File.Exists(finalFile + ".png") ||
                    File.Exists(finalFile + ".bmp"));

                ScreenshotFormat request;
                String extension;
                ShotGetFormat(out request, out extension);

                ImageCodecInfo codec = Screenshots.getImageCodec(request);
                EncoderParameters parameters = Screenshots.getParameters(JPGQual.Value, request);

                screenshot.Save(finalFile + extension, codec, parameters);
                try
                {
                    gecko.Resume();
                } catch(ETCPGeckoException exc)
                {
                    exceptionHandling.HandleException(exc);
                }
            }
        }

        private void ShotPreview_Click(object sender, EventArgs e)
        {
            Image bla;
            createPreview(true, out bla);
        }

        private void JPGQual_Scroll(object sender, EventArgs e)
        {
            JPGQualLabel.Text = JPGQual.Value.ToString() + "%";
        }

        private void ShotSizingType_SelectedIndexChanged(object sender, EventArgs e)
        {
            SettingsFile.SetValue("Screenshots", "Sizing", ShotSizingType.SelectedIndex);
        }

        private void ImgFormat_SelectedIndexChanged(object sender, EventArgs e)
        {
            SettingsFile.SetValue("Screenshots", "Format", ImgFormat.SelectedIndex);
            ScreenshotFormat format;
            ShotGetFormat(out format);
            JPGQual.Enabled = format == ScreenshotFormat.JPEG;
        }


        private bool codesModified;

        private bool VerifyCodesAreSaved()
        {
            if(codesModified)
            {
                if(MessageBox.Show("The GCT list has changed.\n" +
                        "Do you want to store the code list?",
                                   "Gecko dotNET",
                                   MessageBoxButtons.YesNo,
                                   MessageBoxIcon.Warning) ==
                    DialogResult.Yes)
                {
                    GCTSaveCodes(false);
                    codesModified = false;
                }
            }

            return !codesModified;
        }

        private void GCTAddCode_Click(object sender, EventArgs e)
        {
            String codeName;

            if(InputBox.Show("Code name", "Insert code name", "New code", out codeName))
            {
                GCTCodeContents.AddCode(codeName);
                codesModified = true;
            }
        }

        private void GCTDelBtn_Click(object sender, EventArgs e)
        {
            if(GCTCodeList.SelectedIndices.Count > 0)
            {
                if(MessageBox.Show("Are you sure you want to delete the selected code?",
                                   "Gecko dotNET",
                                   MessageBoxButtons.YesNo,
                                   MessageBoxIcon.Warning) ==
                    DialogResult.Yes)
                {
                    GCTCodeContents.Remove(GCTCodeList.SelectedIndices[0]);
                    codesModified = true;
                }
            }
        }

        private void GCTStoreImm_Click(object sender, EventArgs e)
        {
            GCTCodeContents.UpdateCode();
        }

        private void GCTSndButton_Click(object sender, EventArgs e)
        {
            if(!GCTCodeContents.UpdateCode()) return;

            MemoryStream memStream = new MemoryStream();
            GCTCodeContents.GenerateCheatStream(memStream);

            try
            {
                if(checkBoxPauseCodes.Checked)
                {
                    PGame_Click(sender, e);
                    while(TabLock != null) Application.DoEvents();
                }

                gecko.sendCheats(memStream);

                if(checkBoxPauseCodes.Checked)
                {
                    RGame_Click(sender, e);
                }

                memStream.Close();

                MessageBox.Show("Cheats sent!");
            } catch(ETCPGeckoException exc)
            {
                memStream.Close();
                exceptionHandling.HandleException(exc);
            }
        }

        private void GCTPage_Enter(object sender, EventArgs e)
        {
            GCTListFileName.Text = "Code list: " + gamename + ".wgc";
        }

        private void GCTSaveList_Click(object sender, EventArgs e)
        {
            GCTSaveCodes(true);
        }

        private void GCTSaveCodes(bool inform)
        {
            char delim = Path.DirectorySeparatorChar;
            if(!Directory.Exists("codes"))
                Directory.CreateDirectory("codes");

            String storeName = "codes" + delim + gamename + ".wgc";
            if(File.Exists(storeName))
                File.Delete(storeName);

            GCTCodeContents.toWGCFile(storeName);

            if(inform)
                MessageBox.Show("Codes stored!");
        }

        private void GCTLoadCodes()
        {
            if(codesModified)
            {
                if(!VerifyCodesAreSaved())
                {
                    if(MessageBox.Show("The GCT list has changed.\n" +
                            "If you continue, changes will be lost.\n" +
                            "Are you sure?",
                                       "Gecko dotNET",
                                       MessageBoxButtons.YesNo,
                                       MessageBoxIcon.Warning) ==
                        DialogResult.No)
                    {
                        return;
                    }
                }
            }

            char delim = Path.DirectorySeparatorChar;
            GCTCodeContents.Clear();

            String storeName = "codes" + delim + gamename + ".wgc";
            if(File.Exists(storeName))
                GCTCodeContents.fromWGCFile(storeName);
            codesModified = false;
        }

        private void GCTLoadList_Click(object sender, EventArgs e)
        {
            GCTLoadCodes();
        }

        private void GCTDisable_Click(object sender, EventArgs e)
        {
            MemoryStream memStream = new MemoryStream();
            GlobalFunctions.WriteStream(memStream, 0x00D0C0DE);
            GlobalFunctions.WriteStream(memStream, 0x00D0C0DE);
            GlobalFunctions.WriteStream(memStream, 0xF0000000);
            GlobalFunctions.WriteStream(memStream, 0x00000000);
            try
            {
                gecko.sendCheats(memStream);
                memStream.Close();
            } catch(ETCPGeckoException exc)
            {
                exceptionHandling.HandleException(exc);
            }
        }

        private void GCTModified()
        {
            codesModified = true;
        }


        private void WatchAdd_Click(object sender, EventArgs e)
        {
            watcher.SuspendThread();
            if(addWatchDialog == null)
                addWatchDialog = new WatchDialog();
            if(addWatchDialog.AddCodeDialog())
            {
                watcher.AddWatch(addWatchDialog.WName,
                                 addWatchDialog.WAddress,
                                 addWatchDialog.WDataSize);
            }
            watcher.ResumeThread();
        }

        private void WatchEditCM_Click(object sender, EventArgs e)
        {
            WatchEntry entry;
            watcher.GetSelected(out entry);
            if(entry != null)
            {
                if(addWatchDialog == null)
                    addWatchDialog = new WatchDialog();
                watcher.SuspendThread();
                if(addWatchDialog.EditWatchDialog(entry))
                {
                    watcher.UpdateEntry(addWatchDialog.WName,
                                        addWatchDialog.WAddress,
                                        addWatchDialog.WDataSize);
                }
                watcher.ResumeThread();
            }
        }

        private void WatchCM_Opening(object sender, CancelEventArgs e)
        {
            bool enable = WatchList.SelectedRows.Count > 0;
            WatchEditCM.Enabled = enable;
            WatchDeleteCM.Enabled = enable;
            WatchPokeCM.Enabled = enable;
        }

        private void WatchAddWatchCM_Click(object sender, EventArgs e)
        {
            WatchAdd_Click(sender, e);
        }

        private void WatchDeleteCM_Click(object sender, EventArgs e)
        {
            watcher.DeleteSelected();
        }

        private void WatchPokeCM_Click(object sender, EventArgs e)
        {
            WatchEntry entry;
            if(watcher.GetSelected(out entry))
            {
                if(watchValueInput == null)
                    watchValueInput = new ValueInput();
                int maxLength;
                switch(entry.dataSize)
                {
                    case WatchDataSize.Bit8:
                        maxLength = 2;
                        break;

                    case WatchDataSize.Bit16:
                        maxLength = 4;
                        break;

                    default:
                        maxLength = 8;
                        break;
                }
                UInt32 pValue = entry.lastValue;
                try
                {
                    watcher.SuspendThread();
                    if(watchValueInput.ShowDialog(entry.updatedAddress, ref pValue, maxLength))
                    {
                        switch(maxLength)
                        {
                            case 2:
                                gecko.poke08(entry.updatedAddress, (Byte)pValue);
                                break;

                            case 4:
                                gecko.poke16(entry.updatedAddress, (UInt16)pValue);
                                break;

                            default:
                                gecko.poke32(entry.updatedAddress, pValue);
                                break;
                        }
                    }
                    watcher.ResumeThread();
                } catch(ETCPGeckoException exc)
                {
                    exceptionHandling.HandleException(exc);
                }
            } else
                MessageBox.Show("Address not availible!");
        }

        private void WatchList_CellContentDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            WatchPokeCM_Click(sender, e);
        }

        private void WatchListSaveButton_Click(object sender, EventArgs e)
        {
            if(!watcher.hasContent)
            {
                MessageBox.Show("The watch list is empty!");
                return;
            }
            if(WatchListSave.FileName == string.Empty)
                WatchListSave.FileName = gamename + ".xwl";
            if(WatchListSave.ShowDialog() == DialogResult.OK)
            {
                watcher.SaveToFile(WatchListSave.FileName);
            }
        }

        private void WatchListClear_Click(object sender, EventArgs e)
        {
            if(watcher.hasContent)
            {
                if(MessageBox.Show("Are you sure?",
                                   "Gecko dotNet",
                                   MessageBoxButtons.YesNo,
                                   MessageBoxIcon.Warning) ==
                    DialogResult.Yes)
                {
                    watcher.Clear();
                }
            }
        }

        private void WatchListOpenButton_Click(object sender, EventArgs e)
        {
            bool merge = false;
            bool abort = false;
            if(watcher.hasContent)
            {
                switch(MessageBox.Show("There is already a watch list in use.\r\n" +
                    "Do you want to merge the loaded list with the exsiting (Yes),\r\n" +
                    "drop the existing (No) or cancel loading a new list (Cancel)?",
                                       "Gecko dotNet",
                                       MessageBoxButtons.YesNoCancel,
                                       MessageBoxIcon.Warning))
                {
                    case DialogResult.Yes:
                        merge = true;
                        break;

                    case DialogResult.No:
                        merge = false;
                        break;

                    default:
                        abort = true;
                        break;
                }
            }

            if(abort)
                return;

            if(WatchListOpen.ShowDialog() == DialogResult.OK)
            {
                watcher.LoadFromFile(WatchListOpen.FileName, merge);
            }
        }


        private void FSARead_Click(object sender, EventArgs e)
        {
            fsa.DumpTree();
        }


        private void ToolsDisableProtection_CheckedChanged(object sender, EventArgs e)
        {
            if(ToolsDisableProtection.Checked)
            {
                ToolsDisableWatchProtection.Enabled = true;
                ValidMemory.addressDebug = true;
            } else
            {
                ValidMemory.addressDebug = false;
                watcher.addressDebug = false;
                ToolsDisableWatchProtection.Checked = false;
                ToolsDisableWatchProtection.Enabled = false;
            }
        }

        private void ToolsDisableWatchProtection_CheckedChanged(object sender, EventArgs e)
        {
            watcher.addressDebug =
                (ToolsDisableWatchProtection.Checked);
        }

        private void ToolsDumpRegions_SelectedIndexChanged(object sender, EventArgs e)
        {
            int id = ToolsDumpRegions.SelectedIndex;
            ToolsDumpStart.Text =
                GlobalFunctions.toHex(ValidMemory.ValidAreas[id].low);
            ToolsDumpEnd.Text =
                GlobalFunctions.toHex(ValidMemory.ValidAreas[id].high);
            if(ToolsDumpFileName.Text == string.Empty ||
                (ToolsDumpFileName.Text.Length == 10 &&
                    ToolsDumpFileName.Text.Substring(0, 4).ToUpper() == "DUMP"))
                ToolsDumpFileName.Text = "DUMP" + ToolsDumpRegions.Items[id] + ".BIN";
        }

        private void ToolsBrowseDump_Click(object sender, EventArgs e)
        {
            if(ToolsDumpSave.ShowDialog() == DialogResult.OK)
            {
                ToolsDumpFileName.Text = ToolsDumpSave.FileName;
            }
        }

        private void ToolsDump_Click(object sender, EventArgs e)
        {
            UInt32 lowAddress, highAddress;

            if(!GlobalFunctions.tryToHex(ToolsDumpStart.Text, out lowAddress))
            {
                MessageBox.Show("Start address cannot be parsed.");
                return;
            }

            if(!GlobalFunctions.tryToHex(ToolsDumpEnd.Text, out highAddress))
            {
                MessageBox.Show("End address cannot be parsed.");
                return;
            }

            if(!ValidMemory.validRange(lowAddress, highAddress))
            {
                MessageBox.Show("Invalid memory range!");
                return;
            }

            String fileName = ToolsDumpFileName.Text;
            if(fileName == string.Empty)
            {
                MessageBox.Show("No file name given!");
                return;
            }

            try
            {
                FormStop(false);
                if(File.Exists(fileName))
                    File.Delete(fileName);
                FileStream newFile = new FileStream(fileName, FileMode.Create);
                gecko.Dump(lowAddress, highAddress, newFile);
                newFile.Close();
                FormStop(true);
            } catch(ETCPGeckoException exc)
            {
                exceptionHandling.HandleException(exc);
            }
        }


        private TextBox selectedInputBox;

        private void lowerValue_MouseClick(object sender, MouseEventArgs e)
        {
            if(e.Button == MouseButtons.Right)
            {
                selectedInputBox = (TextBox)sender;
                if(selectedInputBox.MaxLength < 8)
                {
                    CvFloatHex.Enabled = false;
                    cvHexFloat.Enabled = false;
                } else
                {
                    CvFloatHex.Enabled = true;
                    cvHexFloat.Enabled = true;
                }
                return;
            }
        }

        private void CvDecHexClick(object sender, EventArgs e)
        {
            int length = 8;

            if(selectedInputBox == textBoxComparisonValue || selectedInputBox == PValue)
                switch(comboBoxSearchDataType.SelectedIndex)
                {
                    case 0:
                        length = 2;
                        break;
                    case 1:
                        length = 4;
                        break;
                }
            UInt32 intV;
            if(UInt32.TryParse(selectedInputBox.Text, out intV))
            {
                String hexV = GlobalFunctions.toHex(intV, length);
                selectedInputBox.Text = hexV;
            } else
            {
                MessageBox.Show("Invalid input value");
            }
        }

        private void CvHexDec_Click(object sender, EventArgs e)
        {
            UInt32 intval;
            if(GlobalFunctions.tryToHex(selectedInputBox.Text, out intval))
            {
                selectedInputBox.Text = intval.ToString();
            } else
            {
                MessageBox.Show("Invalid input value");
            }
        }

        private void InputCvCopy_Click(object sender, EventArgs e)
        {
            Clipboard.SetText(selectedInputBox.SelectedText);
        }

        private void InputCvCut_Click(object sender, EventArgs e)
        {
            Clipboard.SetText(selectedInputBox.SelectedText);
            int oldpos = selectedInputBox.SelectionStart;
            selectedInputBox.Text = selectedInputBox.Text
                .Remove(selectedInputBox.SelectionStart, selectedInputBox.SelectionLength);
            selectedInputBox.SelectionStart = oldpos;
        }

        private void InputCvPaste_Click(object sender, EventArgs e)
        {
            if(!Clipboard.ContainsText())
                return;
            int clength = Clipboard.GetText().Length;
            int oldpos = selectedInputBox.SelectionStart;
            int olength = selectedInputBox.Text.Length;
            String selText = selectedInputBox.Text
                .Remove(selectedInputBox.SelectionStart, selectedInputBox.SelectionLength);
            clength = Math.Min(selectedInputBox.MaxLength - olength, clength);
            selText = selectedInputBox.Text
                .Insert(selectedInputBox.SelectionStart, Clipboard.GetText().Substring(0, clength));
            fixString(selText, selectedInputBox.MaxLength);
            selectedInputBox.Text = selText;
            selectedInputBox.SelectionStart = oldpos + clength;
        }

        private void InputCvUndo_Click(object sender, EventArgs e)
        {
            selectedInputBox.Undo();
        }

        private void InputCvSelectAll_Click(object sender, EventArgs e)
        {
            selectedInputBox.SelectionStart = 0;
            selectedInputBox.SelectionLength = selectedInputBox.Text.Length;
        }

        private void CvFloatHex_Click(object sender, EventArgs e)
        {
            String inputText = selectedInputBox.Text;
            float value;
            if(float.TryParse(inputText, out value))
            {
                UInt32 uval = GlobalFunctions.SingleToUInt(value);
                selectedInputBox.Text = GlobalFunctions.toHex(uval);
            } else
                MessageBox.Show("Invalid input. Please make sure to input floating point values in the manner specified in your operating system.\n" +
                    "This means, if your language uses a comma as a decimal seperator, please use that one for input and not a point!");
        }

        private void cvHexFloat_Click(object sender, EventArgs e)
        {
            String inputText = selectedInputBox.Text;
            UInt32 value;
            if(GlobalFunctions.tryToHex(inputText, out value))
            {
                Single sval = GlobalFunctions.UIntToSingle(value);
                selectedInputBox.Text = sval.ToString("G8");
            } else
                MessageBox.Show("Invalid input.");
        }


        private void checkBoxAutoPreview_Click(object sender, EventArgs e)
        {
            if(checkBoxAutoPreview.Checked)
            {
                DateTime start = DateTime.Now;
                DateTime now;
                TimeSpan sub;
                int msec, odps;
                double dps;
                int previewcount = 0;
                while(checkBoxAutoPreview.Checked)
                {
                    ShotPreview_Click(null, null);
                    previewcount++;
                    now = DateTime.Now;
                    sub = now - start;
                    if(sub.Seconds >= 1)
                    {
                        msec = (sub.Seconds * 1000 + sub.Milliseconds);
                        dps = (double)previewcount * 1000.0 / (double)msec;
                        odps = (int)Math.Round(dps);
                        start = DateTime.Now;
                        previewcount = 0;
                    }
                    Application.DoEvents();
                }
            }
        }

        private void MainControl_Selecting(object sender, TabControlCancelEventArgs e)
        {
            if(TabLock != null)
            {
                MainControl.SelectedTab = TabLock;
            }
        }

        private void memViewGrid_CellMouseDoubleClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            UInt32 bAddress;
            if(GlobalFunctions.tryToHex(memViewGrid.SelectedCells[0].Value.ToString(), out bAddress) &&
                ValidMemory.validAddress(bAddress))
            {
                CenteredMemViewSelection(sender, e, bAddress);
            }
        }

        private void memViewGrid_CellMouseClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            if(e.Button == MouseButtons.XButton1)
            {
                goBackToolStripMenuItem_Click(sender, e);
            }

            if(e.Button == MouseButtons.XButton2)
            {
                goForwardToolStripMenuItem_Click(sender, e);
            }
        }

        private void memViewGrid_KeyDown(object sender, KeyEventArgs e)
        {
            UInt32 bAddress = ValidMemory.ValidAreas[MemViewARange.SelectedIndex].low +
                (uint)vScrollBarMemViewGrid.Value;
            uint smallChange = (uint)vScrollBarMemViewGrid.SmallChange;
            uint largeChange = (uint)vScrollBarMemViewGrid.LargeChange;

            int currentRow = memViewGrid.SelectedCells[0].RowIndex, currentCol = memViewGrid.SelectedCells[0].ColumnIndex;

            if(e.KeyCode == Keys.Up)
            {
                if((viewer.address & 0xFFFFFFF0) == (viewer.selectedAddress & 0xFFFFFFF0))
                {
                    bAddress -= smallChange;
                } else if(e.Shift)
                {
                    bAddress -= smallChange;
                    e.Handled = true;
                } else
                {
                    MemView.Update();
                }
            }

            if(e.KeyCode == Keys.Down)
            {
                if(((viewer.address + 0xF0) & 0xFFFFFFF0) == (viewer.selectedAddress & 0xFFFFFFF0))
                {
                    bAddress += smallChange;
                } else if(e.Shift)
                {
                    bAddress += smallChange;
                    e.Handled = true;
                } else
                {
                    MemView.Update();
                }
            }

            if(e.KeyCode == Keys.PageUp)
            {
                bAddress -= largeChange;
                e.Handled = true;
            }

            if(e.KeyCode == Keys.PageDown)
            {
                bAddress += largeChange;
                e.Handled = true;
            }

            if(bAddress != viewer.address && ValidMemory.validAddress(bAddress))
            {
                CenteredMemView(sender, e, bAddress);
            }

            if(e.KeyCode == Keys.Enter)
            {
                e.Handled = true;
                UInt32 destinationAddress;
                if(GlobalFunctions.tryToHex(memViewGrid.SelectedCells[0].Value.ToString(), out destinationAddress) &&
                    ValidMemory.validAddress(destinationAddress))
                {
                    CenteredMemViewSelection(sender, e, destinationAddress);
                }
            }

            if(e.Control && e.KeyCode == Keys.C)
            {
                if(!e.Shift)
                {
                    Clipboard.SetText(memViewGrid.SelectedCells[0].Value.ToString());
                } else
                {
                    Clipboard.SetText(MemoryViewerContentsAsString());
                }
            }

            if(e.KeyCode == Keys.Back || e.KeyCode == Keys.BrowserBack || e.KeyCode == Keys.MediaPreviousTrack)
            {
                e.Handled = true;

                goBackToolStripMenuItem_Click(sender, e);
            }

            if(e.KeyCode == Keys.BrowserForward || e.KeyCode == Keys.MediaNextTrack)
            {
                e.Handled = true;

                goForwardToolStripMenuItem_Click(sender, e);
            }
        }

        private String MemoryViewerContentsAsString()
        {
            String returnResult = String.Empty;

            foreach(DataGridViewRow gridRow in memViewGrid.Rows)
            {
                foreach(DataGridViewCell rowCell in gridRow.Cells)
                {
                    if(rowCell == memViewGrid.CurrentCell)
                    {
                        returnResult += "*" + rowCell.Value.ToString() + "*";
                    } else
                    {
                        returnResult += rowCell.Value.ToString();
                    }
                    if(rowCell != gridRow.Cells[gridRow.Cells.Count - 1])
                    {
                        returnResult += "\t";
                    }
                }
                returnResult += "\r\n";
            }

            return returnResult;
        }

        private void memViewGrid_CellMouseDown(object sender, DataGridViewCellMouseEventArgs e)
        {
            int row = e.RowIndex, col = e.ColumnIndex;
            if(col >= 0 && row >= 0 && e.Button == MouseButtons.Right)
            {
                memViewGrid.CurrentCell = memViewGrid[col, row];
            }
        }

        private void checkBoxAlwaysOnTop_CheckedChanged(object sender, EventArgs e)
        {
            TopMost = checkBoxAlwaysOnTop.Checked;
            codeWizard.TopMost = checkBoxAlwaysOnTop.Checked;
            Settings.Default.AlwaysOnTop = TopMost;
            Settings.Default.Save();
        }

        private bool wasRunning;

        private void checkBoxFPS_CheckedChanged(object sender, EventArgs e)
        {
            if(checkBoxFPS.Checked)
            {
                WiiStatus status = gecko.status();
                wasRunning = status == WiiStatus.Running;
            }

            timerFPS.Enabled = checkBoxFPS.Checked;

            if(!checkBoxFPS.Checked && wasRunning)
            {
                RGame_Click(sender, e);
            }
        }

        private void timerFPS_Tick(object sender, EventArgs e)
        {
            PGame_Click(sender, e);
        }

        private void numericUpDownFPS_ValueChanged(object sender, EventArgs e)
        {
            timerFPS.Interval = (int)(1000 / Convert.ToDouble(numericUpDownFPS.Value));
            Settings.Default.FPS = numericUpDownFPS.Value;
            Settings.Default.Save();
        }

        private void DisRegion_TextChanged(object sender, EventArgs e)
        {
            Settings.Default.DisAsmAddr = DisRegion.Text;
            Settings.Default.Save();
        }

        private void BPAddress_TextChanged(object sender, EventArgs e)
        {
            Settings.Default.BPAddr = BPAddress.Text;
            Settings.Default.Save();
        }

        private void memViewAValue_TextChanged(object sender, EventArgs e)
        {
            Settings.Default.MemViewAddr = memViewAValue.Text;
            Settings.Default.Save();

            uint address;
            if(GlobalFunctions.tryToHex(memViewAValue.Text, out address) && ValidMemory.validAddress(address))
            {
                int rangeID = ValidMemory.rangeCheckId(address);
                uint range = ValidMemory.ValidAreas[rangeID].high;
                range -= ValidMemory.ValidAreas[rangeID].low;
                vScrollBarMemViewGrid.Maximum = (int)range;

                uint offset = address - ValidMemory.ValidAreas[rangeID].low;
                vScrollBarMemViewGrid.Value = (int)offset;
            }
        }

        private void BPType_SelectedIndexChanged(object sender, EventArgs e)
        {
            Settings.Default.BPType = BPType.SelectedIndex;
            Settings.Default.Save();
        }

        private void SearchResults_Sorted(object sender, EventArgs e)
        {
            search.SortResults();
        }

        private void SearchResults_UserDeletingRow(object sender, DataGridViewRowCancelEventArgs e)
        {
            e.Cancel = true;

            if(SearchResults.SelectedRows[0] == e.Row)
            {
                search.DeleteResults(SearchResults.SelectedRows);
            }
        }

        private void buttonLoadSearch_Click(object sender, EventArgs e)
        {
            DialogResult LoadFileResult = openFileDialogSearch.ShowDialog();
            bool LoadSearchResult = false;
            if(LoadFileResult == DialogResult.OK)
            {
                LoadSearchResult = search.LoadSearchHistory(openFileDialogSearch.FileName);

                if(LoadSearchResult)
                {
                    Search.Text = "Refine";
                    comboBoxComparisonRHS.Items[1] = "Last value";
                    ResSrch.Enabled = true;
                    numericUpDownNewSearchIndex.Value = search.DumpNum;
                    numericUpDownOldSearchIndex.Value = search.DumpNum - 1;
                    UpdateValueTypeDropDown();
                    search.UpdateGridViewPage(true);
                } else
                {
                    Search.Text = "Search";
                    comboBoxComparisonRHS.Items[1] = "Unknown value";
                    ResSrch.Enabled = false;
                    search.Reset();
                    buttonUndoSearch.Enabled = search.CanUndo();
                    UpdateValueTypeDropDown();
                }
            }
        }

        private void buttonSaveSearch_Click(object sender, EventArgs e)
        {
            DialogResult SaveFileResult = saveFileDialogSearch.ShowDialog();
            if(SaveFileResult == DialogResult.OK)
            {
                search.SaveSearchHistory(saveFileDialogSearch.FileName);
            }
        }

        private void buttonCancelSearch_Click(object sender, EventArgs e)
        {
            DialogResult confirmationPrompt = MessageBox.Show("Are you sure you want to cancel?",
                                                              "Confirm Cancel",
                                                              MessageBoxButtons.OKCancel);
            if(confirmationPrompt == DialogResult.OK)
            {
                buttonCancelSearch.Enabled = false;
                gecko.CancelDump = true;
            }
        }

        protected void EnableControlTypes(Control parent, bool enabled)
        {
            foreach(Control c in parent.Controls)
            {
                if(c is Button || c is ComboBox || c is TextBox || c is BPList || c is CheckBox || c is DataGridView)
                {
                    if(!enabled && !c.Enabled)
                    {
                        WasAlreadyDisabled.Add(c);
                    }

                    if(enabled && c.Enabled)
                    {
                        WasAlreadyDisabled.Remove(c);
                    }

                    c.Enabled = enabled;
                }

                EnableControlTypes(c, enabled);
            }
        }

        private void EnableMainControls(bool enabled)
        {
            EnableControlTypes(MainControl, enabled);

            if(enabled)
            {
                foreach(Control c in WasAlreadyDisabled)
                {
                    c.Enabled = false;
                }
                WasAlreadyDisabled.Clear();
            }
        }

        private void memViewGrid_CellMouseMove(object sender, DataGridViewCellMouseEventArgs e)
        {
            if(e.ColumnIndex < 1) return;
            if(e.RowIndex < 0) return;

            uint offset;
            if(e.X < 21)
            {
                offset = 0;
            } else if(e.X < 35)
            {
                offset = 1;
            } else if(e.X < 49)
            {
                offset = 2;
            } else
            {
                offset = 3;
            }

            uint hoverAddress = viewer.address;
            hoverAddress += 0x10 * (uint)e.RowIndex;
            hoverAddress += (uint)(e.ColumnIndex - 1) * 4;
            hoverAddress += offset;
            address.HeaderText = GlobalFunctions.toHex(hoverAddress, 8);
        }

        private void WalkToBLR()
        {
            if(SteppingOut)
            {
                SteppingOut = false;
                buttonStepOutOf.Text = "Step out";
                return;
            }

            try
            {
                if(gecko.status() == WiiStatus.Breakpoint)
                {
                    SteppingOut = true;
                    buttonStepOutOf.Text = "Cancel";
                    while(!bpHandler.IsBLR() && SteppingOut)
                    {
                        BPStepOverButton_Click(null, null);
                        do
                        {
                            Application.DoEvents();
                        } while (BPCancel.Enabled);
                        MainControl.Update();
                    }
                    SteppingOut = false;
                    buttonStepOutOf.Text = "Step out";
                }
            } catch(ETCPGeckoException exc)
            {
                exceptionHandling.HandleException(exc);
            }
        }

        private void comboBoxDisplayType_SelectedIndexChanged(object sender, EventArgs e)
        {
            search.DisplayType = comboBoxDisplayType.SelectedItem.ToString();
            comboBoxDisplayType.Update();
            search.UpdateGridViewPage();
            ResizeSearchResults();
        }

        private void SearchResults_ColumnDividerDoubleClick(object sender,
                                                            DataGridViewColumnDividerDoubleClickEventArgs e)
        {
            SearchResults.AutoResizeColumn(e.ColumnIndex, DataGridViewAutoSizeColumnMode.AllCells);
        }

        public void ResizeSearchResults()
        {
            SearchResults.AutoResizeColumn(1, DataGridViewAutoSizeColumnMode.AllCells);
            SearchResults.AutoResizeColumn(2, DataGridViewAutoSizeColumnMode.AllCells);
            SearchResults.AutoResizeColumn(3, DataGridViewAutoSizeColumnMode.AllCells);
        }

        private void SearchResults_CellMouseDown(object sender, DataGridViewCellMouseEventArgs e)
        {
            int row = e.RowIndex, col = e.ColumnIndex;
            if(row >= 0 && e.Button == MouseButtons.Right && SearchResults[col, row].Selected == false)
            {
                foreach(DataGridViewRow selRow in SearchResults.SelectedRows)
                {
                    selRow.Selected = false;
                }

                SearchResults[col, row].Selected = true;
                SearchResults.CurrentCell = SearchResults[col, row];
            }
        }

        private void gCTWizardToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int indexToSelect = GCTCodeContents.Count;
            bool wasSelected = false;
            if(GCTCodeList.SelectedItems.Count != 0)
            {
                indexToSelect = GCTCodeList.SelectedIndices[0];
                wasSelected = true;
            }

            if(GCTCodeList.SelectedItems.Count > 0)
            {
                GCTCodeList.SelectedItems[0].Selected = false;
            }

            uint address = 0x80000000;
            string value = (0).ToString();
            if(SearchResults.SelectedRows.Count > 0)
            {
                address = search.GetAddress(SearchResults.SelectedRows[0].Index);
                value = search.GetResult(SearchResults.SelectedRows[0].Index).SValue;
            }
            codeWizard.textBoxAddress.Text = GlobalFunctions.toHex(address);
            codeWizard.textBoxValue.Text = value;
            codeWizard.comboBoxCodeType.SelectedIndex = 0;

            switch(comboBoxSearchDataType.SelectedIndex)
            {
                case 0:
                    codeWizard.radioButton8Bit.Checked = true;
                    break;
                case 1:
                    codeWizard.radioButton16Bit.Checked = true;
                    break;
                case 2:
                case 3:
                    codeWizard.radioButton32Bit.Checked = true;
                    break;
                default:
                    codeWizard.radioButton32Bit.Checked = true;
                    break;
            }

            codeWizard.PrepareGCTWizard(indexToSelect);
            DialogResult dialogResult = codeWizard.ShowDialog();
            if(dialogResult == DialogResult.OK)
            {
            }

            if(wasSelected)
            {
                GCTCodeList.Items[indexToSelect].Selected = true;
            }
        }

        private void gCTWizardToolStripMenuItemMemView_Click(object sender, EventArgs e)
        {
            int indexToSelect = GCTCodeContents.Count;
            bool wasSelected = false;
            if(GCTCodeList.SelectedItems.Count != 0)
            {
                indexToSelect = GCTCodeList.SelectedIndices[0];
                wasSelected = true;
            }

            if(GCTCodeList.SelectedItems.Count > 0)
            {
                GCTCodeList.SelectedItems[0].Selected = false;
            }

            UInt32 vAdd = viewer.selectedAddress;
            UInt32 value = gecko.peek(vAdd);

            codeWizard.textBoxAddress.Text = GlobalFunctions.toHex(vAdd);
            codeWizard.textBoxValue.Text = GlobalFunctions.toHex(value);

            codeWizard.comboBoxCodeType.SelectedIndex = 0;

            codeWizard.radioButton32Bit.Checked = true;

            codeWizard.PrepareGCTWizard(indexToSelect);
            DialogResult dialogResult = codeWizard.ShowDialog();
            if(dialogResult == DialogResult.OK)
            {
            }

            if(wasSelected)
            {
                GCTCodeList.Items[indexToSelect].Selected = true;
            }
        }

        private void copyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Clipboard.Clear();
            String clipboardText = String.Empty;
            foreach(DataGridViewRow row in SearchResults.SelectedRows)
            {
                for(int i = 0; i < SearchResults.ColumnCount; i++)
                {
                    clipboardText += row.Cells[i].Value.ToString();
                    if(i + 1 < SearchResults.ColumnCount)
                    {
                        clipboardText += "\t";
                    } else
                    {
                        clipboardText += "\r\n";
                    }
                }
            }
            Clipboard.SetText(clipboardText);
        }

        private void checkBoxPauseCodes_CheckedChanged(object sender, EventArgs e)
        {
            Settings.Default.PauseCodes = checkBoxPauseCodes.Checked;
            Settings.Default.Save();
        }

        private void checkBoxBPNext_CheckedChanged(object sender, EventArgs e)
        {
            Settings.Default.BPNext = checkBoxBPNext.Checked;
            Settings.Default.Save();
        }

        private void MainForm_ResizeEnd(object sender, EventArgs e)
        {
            Settings.Default.LastSize = Size;
            Settings.Default.Save();
        }

        private void memViewAValue_KeyDown(object sender, KeyEventArgs e)
        {
            if(e.KeyCode == Keys.Up || e.KeyCode == Keys.Down || e.KeyCode == Keys.PageUp || e.KeyCode == Keys.PageDown)
            {
                KeyEventArgs newArgs = new KeyEventArgs(e.KeyCode | Keys.Shift);
                memViewGrid_KeyDown(sender, newArgs);
                memViewAValue.Text = GlobalFunctions.toHex(viewer.selectedAddress);
                e.Handled = true;
            }
            if(e.KeyCode == Keys.Enter)
            {
                uint address;
                if(GlobalFunctions.tryToHex(memViewAValue.Text, out address))
                {
                    CenteredMemViewSelection(sender, e, address);
                }
            }
        }

        private void vScrollBarMemViewGrid_Scroll(object sender, ScrollEventArgs e)
        {
            uint vAddress;
            memViewAValue.IsValidGet(out vAddress);
            int index = ValidMemory.rangeCheckId(vAddress);
            vAddress = ValidMemory.ValidAreas[index].low + (uint)e.NewValue;
            if(vAddress >= ValidMemory.ValidAreas[index].high)
            {
                vAddress = ValidMemory.ValidAreas[index].low;
            }
            if(!ValidMemory.validAddress(vAddress))
            {
                MessageBox.Show("Invalid address");
                return;
            }
            vAddress &= 0xFFFFFFFC;

            MemViewARange.SelectedIndexChanged -= MemViewARange_SelectedIndexChanged;
            MemViewARange.SelectedIndex = ValidMemory.rangeCheckId(vAddress);
            MemViewARange.SelectedIndexChanged += MemViewARange_SelectedIndexChanged;

            CenteredMemView(sender, e, vAddress);
        }

        private void disassemblerToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DisRegion.Text = GlobalFunctions.toHex(viewer.selectedAddress);

            DisUpdateBtn_Click(sender, e);

            MainControl.SelectedTab = DisPage;
        }

        private void memoryViewerToolStripMenuItem_Click(object sender, EventArgs e)
        {
            CenteredMemViewSelection(sender, e, disassembler.disAddress);
            MainControl.SelectedTab = MemView;
        }

        private void SortToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ListSortDirection SortDirection = ListSortDirection.Ascending;
            DataGridViewColumn currentSelectedColumn = SearchResults.Columns[SearchResults.CurrentCell.ColumnIndex];

            if(SearchResults.SortedColumn == currentSelectedColumn && SearchResults.SortOrder == SortOrder.Ascending)
            {
                SortDirection = ListSortDirection.Descending;
            }
            SearchResults.Sort(currentSelectedColumn, SortDirection);
        }

        private void deleteToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SendKeys.Send("{DELETE}");
            SendKeys.Flush();
        }

        private void copySelectionToolStripMenuItem_Click(object sender, EventArgs e)
        {
            memViewGrid_KeyDown(sender, new KeyEventArgs(Keys.C | Keys.Control));
        }

        private void copyAllCellsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            memViewGrid_KeyDown(sender, new KeyEventArgs(Keys.C | Keys.Control | Keys.Shift));
        }

        private void disableToolStripMenuItem_Click(object sender, EventArgs e)
        {
            CodeContent code = GCTCodeContents[GCTCodeList.SelectedIndices[0]];
            CodeContent commentedCode = new CodeContent();
            for(int i = 0; i < code.lines.Count; i++)
            {
                commentedCode.addLine(code.lines[i].left, code.lines[i].right, false);
            }

            GCTCodeValues.Text = CodeController.CodeContentToCodeTextBox(commentedCode);
        }

        private void enableToolStripMenuItem_Click(object sender, EventArgs e)
        {
            CodeContent code = GCTCodeContents[GCTCodeList.SelectedIndices[0]];
            CodeContent uncommentedCode = new CodeContent();
            for(int i = 0; i < code.lines.Count; i++)
            {
                uncommentedCode.addLine(code.lines[i].left, code.lines[i].right, true);
            }

            GCTCodeValues.Text = CodeController.CodeContentToCodeTextBox(uncommentedCode);
        }

        private void addToolStripMenuItem_Click(object sender, EventArgs e)
        {
            AddressTextBox addressBox = GetAddressBoxFromSender(sender);
            if(addressBox != null)
            {
                addressBox.SendKeyCode(new KeyEventArgs(Keys.Control | Keys.Enter));
            }
        }

        private void removeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            AddressTextBox addressBox = GetAddressBoxFromSender(sender);
            if(addressBox != null)
            {
                addressBox.SendKeyCode(new KeyEventArgs(Keys.Control | Keys.Delete));
            }
        }

        private void clearAllHistoryToolStripMenuItem_Click(object sender, EventArgs e)
        {
            AddressTextBox addressBox = GetAddressBoxFromSender(sender);
            if(addressBox != null)
            {
                addressBox.SendKeyCode(new KeyEventArgs(Keys.Control | Keys.Shift | Keys.Delete));
            }
        }

        private void cutAllHistoryToolStripMenuItem_Click(object sender, EventArgs e)
        {
            AddressTextBox addressBox = GetAddressBoxFromSender(sender);
            if(addressBox != null)
            {
                addressBox.SendKeyCode(new KeyEventArgs(Keys.Control | Keys.Shift | Keys.X));
            }
        }

        private void copyAllHistoryToolStripMenuItem_Click(object sender, EventArgs e)
        {
            AddressTextBox addressBox = GetAddressBoxFromSender(sender);
            if(addressBox != null)
            {
                addressBox.SendKeyCode(new KeyEventArgs(Keys.Control | Keys.Shift | Keys.C));
            }
        }

        private void pasteAllHistoryToolStripMenuItem_Click(object sender, EventArgs e)
        {
            AddressTextBox addressBox = GetAddressBoxFromSender(sender);
            if(addressBox != null)
            {
                addressBox.SendKeyCode(new KeyEventArgs(Keys.Control | Keys.Shift | Keys.V));
            }
        }

        private void autoHistoryToolStripMenuItem_Click(object sender, EventArgs e)
        {
            AddressTextBox addressBox = GetAddressBoxFromSender(sender);
            if(addressBox != null)
            {
                addressBox.AutoHistory = !addressBox.AutoHistory;
            }
        }

        private void addressContextMenu_Opened(object sender, EventArgs e)
        {
            AddressTextBox addressBox = GetAddressBoxFromSender(sender);
            HistoryTextBox historyBox = GetHistoryBoxFromSender(sender);
            if(addressBox != null)
            {
                autoHistoryToolStripMenuItem.Checked = addressBox.AutoHistory;
                AddressContextMenuOwner = addressBox;
                HistoryContextMenuOwner = null;
            } else if(historyBox != null)
            {
                autoHistoryToolStripMenuItem.Checked = historyBox.AutoHistory;
                AddressContextMenuOwner = null;
                HistoryContextMenuOwner = historyBox;
            }
        }

        private AddressTextBox GetAddressBoxFromSender(object sender)
        {
            ToolStripMenuItem myItem = sender as ToolStripMenuItem;

            if(myItem == null)
            {
                ContextMenuStrip theStrip = sender as ContextMenuStrip;
                if(theStrip != null)
                {
                    return theStrip.SourceControl as AddressTextBox;
                }
            }

            if(myItem != null)
            {
                ContextMenuStrip theStrip = myItem.Owner as ContextMenuStrip;
                if(theStrip != null)
                {
                    return theStrip.SourceControl as AddressTextBox;
                }
            }

            return null;
        }

        private HistoryTextBox GetHistoryBoxFromSender(object sender)
        {
            ToolStripMenuItem myItem = sender as ToolStripMenuItem;

            if(myItem == null)
            {
                ContextMenuStrip theStrip = sender as ContextMenuStrip;
                if(theStrip != null)
                {
                    return theStrip.SourceControl as HistoryTextBox;
                }
            }

            if(myItem != null)
            {
                ContextMenuStrip theStrip = myItem.Owner as ContextMenuStrip;
                if(theStrip != null)
                {
                    return theStrip.SourceControl as HistoryTextBox;
                }
            }

            return null;
        }

        private void showHistoryToolStripMenuItem_Click(object sender, EventArgs e)
        {
            AddressTextBox addressBox = GetAddressBoxFromSender(sender);
            if(addressBox != null)
            {
                addressBox.ShowHistory(true);
            }
        }

        private void BPConditionRegSelect_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateBPCondValue();
        }

        private void UpdateBPCondValue()
        {
            BPCondValue.Text = GlobalFunctions.toHex(bpHandler.GetRegisterValue(BPConditionRegSelect.SelectedIndex));
        }

        private void copyToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            if(AddressContextMenuOwner != null)
                Clipboard.SetText(AddressContextMenuOwner.Text);
            else if(HistoryContextMenuOwner != null)
                Clipboard.SetText(HistoryContextMenuOwner.Text);
        }

        private void pasteToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if(AddressContextMenuOwner != null)
                AddressContextMenuOwner.Text = Clipboard.GetText();
            else if(HistoryContextMenuOwner != null)
                HistoryContextMenuOwner.Text = Clipboard.GetText();
        }

        private void MemViewScrollbar_KeyDown(object sender, KeyEventArgs e)
        {
            if(e.KeyCode == Keys.PageDown)
            {
                MemViewScrollbar.Value = 0;
            } else if(e.KeyCode == Keys.PageUp)
            {
                MemViewScrollbar.Value = 2;
            }
        }

        private void deleteToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            BPCondDel_Click(sender, e);
        }

        private void clearAllToolStripMenuItem_Click(object sender, EventArgs e)
        {
            BPCondClear_Click(sender, e);
        }

        private void copyToolStripMenuItem2_Click(object sender, EventArgs e)
        {
            string clipboardText = String.Empty;
            foreach(string cond in BPCondList.Items)
            {
                clipboardText += cond.ToString() + "\r\n";
            }
            Clipboard.SetText(clipboardText);
        }

        private void SRR0NEQToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int reg = (int)BPList.RegisterList.SRR0;
            BreakpointCondition cond = new BreakpointCondition(reg,
                                                               bpHandler.GetRegisterValue(reg),
                                                               BreakpointComparison.NotEqual);

            bpHandler.conditions.Add(cond);
        }

        private void SRR0EQToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int reg = (int)BPList.RegisterList.SRR0;
            BreakpointCondition cond = new BreakpointCondition(reg,
                                                               bpHandler.GetRegisterValue(reg),
                                                               BreakpointComparison.Equal);

            bpHandler.conditions.Add(cond);
        }

        private void copyToolStripMenuItem3_Click(object sender, EventArgs e)
        {
            string clipboardText = String.Empty;
            foreach(string asm in DisAssBox.Items)
            {
                clipboardText += asm.ToString() + "\r\n";
            }
            Clipboard.SetText(clipboardText);
        }

        private void AsText_KeyDown(object sender, KeyEventArgs e)
        {
            if(e.KeyCode == Keys.Enter)
            {
                e.Handled = true;
                Assemble_Click(sender, e);
            }
        }

        private void SetConditionGroupTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if(e.KeyCode == Keys.Enter)
            {
                List<int> indices = new List<int>();
                for(int i = BPCondList.SelectedItems.Count - 1; i >= 0; i--)
                {
                    indices.Add(BPCondList.SelectedIndices[i]);
                }

                foreach(int index in indices)
                {
                    bpHandler.conditions
                        .SetIndexedConditionGroup(index, Convert.ToUInt32(SetConditionGroupTextBox.Text));
                }
                BPCondMenu.Hide();
            }
        }

        private void BPCondMenu_Opened(object sender, EventArgs e)
        {
            int index = BPCondList.SelectedIndex;
            String newText;

            if(index == -1)
            {
                newText = "1";
            } else
            {
                newText = bpHandler.conditions.GetIndexedConditionGroup(BPCondList.SelectedIndex).ToString();
            }
            SetConditionGroupTextBox.Text = newText;
        }

        private void SetConditionGroupTextBox_TextChanged(object sender, EventArgs e)
        {
            String text = SetConditionGroupTextBox.Text;
            text = Regex.Replace(text, "[^0-9]", String.Empty);
            SetConditionGroupTextBox.Text = text;
        }

        private void setConditionGroupToolStripMenuItem_MouseMove(object sender, MouseEventArgs e)
        {
            SetConditionGroupTextBox.Focus();
            SetConditionGroupTextBox.SelectAll();
        }

        private void pasteToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            String[] sep = Clipboard.GetText().Split(new char[] { '\r', '\n' });
            foreach(String entry in sep)
            {
                BreakpointCondition cond = BreakpointCondition.FromString(entry);
                if(cond != null)
                {
                    bpHandler.conditions.Add(cond);
                }
            }
        }

        private void copyToolStripMenuItem4_Click(object sender, EventArgs e)
        {
            string clipboardText = String.Empty;
            foreach(string cond in BPCondList.SelectedItems)
            {
                clipboardText += cond.ToString() + "\r\n";
            }
            Clipboard.SetText(clipboardText);
        }

        private void checkBoxLogSteps_CheckedChanged(object sender, EventArgs e)
        {
            if(BPStepLogWriter != null)
            {
                BPStepLogWriter.Close();
                BPStepLogWriter.Dispose();
                BPStepLogWriter = null;
            }

            if(checkBoxLogSteps.Checked)
            {
                DialogResult SaveFileResult = saveFileDialogLogSteps.ShowDialog();
                if(SaveFileResult == DialogResult.OK)
                {
                    BPStepLogWriter = new StreamWriter(saveFileDialogLogSteps.FileName, true);
                    BPStepLogWriter.WriteLine();
                    BPStepLogWriter.WriteLine();
                } else
                {
                    checkBoxLogSteps.Checked = false;
                }
            }
        }

        private void buttonUndoSearch_Click(object sender, EventArgs e)
        {
            DialogResult confirmationPrompt = MessageBox.Show("Are you sure you want to undo?",
                                                              "Confirm Undo",
                                                              MessageBoxButtons.OKCancel);
            if(confirmationPrompt == DialogResult.OK)
            {
                search.UndoSearch();
            }

            buttonUndoSearch.Enabled = search.CanUndo();
        }

        private void numericUpDownNewSearchIndex_ValueChanged(object sender, EventArgs e)
        {
            if(numericUpDownNewSearchIndex.Value > search.DumpNum)
            {
                numericUpDownNewSearchIndex.Value = search.DumpNum;
            } else if(numericUpDownNewSearchIndex.Value != 0)
            {
                search.LoadIndexIntoNewSearchDump(Convert.ToInt32(numericUpDownNewSearchIndex.Value));
                search.LoadIndexIntoSearchList(Convert.ToInt32(numericUpDownNewSearchIndex.Value));
                search.UpdateGridViewPage(false);
            }
            UpdateValueTypeDropDown();
        }

        private void numericUpDownOldSearchIndex_ValueChanged(object sender, EventArgs e)
        {
            if(numericUpDownOldSearchIndex.Value > search.DumpNum - 1)
            {
                if(search.DumpNum == 0)
                {
                    numericUpDownOldSearchIndex.Value = 0;
                } else
                {
                    numericUpDownOldSearchIndex.Value = search.DumpNum - 1;
                }
            } else if(numericUpDownOldSearchIndex.Value != 0)
            {
                search.LoadIndexIntoOldSearchDump(Convert.ToInt32(numericUpDownOldSearchIndex.Value));
                search.UpdateGridViewPage(false);
            }
            UpdateValueTypeDropDown();
        }

        private void buttonAddSearchGroup_Click(object sender, EventArgs e)
        {
            UInt32 lValue;
            if(!GlobalFunctions.tryToHex(textBoxComparisonValue.Text, out lValue))
            {
                lValue = 0;
            }

            searchComparisons.Add(new SearchComparisonInfo(GetCmpType(), lValue, GetCmpRHS()));
            SearchGroupIndex = searchComparisons.Count - 1;
            groupBoxSearchGroups.Text = "Search Groups (" + searchComparisons.Count + ")";
        }

        private int SearchGroupIndex
        {
            get
            {
                return Convert.ToInt32(numericUpDownSearchGroup.Value) - 1;
            }
            set
            {
                numericUpDownSearchGroup.Value = value + 1;
            }
        }

        private void buttonRemoveGroup_Click(object sender, EventArgs e)
        {
            if(searchComparisons.Count > 1)
            {
                searchComparisons.RemoveAt(SearchGroupIndex);
            }
            if(SearchGroupIndex >= searchComparisons.Count)
            {
                SearchGroupIndex = searchComparisons.Count - 1;
            }
            groupBoxSearchGroups.Text = "Search Groups (" + searchComparisons.Count + ")";
        }

        private void buttonClearSearchGroup_Click(object sender, EventArgs e)
        {
            while(searchComparisons.Count > 1)
            {
                searchComparisons.RemoveAt(1);
            }
            SearchGroupIndex = 0;
            groupBoxSearchGroups.Text = "Search Groups (" + searchComparisons.Count + ")";
        }

        private void lowerValue_TextChanged(object sender, EventArgs e)
        {
            UInt32 value;
            if(GlobalFunctions.tryToHex(textBoxComparisonValue.Text, out value))
            {
                searchComparisons[SearchGroupIndex].value = value;
            }
        }

        private void numericUpDownSearchGroup_ValueChanged(object sender, EventArgs e)
        {
            Int32 value = SearchGroupIndex;
            if(value >= searchComparisons.Count)
            {
                SearchGroupIndex = searchComparisons.Count - 1;
                return;
            }
            SearchComparisonInfo comp = searchComparisons[value];
            SetCmpRHS(comp.searchType);
            SetCmpType(comp.comparisonType);
            textBoxComparisonValue.Text = GlobalFunctions.toHex(comp.value);
        }

        private void GCTCodeValues_KeyDown(object sender, KeyEventArgs e)
        {
            if(e.KeyCode == Keys.A && e.Control)
            {
                GCTCodeValues.SelectAll();
            }
        }

        private void setSRR0HereToolStripMenuItem_Click(object sender, EventArgs e)
        {
            bpHandler.SetSRR0(disassembler.disAddress);
            MainControl.SelectedTab = BreakpointPage;
        }

        private void ShowMemContextMenu_Opening(object sender, CancelEventArgs e)
        {
            if(ValidMemory.validAddress(bpHandler.MemoryAddress))
            {
                toolStripTextBoxShowMemAddress.Text = GlobalFunctions.toHex(bpHandler.MemoryAddress);
                toolStripTextBoxShowMemValue.Text = GlobalFunctions.toHex(gecko.peek(bpHandler.MemoryAddress));
            } else
            {
                toolStripTextBoxShowMemAddress.Text = "00000000";
                toolStripTextBoxShowMemValue.Text = "00000000";
            }
        }

        private void ShowMemAddressToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Clipboard.SetText(toolStripTextBoxShowMemAddress.Text);
        }

        private void ShowMemValueToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Clipboard.SetText(toolStripTextBoxShowMemValue.Text);
        }

        public void ScrollBPDissToLine(int line)
        {
        }

        private void walkToBlrToolStripMenuItem_Click(object sender, EventArgs e)
        {
            WalkToBLR();
        }

        private void ParseStackFrame(uint stackPointer, out uint LRSaveWord, ref uint nextFramePointer)
        {
            LRSaveWord = 0;
            nextFramePointer = 0;
            try
            {
                if(bpHandler.contextAddress != 0)
                {
                    uint potentialNextFramePointer = gecko.peek(stackPointer);
                    uint PotentialLRSaveWord = gecko.peek(potentialNextFramePointer + 4);
                    if(ValidMemory.rangeCheck(potentialNextFramePointer) == AddressType.Rw)
                        nextFramePointer = potentialNextFramePointer;

                    if(ValidMemory.rangeCheck(PotentialLRSaveWord) == AddressType.Ex)
                        LRSaveWord = PotentialLRSaveWord;
                }
            } catch(ETCPGeckoException exc)
            {
                exceptionHandling.HandleException(exc);
            }
        }

        private void stackFrameToolStripMenuItem_Click(object sender, EventArgs e)
        {
            uint LRSaveWord = 0;
            uint nextFramePointer = bpHandler.GetRegisterValue((int)BPList.RegisterList.r1);
            ParseStackFrame(nextFramePointer, out LRSaveWord, ref nextFramePointer);

            try
            {
                if(LRSaveWord != 0)
                {
                    if(bpHandler.SetBreakpoint(LRSaveWord, BreakpointType.Execute, true))
                    {
                        BPMode(true);
                    }
                    bpHandler.DecIndent();
                }
            } catch(ETCPGeckoException exc)
            {
                exceptionHandling.HandleException(exc);
            }
        }

        private void leafToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                if(gecko.status() == WiiStatus.Breakpoint)
                {
                    if(bpHandler.SetBreakpoint(bpHandler.GetRegisterValue((int)BPList.RegisterList.LR),
                                               BreakpointType.Execute,
                                               true))
                    {
                        BPMode(true);
                    }
                    bpHandler.DecIndent();
                }
            } catch(ETCPGeckoException exc)
            {
                exceptionHandling.HandleException(exc);
            }
        }

        private uint RecursivePromptDisassemblySearch(uint searchStartAddress, bool searchDown, string regex, int count)
        {
            UInt32 bAddress;
            do
            {
                bAddress = FindRegexAddressInDisassembly(ref searchStartAddress, searchDown, regex, count);
            } while (bAddress == 0 && SearchingDisassembly);

            if(bAddress != 0)
            {
                return bAddress;
            } else
            {
                DialogResult result = MessageBox.Show("Could not find " +
                    textBoxDisassemblySearch.Text +
                    "\n\nContinue searching?",
                                                      string.Empty,
                                                      MessageBoxButtons.YesNo);
                if(result == DialogResult.Yes)
                {
                    if(ValidMemory.validAddress(searchStartAddress))
                    {
                        return RecursivePromptDisassemblySearch(searchStartAddress, searchDown, regex, count);
                    } else return 0;
                } else return 0;
            }
        }

        private void buttonDisassemblySearch_Click(object sender, EventArgs e)
        {
            if(SearchingDisassembly)
            {
                SearchingDisassembly = false;
                return;
            }
            uint searchStartAddress;
            if(!AsAddress.IsValidGet(out searchStartAddress))
            {
                MessageBox.Show("Start address fail!");
                return;
            }

            if(textBoxDisassemblySearch.Text == String.Empty)
            {
                MessageBox.Show("Regex fail!");
                return;
            }

            string searchString = textBoxDisassemblySearch.Text;

            if(!checkBoxRegexSearch.Checked)
            {
                searchString = Regex.Escape(searchString);
            }

            SearchingDisassembly = true;
            buttonDisassemblySearch.Text = "Cancel";

            UInt32 bAddress;
            UInt32 searchStartAddressCopy = searchStartAddress;
            bool searchDown = radioButtonSearchDisassemblyDown.Checked;
            do
            {
                bAddress = FindRegexAddressInDisassembly(ref searchStartAddressCopy,
                                                         searchDown,
                                                         searchString,
                                                         0xFE00 / 4 * 2);
            } while (bAddress == 0 && SearchingDisassembly && searchStartAddressCopy != 0x817FFFFC &&
                searchStartAddressCopy != 0x80000000);

            if(bAddress != 0)
            {
                disassembler.DissToBox(bAddress);
            } else
            {
                if(SearchingDisassembly)
                    MessageBox.Show("Could not find search query");
            }

            SearchingDisassembly = false;
            buttonDisassemblySearch.Text = "Search";
        }

        public uint FindRegexAddressInDisassembly(ref uint searchStartAddress, bool searchDown, string regex, int count)
        {
            uint disassemblyStartAddress;
            uint retVal = 0;
            string[] searchDisassemblyStrings;
            disassemblyStartAddress = searchStartAddress + 4;
            if(!searchDown)
            {
                disassemblyStartAddress = searchStartAddress - (uint)(count * 4);
                if(disassemblyStartAddress < 0x80000000)
                {
                    uint diff = 0x80000000 - disassemblyStartAddress;
                    disassemblyStartAddress = 0x80000000;
                    count -= (int)(diff / 4);
                }
            }

            searchDisassemblyStrings = disassembler.Disassemble(disassemblyStartAddress, count);

            if(!searchDown)
            {
                Array.Reverse(searchDisassemblyStrings);
            }

            String foundLine = String.Empty;

            foreach(String line in searchDisassemblyStrings)
            {
                if(Regex.Match(line.Substring(20), regex).Success)
                {
                    foundLine = line;
                    break;
                }
            }

            if(foundLine != String.Empty)
            {
                if(!(GlobalFunctions.tryToHex(foundLine.Substring(0, 8), out retVal) && ValidMemory.validAddress(retVal)))
                {
                    retVal = 0;
                }
            } else
            {
                int end = searchDisassemblyStrings.Length - 1;
                uint bAddress;
                if(GlobalFunctions.tryToHex(searchDisassemblyStrings[end].Substring(0, 8), out bAddress) &&
                    ValidMemory.validAddress(bAddress))
                {
                    searchStartAddress = bAddress;
                } else
                {
                    searchStartAddress = 0;
                }
            }

            return retVal;
        }

        private void buttonSerialPoke_Click(object sender, EventArgs e)
        {
            PButton_Click(sender, e);
            PAddress.ShowHistory(true);
            PAddress.SendKeyCode(new KeyEventArgs(Keys.Down));
            PAddress.SendKeyCode(new KeyEventArgs(Keys.Enter));
        }

        private uint GetFunctionStartAddress(uint searchStartAddress)
        {
            string prologueString = "^(blr|b..lr|\\.)";
            uint prologueAddress = RecursivePromptDisassemblySearch(searchStartAddress, false, prologueString, 1000) + 4;
            return prologueAddress;
        }

        private uint GetFunctionEndAddress(uint searchStartAddress)
        {
            string epilogueString = "^(blr|b..lr)";
            uint epilogueAddress = RecursivePromptDisassemblySearch(searchStartAddress, true, epilogueString, 1000);
            return epilogueAddress;
        }

        private void copyFunctionToolStripMenuItem_Click(object sender, EventArgs e)
        {
            uint searchStartAddress;
            if(!AsAddress.IsValidGet(out searchStartAddress))
            {
                MessageBox.Show("Start address fail!");
                return;
            }

            uint prologueAddress = GetFunctionStartAddress(searchStartAddress);
            uint epilogueAddress = GetFunctionEndAddress(searchStartAddress);

            int count = (int)(epilogueAddress - prologueAddress) + 4;
            count /= 4;

            String[] searchDisassemblyStrings = disassembler.Disassemble(prologueAddress, count);

            String BigDisassemblyString = String.Empty;

            foreach(string line in searchDisassemblyStrings)
            {
                BigDisassemblyString += line + "\r\n";
            }

            Clipboard.SetText(BigDisassemblyString);
        }

        private void toolStripTextBoxShowMemValue_KeyDown(object sender, KeyEventArgs e)
        {
            if(e.KeyCode == Keys.Enter && !e.Control)
            {
                e.Handled = true;
                uint address, value;
                if(GlobalFunctions.tryToHex(toolStripTextBoxShowMemAddress.Text, out address) &&
                    ValidMemory.validAddress(address) &&
                    GlobalFunctions.tryToHex(toolStripTextBoxShowMemValue.Text, out value))
                {
                    gecko.poke(address, value);
                }
            }
        }

        private void ChangeMemViewFontSize(float newSize)
        {
            foreach(DataGridViewRow row in memViewGrid.Rows)
            {
                foreach(DataGridViewCell cell in row.Cells)
                {
                    Font newFont = null;
                    if(cell.Style.Font != null)
                    {
                        newFont = new Font(cell.Style.Font.FontFamily, newSize);
                    } else if(cell.InheritedStyle.Font != null)
                    {
                        newFont = new Font(cell.InheritedStyle.Font.FontFamily, newSize);
                    }

                    if(newFont != null)
                    {
                        cell.Style.Font = newFont;
                    }
                }
            }

            Font newColumnHeaderFont = new Font(memViewGrid.ColumnHeadersDefaultCellStyle.Font.FontFamily, newSize);

            memViewGrid.ColumnHeadersDefaultCellStyle.Font = newColumnHeaderFont;
        }

        private void toolStripTextBoxMemViewFontSize_KeyDown(object sender, KeyEventArgs e)
        {
            if(e.KeyCode == Keys.Enter)
            {
                try
                {
                    float casted = Convert.ToSingle(toolStripTextBoxMemViewFontSize.Text);
                    ChangeMemViewFontSize(casted);
                    Settings.Default.MemViewFontSize = casted;
                    Settings.Default.Save();
                    memViewContextMenu.Close();
                } catch(FormatException)
                {
                    toolStripTextBoxMemViewFontSize.Text = (10).ToString();
                }
            }
        }

        private void fontSizeToolStripMenuItem_MouseMove(object sender, MouseEventArgs e)
        {
            toolStripTextBoxMemViewFontSize.Focus();
            toolStripTextBoxMemViewFontSize.SelectAll();
        }

        private void viewFloatsInHexToolStripMenuItem_CheckedChanged(object sender, EventArgs e)
        {
            bpHandler.ShowFloatsInHex = viewFloatsInHexToolStripMenuItem.Checked;
            Settings.Default.ViewFloatsInHex = viewFloatsInHexToolStripMenuItem.Checked;
            Settings.Default.Save();
        }

        private uint IsLeafFunction()
        {
            try
            {
                if(gecko.status() == WiiStatus.Breakpoint)
                {
                    uint potentialBLAddress = bpHandler.GetRegisterValue((int)BPList.RegisterList.LR) - 4;
                    uint currentAddress = bpHandler.GetRegisterValue((int)BPList.RegisterList.SRR0);
                    string[] asmArray = disassembler.Disassemble(potentialBLAddress, 1);
                    string asm = string.Empty;
                    if(asmArray.Length > 0) asm = asmArray[0];
                    uint potentialStartAddress = 0;

                    if(Regex.Match(asm, "bl\\t0x").Success)
                    {
                        int addressIndex = asm.LastIndexOf("bl\t0x") + 5;

                        if(GlobalFunctions.tryToHex(asm.Substring(addressIndex), out potentialStartAddress) &&
                            ValidMemory.validAddress(potentialStartAddress))
                        {
                            uint startAddressCopy = potentialStartAddress;
                            uint potentialEndAddress = RecursivePromptDisassemblySearch(potentialStartAddress,
                                                                                        true,
                                                                                        "^(blr|b..lr)",
                                                                                        5000);
                            int range = (int)(potentialEndAddress - potentialStartAddress + 10);

                            if(currentAddress >= potentialStartAddress && currentAddress <= potentialEndAddress &&
                                FindRegexAddressInDisassembly(ref startAddressCopy, true, "stwu r1,", range) == 0)
                            {
                                return potentialBLAddress + 4;
                            }
                        }
                    }
                }
            } catch(ETCPGeckoException exc)
            {
                exceptionHandling.HandleException(exc);
            }

            return 0;
        }

        private List<uint> GetBreakpointCallStack()
        {
            List<uint> callStack = new List<uint>();

            uint address = bpHandler.GetRegisterValue((int)BPList.RegisterList.SRR0);
            if(address != 0) callStack.Add(address);

            address = IsLeafFunction();
            if(address != 0) callStack.Add(address - 4);

            uint stackPointer = bpHandler.GetRegisterValue((int)BPList.RegisterList.r1);

            do
            {
                ParseStackFrame(stackPointer, out address, ref stackPointer);
                if(address != 0) callStack.Add(address - 4);
            } while (stackPointer != 0);

            return callStack;
        }

        private void listBoxCallStack_DoubleClick(object sender, EventArgs e)
        {
            uint disasmAddress = 0;
            if(listBoxCallStack.Items.Count == 0)
            {
                LoadCallStack();
                return;
            }
            if(GlobalFunctions.tryToHex(listBoxCallStack.SelectedItem.ToString(), out disasmAddress))
            {
                if(ValidMemory.validAddress(disasmAddress))
                {
                    DisRegion.Text = listBoxCallStack.SelectedItem.ToString();

                    DisUpdateBtn_Click(sender, e);
                }
            }
        }

        private void LoadCallStack()
        {
            if(bpHandler.contextAddress != 0)
            {
                listBoxCallStack.Items.Clear();
                listBoxCallStack.Items.Add("Loading call stack...");
                List<uint> callStack = GetBreakpointCallStack();
                listBoxCallStack.Items.Clear();
                foreach(uint address in callStack)
                {
                    string Hex = String.Format("{0:X}", address);
                    listBoxCallStack.Items.Add(Hex);
                }
            } else
            {
                MessageBox.Show("Must be in a breakpoint to show call stack");
            }
        }

        private void gotoFunctionStartToolStripMenuItem_Click(object sender, EventArgs e)
        {
            uint startAddress;
            if(GlobalFunctions.tryToHex(AsAddress.Text, out startAddress))
            {
                if(ValidMemory.validAddress(startAddress))
                {
                    startAddress = GetFunctionStartAddress(startAddress);
                    if(ValidMemory.validAddress(startAddress))
                    {
                        DisRegion.Text = String.Format("{0:X}", startAddress);

                        DisUpdateBtn_Click(sender, e);
                    }
                }
            }
        }

        private void gotoFunctionEndToolStripMenuItem_Click(object sender, EventArgs e)
        {
            uint startAddress;
            if(GlobalFunctions.tryToHex(AsAddress.Text, out startAddress))
            {
                if(ValidMemory.validAddress(startAddress))
                {
                    startAddress = GetFunctionEndAddress(startAddress);
                    if(ValidMemory.validAddress(startAddress))
                    {
                        DisRegion.Text = String.Format("{0:X}", startAddress - 0x40);

                        DisUpdateBtn_Click(sender, e);

                        DisAssBox.SelectedIndex = 0x10;
                    }
                }
            }
        }

        private void copyToolStripMenuItem5_Click(object sender, EventArgs e)
        {
            Clipboard.SetText(listBoxCallStack.SelectedItem.ToString());
        }

        private void copyAllToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string clipboard = String.Empty;
            foreach(object item in listBoxCallStack.Items)
            {
                clipboard += item.ToString() + "\r\n";
            }
            Clipboard.SetText(clipboard);
        }

        private void loadToolStripMenuItem_Click(object sender, EventArgs e)
        {
            LoadCallStack();
        }

        private void convertASCIIToHexToolStripMenuItem_Click(object sender, EventArgs e)
        {
        }

        private void convertHexToASCIIToolStripMenuItem_Click(object sender, EventArgs e)
        {
        }

        private void jumpToOffsetToolStripMenuItem_MouseMove(object sender, MouseEventArgs e)
        {
            toolStripTextBoxMemViewOffset.Focus();
            toolStripTextBoxMemViewOffset.SelectAll();
        }

        private void toolStripTextBoxMemViewOffset_KeyDown(object sender, KeyEventArgs e)
        {
            if(e.KeyCode == Keys.Enter)
            {
                memViewAValue.AddOffsetToAddress(toolStripTextBoxAddressAddOffset.Text);
                MemViewUpdate_Click(sender, e);
                memViewContextMenu.Close();
                toolStripTextBoxMemViewOffset.Text = (0).ToString();
            }
        }

        private void addOffsetToolStripMenuItem_MouseMove(object sender, MouseEventArgs e)
        {
            toolStripTextBoxAddressAddOffset.Focus();
            toolStripTextBoxAddressAddOffset.SelectAll();
        }

        private void toolStripTextBoxAddressAddOffset_KeyDown(object sender, KeyEventArgs e)
        {
            if(e.KeyCode == Keys.Enter)
            {
                if(AddressContextMenuOwner != null)
                    AddressContextMenuOwner.AddOffsetToAddress(toolStripTextBoxAddressAddOffset.Text);
                HistoryContextMenu.Close();
                toolStripTextBoxAddressAddOffset.Text = (0).ToString();
            }
        }

        private int indexOfThreadGridView = -1;

        private void pauseToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if(indexOfThreadGridView != -1)
            {
                string addressStringLol = (string)ThreadGridView.Rows[indexOfThreadGridView].Cells[ThreadAddressColumn.Index].Value;
                uint address = uint.Parse(addressStringLol, NumberStyles.HexNumber);
                gecko.poke08(address + 0x328, 1);

                UInt32 OSSuspendThread;
                switch(gecko.OsVersionRequest())
                {
                    case 400:
                    case 410:
                        OSSuspendThread = 0x0103CB18;
                        break;

                    case 532:
                        OSSuspendThread = 0x010424dc;
                        break;

                    case 550:
                    case 551:
                        OSSuspendThread = 0x01047EA0;
                        break;

                    default:
                        MessageBox.Show("Unsupported Wii U OS version.",
                                        "Version mismatch",
                                        MessageBoxButtons.OK,
                                        MessageBoxIcon.Error);
                        return;
                }

                gecko.rpc(OSSuspendThread, address);

                ThreadGridView.Rows[indexOfThreadGridView].Cells[ThreadStateColumn.Index].Value = "Paused";
                int index = ThreadAddress.IndexOf(address);
                ThreadRunning[index] = false;
                ThreadDisplayComboBox_SelectedIndexChanged(ThreadDisplayComboBox, EventArgs.Empty);
            }
        }

        private void unpauseThreadToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if(indexOfThreadGridView != -1)
            {
                string addressStringLol = (string)ThreadGridView.Rows[indexOfThreadGridView].Cells[ThreadAddressColumn.Index].Value;
                uint address = uint.Parse(addressStringLol, NumberStyles.HexNumber);
                gecko.poke08(address + 0x328, 0);

                UInt32 OSResumeThread;
                switch(gecko.OsVersionRequest())
                {
                    case 400:
                    case 410:
                        OSResumeThread = 0x0103BFEC;
                        break;

                    case 532:
                        OSResumeThread = 0x010419b0;
                        break;

                    case 550:
                    case 551:
                        OSResumeThread = 0x01047374;
                        break;

                    default:
                        MessageBox.Show("Unsupported Wii U OS version.",
                                        "Version mismatch",
                                        MessageBoxButtons.OK,
                                        MessageBoxIcon.Error);
                        return;
                }

                gecko.rpc(OSResumeThread, address);

                ThreadGridView.Rows[indexOfThreadGridView].Cells[ThreadStateColumn.Index].Value = "Running";
                int index = ThreadAddress.IndexOf(address);
                ThreadRunning[index] = true;
                ThreadDisplayComboBox_SelectedIndexChanged(ThreadDisplayComboBox, EventArgs.Empty);
            }
        }

        private void memoryViewerToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            if(indexOfThreadGridView != -1)
            {
                string addressStringLol = (string)ThreadGridView.Rows[indexOfThreadGridView].Cells[ThreadAddressColumn.Index].Value;
                uint address = uint.Parse(addressStringLol, NumberStyles.HexNumber);
                CenteredMemViewSelection(sender, e, address);
                MainControl.SelectedTab = MemView;
            }
        }

        private void ThreadGridView_CellMouseClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            if(e.RowIndex != -1 && e.Button == MouseButtons.Right)
            {
                ThreadGridView.Rows[e.RowIndex].Cells[e.ColumnIndex].Selected = true;
                Rectangle bounds = ThreadGridView.GetCellDisplayRectangle(e.ColumnIndex, e.RowIndex, true);
                indexOfThreadGridView = e.RowIndex;
                ThreadContextMenuStrip.Show((Control)sender, bounds.Left + e.Location.X, bounds.Top + e.Location.Y);
            }
        }

        private void ThreadDisplayComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if(ThreadDisplayComboBox.SelectedIndex != -1)
            {
                int index = ThreadNames.IndexOf(ThreadDisplayComboBox.Text);
                if(ThreadRunning[index])
                {
                    threadStateLabel.Text = "RUNNING";
                    bpHandler.contextAddress = 0;
                } else
                {
                    threadStateLabel.Text = "PAUSED";
                    PopulateRegisters(ThreadAddress[index]);
                }
            }
        }

        private void PopulateRegisters(uint threadAddress)
        {
            bpHandler.contextAddress = threadAddress;
            bpHandler.GetRegisters();
        }

        private void GSC_Click(object sender, EventArgs e)
        {
            FolderBrowserDialog FBD = new FolderBrowserDialog();
            FBD.ShowNewFolderButton = true;
            DialogResult result = FBD.ShowDialog();
            if(result == DialogResult.OK)
            {
                SendingFilePath = FBD.SelectedPath;
                textBox1.Text = FBD.SelectedPath;
                Environment.SpecialFolder root = FBD.RootFolder;
            }
        }

        private void Send_Click(object sender, EventArgs e)
        {
            if(SendingFilePath != string.Empty)
            {
                FileStream stream = new FileStream(textBox1.Text, FileMode.Open, FileAccess.Read);
                Byte[] buffer = new Byte[stream.Length];
                stream.Read(buffer, 5000, buffer.Length);
                TcpClient socket = new TcpClient(textBox2.Text, 7331);
                NetworkStream nw = socket.GetStream();
                nw.Write(buffer, 5000, buffer.Length);
                nw.Close();
            }
        }
    }
}