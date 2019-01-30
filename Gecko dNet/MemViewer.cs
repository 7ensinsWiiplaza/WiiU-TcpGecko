using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;

using TCPTCPGecko;

namespace GeckoApp
{
    public enum MemoryViewMode
    {
        Hex,
        ASCII,
        ANSI,
        Unicode,
        Single,
        AutoZero,
        AutoDot
    }

    class MemoryViewer
    {
        private TCPGecko gecko;
        private DataGridView gView;
        private TextBox pokeAddress;
        private TextBox pokeValue;
        private Label fpValue;
        private uint cAddress;
        private Stack<uint> history = new Stack<uint>(), redo = new Stack<uint>();

        private ExceptionHandler exceptionHandling;

        private int oldCol = 1;
        private int oldRow = 0;

        public uint address
        {
            get
            {
                return cAddress;
            }
            set
            {
                value = value & 0xFFFFFFFC;
                if (cAddress != value)
                {
                    history.Push(cAddress);
                    redo.Clear();
                    cAddress = value & 0xFFFFFFFC;
                }
            }
        }

        public uint selectedAddress { get; private set; }


        public MemoryViewMode viewMode { get; set; }

        public bool Searching { get; set; }

        public MemoryViewer(TCPGecko UGecko, uint initAddress, DataGridView UGView,
            TextBox UPokeAddress, TextBox UPokeValue, Label UFPValue, ExceptionHandler UExpHandler)
        {
            gecko = UGecko;
            exceptionHandling = UExpHandler;

            cAddress = initAddress;
            gView = UGView;
            pokeAddress = UPokeAddress;
            pokeValue = UPokeValue;
            fpValue = UFPValue;
            gView.CellClick += CellClick;
            gView.SelectionChanged += CellSelectionChange;
        }

        public void Update()
        {
            Update(false);
        }

        public void Update(bool fast)
        {
            MemoryStream miniDump = new MemoryStream();
            int oldColumnIndex, oldRowIndex;
            if (gView.SelectedCells.Count > 0)
            {
                oldColumnIndex = gView.SelectedCells[0].ColumnIndex;
                oldRowIndex = gView.SelectedCells[0].RowIndex;
            }
            else
            {
                oldColumnIndex = 1;
                oldRowIndex = 1;
            }

            uint sAddress = cAddress & 0xFFFFFFF0;
            uint offset = cAddress - sAddress;
            try
            {
                gecko.Dump(sAddress, sAddress + 0x100, miniDump);

                if (gView.Rows.Count != 16)
                {
                    gView.Rows.Clear();
                    gView.Rows.Add(16);
                }

                miniDump.Seek(0, SeekOrigin.Begin);
                uint value, bValue;
                byte[] buffer = new byte[4];
                ushort hwInput;
                uint pValue = 0;
                for (int i = 0; i < 16; i++)
                {
                    gView.Rows[i].Cells[0].Value = GlobalFunctions.toHex(sAddress + i * 16);
                    for (int j = 1; j < 5; j++)
                    {
                        miniDump.Read(buffer, 0, 4);
                        bValue = BitConverter.ToUInt32(buffer, 0);
                        value = ByteSwap.Swap(bValue);
                        if (sAddress + i * 0x10 + (j - 1) * 4 == selectedAddress)
                            pValue = value;
                        DataGridViewCell cell = gView.Rows[i].Cells[j];
                        if (viewMode == MemoryViewMode.Hex)
                        {
                            cell.Value = GlobalFunctions.toHex(value);
                        }
                        else if (viewMode == MemoryViewMode.ASCII)
                        {
                            cell.Value = DecodeASCII(buffer);
                        }
                        else if (viewMode == MemoryViewMode.ANSI)
                        {
                            cell.Value = DecodeANSI(buffer);
                        }
                        else if (viewMode == MemoryViewMode.Unicode)
                        {
                            cell.Value = DecodeUnicode(buffer);
                        }
                        else if (viewMode == MemoryViewMode.Single)
                        {
                            cell.Value = PrettyFloat(GlobalFunctions.UIntToSingle(value));
                        }
                        else if (viewMode == MemoryViewMode.AutoZero || viewMode == MemoryViewMode.AutoDot)
                        {
                            if (ValidMemory.validAddress(value))
                            {
                                cell.Value = GlobalFunctions.toHex(value);
                            }
                            else
                            {
                                float singleCast = GlobalFunctions.UIntToSingle(value);
                                if (!float.IsNaN(singleCast) && Math.Abs(singleCast) > 1e-7 && Math.Abs(singleCast) < 1e10)
                                {
                                    cell.Value = PrettyFloat(GlobalFunctions.UIntToSingle(value));
                                }
                                else
                                {
                                    if (IsASCII(buffer))
                                    {
                                        if (viewMode == MemoryViewMode.AutoZero && value == 0)
                                        {
                                            cell.Value = GlobalFunctions.toHex(value);
                                        }
                                        else
                                        {
                                            cell.Value = DecodeASCII(buffer);
                                        }
                                    }
                                    else
                                    {
                                        cell.Value = GlobalFunctions.toHex(value);
                                    }
                                }
                            }
                        }
                    }
                }
                oldRow = (int)offset / 0x10;
                oldCol = (int)(offset & 0xC) / 4 + 1;
                if (!fast)
                {
                    gView.Rows[oldRowIndex].Cells[oldColumnIndex].Selected = true;
                }

                pokeAddress.Text = GlobalFunctions.toHex(selectedAddress);
                fpValue.Text = GlobalFunctions.UIntToSingle(pValue).ToString("G6");
            }
            catch (ETCPGeckoException e)
            {
                exceptionHandling.HandleException(e);
            }
        }

        private string PrettyFloat(float val)
        {
            string floatString = val.ToString("G6");
            if (floatString.Length > 8)
            {
                return val.ToString("G2");
            }
            else
            {
                return floatString;
            }
        }

        private static string DecodeUnicode(byte[] buffer)
        {
            string cellV = string.Empty;
            for (int k = 0; k < 2; k++)
            {
                ushort hwInput = (ushort)(buffer[k * 2] << 8 + buffer[k * 2 + 1]);
                if (hwInput > 0x20 && hwInput != 0x7F)
                    cellV += "   " + (char)hwInput;
                else
                    cellV += "   .";
            }
            return cellV;
        }

        private static bool IsASCII(byte[] buffer)
        {
            for (int k = 0; k < 4; k++)
            {
                if ((buffer[k] != 0) && (buffer[k] < 0x20 || buffer[k] > 0x7E)) return false;
            }
            return true;
        }

        private static string DecodeASCII(byte[] buffer)
        {
            string cellV = string.Empty;
            for (int k = 0; k < 4; k++)
            {
                byte bInput = buffer[k];
                if (bInput > 0x20 && bInput < 0x7F)
                    cellV += " " + (char)bInput;
                else
                    cellV += " .";
            }
            return cellV;
        }

        private static string DecodeANSI(byte[] buffer)
        {
            string cellV = string.Empty;
            for (int k = 0; k < 4; k++)
            {
                byte bInput = buffer[k];
                if (bInput > 0x20 && bInput != 0x7F)
                    cellV += " " + (char)bInput;
                else
                    cellV += " .";
            }
            return cellV;
        }

        private void CellClick(object sender, DataGridViewCellEventArgs e)
        {
        }


        private void CellSelectionChange(object sender, EventArgs e)
        {
            uint sAddress = cAddress & 0xFFFFFFF0;
            if (gView.SelectedCells.Count > 0)
            {
                int col = gView.SelectedCells[0].ColumnIndex;
                int row = gView.SelectedCells[0].RowIndex;
                if (col == 0)
                {
                    gView.Rows[oldRow].Cells[oldCol].Selected = true;
                }
                else
                {
                    oldCol = col;
                    oldRow = row;
                    uint addr = (uint)(sAddress + row * 16 + (col - 1) * 4);

                    if (selectedAddress == addr) return;

                    selectedAddress = addr;
                    pokeAddress.Text = GlobalFunctions.toHex(addr);
                    try
                    {
                        uint locValue = gecko.peek(addr);
                        pokeValue.Text = GlobalFunctions.toHex(locValue);
                        fpValue.Text = GlobalFunctions.UIntToSingle(locValue).ToString("G6");
                    }
                    catch (ETCPGeckoException exc)
                    {
                        exceptionHandling.HandleException(exc);
                    }
                }
            }
        }

        public void GoBack()
        {
            if (history.Count > 0)
            {
                redo.Push(cAddress);
                cAddress = history.Pop();
            }
        }

        public void GoForward()
        {
            if (redo.Count > 0)
            {
                history.Push(cAddress);
                cAddress = redo.Pop();
            }
        }

        public void SearchString(byte[] searchBytes, bool caseSensitive, bool unicode, bool hex)
        {
            byte[] stringBytes = searchBytes;

            uint startAddr = selectedAddress + 4;

            uint endAddress = 0;

            int dumpLength = (unicode ? 2 : 1);

            bool found = false;
            for (int i = 0; i < ValidMemory.ValidAreas.Length; i++)
            {
                if (startAddr >= ValidMemory.ValidAreas[i].low &&
                    startAddr < ValidMemory.ValidAreas[i].high)
                {
                    found = true;
                    endAddress = ValidMemory.ValidAreas[i].high;
                    break;
                }
            }

            if (!found)
            {
                throw new Exception("Memory area could not be acquired!");
            }

            bool valueFound = false;

            uint beginAddress = startAddr;

            MemoryStream ms = new MemoryStream();
            uint cVal;
            char cChar;

            uint SearchBufferSize = 0x400 * 256;

            uint dumpHigh = Math.Min(startAddr + SearchBufferSize, endAddress);

            try
            {
                int index;

                do
                {
                    ms.Seek(0, SeekOrigin.End);
                    int startIndex = (int)ms.Position;
                    gecko.Dump(startAddr, dumpHigh, ms);

                    byte[] streamArray = ms.GetBuffer();

                    index = GlobalFunctions.IndexOfByteArray(streamArray, stringBytes, startIndex, caseSensitive);

                    if (index != -1)
                    {
                        valueFound = true;
                        break;
                    }

                    startAddr = dumpHigh;
                    dumpHigh = Math.Min(startAddr + SearchBufferSize, endAddress);
                    Application.DoEvents();
                } while (dumpHigh != endAddress && !valueFound && Searching);

                if (valueFound)
                {
                    uint address = (uint)(beginAddress + index);
                    cAddress = address;
                }
                else
                {
                    cAddress = beginAddress - 4;
                    if (dumpHigh == endAddress)
                        MessageBox.Show("Could not find search query");
                }
            }
            catch (ETCPGeckoException exc)
            {
                exceptionHandling.HandleException(exc);
            }
        }
    }
}
