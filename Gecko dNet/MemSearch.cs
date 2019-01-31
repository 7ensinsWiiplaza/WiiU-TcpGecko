using Ionic.Zip;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;

using TCPTCPGecko;

namespace GeckoApp
{
    public enum SearchSize
    {
        Bit8,
        Bit16,
        Bit32,
        Single
    }

    public enum SearchType
    {
        Exact,
        Unknown,
        Old,
        Diff
    }

    public enum ComparisonType
    {
        Equal,
        NotEqual,
        Lower,
        LowerEqual,
        Greater,
        GreaterEqual,
        DifferentBy,
        DifferentByLess,
        DifferentByMore
    }

    public class SearchComparisonInfo
    {
        public ComparisonType comparisonType;
        public uint value;
        public SearchType searchType;

        public SearchComparisonInfo()
        {
            comparisonType = ComparisonType.Equal;
            value = 0;
            searchType = SearchType.Exact;
        }

        public SearchComparisonInfo(ComparisonType ctype, uint searchValue, SearchType stype)
        {
            comparisonType = ctype;
            value = searchValue;
            searchType = stype;
        }
    }

    [Serializable()]
    public class SearchResult
    {

        public uint address { get; private set; }
        public uint value { get; private set; }
        public uint oldValue { get; private set; }

        public SearchResult(uint address, uint value, uint old)
        {
            this.address = address;
            this.value = value;
            oldValue = old;
        }
    }

    public class DumpRange
    {
        public uint rangeLength;
        public uint streamOffset;

        public uint startAddress { get; set; }
        public uint endAddress { get; set; }

        public DumpRange()
        {
        }

        public DumpRange(uint startAddress)
        {
            this.startAddress = startAddress;
        }

        public DumpRange(uint startAddress, uint endAddress)
        {
            this.startAddress = startAddress;
            this.endAddress = endAddress;
        }
    }

    public struct StringResult
    {
        public string SAddress;
        public string SValue;
        public string SOldValue;
    }

    public class MemSearch
    {
        const int pageSize = 256;

        private List<uint> resultAddressList;
        private SearchSize sSize;
        private int cPage;
        private int cPages;
        private int oldSelectedRow;
        private bool InitialSearch;

        public SearchSize searchSize
        { get { return sSize; } }

        private bool UnknownStart;
        private uint UnknownLAddress;
        private uint UnknownHAddress;

        private TCPGecko gecko;
        private DataGridView gView;
        private Button prvButton;
        private Button nxButton;
        private Label resLab;
        private NumericUpDown pageUpDown;
        public Dump oldDump;
        public Dump newDump;
        private Dump undoDump;
        private List<uint> undoList;
        private int dumpNum;
        public int DumpNum
        {
            get { return dumpNum; }
        }
        private SearchHistoryManager searchHistory;

        private bool NewSearch = true;

        private ExceptionHandler exceptionHandling;
        public string DisplayType { get; set; }
        public bool blockDump { get; private set; }
        public uint totalBlockSize { get; private set; }
        public uint blocksDumpedSize { get; private set; }
        public int blockID { get; private set; }
        public int blockCount { get; private set; }
        public uint blockStart { get; private set; }
        public uint blockEnd { get; private set; }


        public MemSearch(TCPGecko uGecko, DataGridView uGView, Button uPrvButton, Button uNxButton,
            Label UResLab, NumericUpDown UPageUpDown, ExceptionHandler UEHandler)
        {
            exceptionHandling = UEHandler;

            gecko = uGecko;
            gView = uGView;

            prvButton = uPrvButton;
            nxButton = uNxButton;
            resLab = UResLab;
            pageUpDown = UPageUpDown;

            pageUpDown.ValueChanged += UpDownValueChanged;
            nxButton.Click += nextPage;
            prvButton.Click += previousPage;

            resultAddressList = new List<uint>();
            undoList = new List<uint>();

            blockDump = false;

            dumpNum = 0;

            searchHistory = new SearchHistoryManager();

        }

        void UpDownValueChanged(object sender, EventArgs e)
        {
            cPage = Convert.ToInt32(pageUpDown.Value) - 1;
            PrintPageAlt();
        }

        private void PrintPageAlt()
        {
            if (cPage <= 0)
            {
                cPage = 0;
                prvButton.Enabled = false;
            }
            else
            {
                prvButton.Enabled = true;
            }

            if (cPage >= cPages - 1)
            {
                cPage = cPages - 1;
                if (cPage < 0) cPage = 0;
                nxButton.Enabled = false;
            }
            else
            {
                nxButton.Enabled = (cPages > 1);
            }

            resLab.Text = resultAddressList.Count.ToString() + " results ("
             + cPages.ToString() + " pages)";

            int i = 0;
            string addr, value, oldv, diff;

            int strLength;
            switch (sSize)
            {
                case SearchSize.Bit8: strLength = 2; break;
                case SearchSize.Bit16: strLength = 4; break;
                default: strLength = 8; break;
            }

            int searchBytes = strLength / 2;

            int start = cPage * pageSize;
            int end = Math.Min(cPage * pageSize + pageSize, resultAddressList.Count);
            int count = end - start;
            if (count < gView.Rows.Count)
            {
                gView.Rows.Clear();
            }
            int addCount = count - gView.Rows.Count;
            if (addCount > 0)
            {
                gView.Rows.Add(addCount);
            }

            for (int j = start; j < end; j++)
            {
                SearchResult result;
                if (oldDump == null)
                {
                    result = new SearchResult(resultAddressList[j],
                        newDump.ReadAddress(resultAddressList[j], searchBytes),
                        0);
                }
                else
                {
                    result = new SearchResult(resultAddressList[j],
                        newDump.ReadAddress(resultAddressList[j], searchBytes),
                        oldDump.ReadAddress(resultAddressList[j], searchBytes));
                }

                addr = fixString(Convert.ToString(result.address, 16).ToUpper(), 8);
                if (DisplayType == "Hex")
                {
                    value = fixString(Convert.ToString(result.value, 16).ToUpper(), strLength);
                    oldv = fixString(Convert.ToString(result.oldValue, 16).ToUpper(), strLength);
                    diff = fixString(Convert.ToString(result.value - result.oldValue, 16).ToUpper(), strLength);
                }
                else if (DisplayType == "Dec")
                {
                    value = ((int)result.value).ToString();
                    oldv = ((int)result.oldValue).ToString();
                    diff = ((int)(result.value - result.oldValue)).ToString();
                }
                else
                {
                    float floatVal = GlobalFunctions.UIntToSingle(result.value);
                    float floatOldVal = GlobalFunctions.UIntToSingle(result.oldValue);

                    value = floatVal.ToString("g5");
                    oldv = floatOldVal.ToString("g5");
                    diff = (floatVal - floatOldVal).ToString("g5");
                }
                gView.Rows[i].Cells[0].Value = addr;

                if (InitialSearch)
                {
                    gView.Rows[i].Cells[1].Value = string.Empty;
                    gView.Rows[i].Cells[3].Value = string.Empty;

                }
                else if (resultAddressList[i] < oldDump.StartAddress || resultAddressList[i] > oldDump.EndAddress - searchBytes)
                {
                    gView.Rows[i].Cells[1].Value = "N/A";
                    gView.Rows[i].Cells[3].Value = "N/A";
                }
                else
                {
                    gView.Rows[i].Cells[1].Value = oldv;
                    gView.Rows[i].Cells[3].Value = diff;
                }
                gView.Rows[i].Cells[2].Value = value;
                i++;
            }
        }

        private void nextPage(object sender, EventArgs e)
        {
            pageUpDown.Value = Convert.ToDecimal(cPage + 2);
        }

        private void previousPage(object sender, EventArgs e)
        {
            pageUpDown.Value = Convert.ToDecimal(cPage);
        }

        private string fixString(string input, int length)
        {
            string parse = input;
            if (parse.Length > length)
                parse =
                    parse.Substring(parse.Length - length, length);

            while (parse.Length < length)
                parse = "0" + parse;

            return parse;
        }

        public uint GetAddress(int index)
        {
            return resultAddressList[cPage * pageSize + index];
        }

        public StringResult GetResult(int index)
        {
            uint resultAddress = GetAddress(index);

            int strLength;
            switch (sSize)
            {
                case (SearchSize.Bit8): strLength = 2; break;
                case (SearchSize.Bit16): strLength = 4; break;
                default: strLength = 8; break;
            }
            StringResult result;
            result.SAddress = fixString(Convert.ToString(resultAddress, 16).ToUpper(), 8);
            result.SValue = fixString(Convert.ToString(newDump.ReadAddress32(resultAddress), 16).ToUpper(), strLength);
            if (oldDump != null)
            {
                result.SOldValue = fixString(Convert.ToString(oldDump.ReadAddress32(resultAddress), 16).ToUpper(), strLength);
            }
            else
            {
                result.SOldValue = string.Empty;
            }
            return result;
        }

        public uint GetNewValueFromAddress(uint resultAddress)
        {
            return newDump.ReadAddress(resultAddress, 4);
        }

        public static uint ReadStream(Stream input, int blength)
        {
            byte[] buffer = new byte[blength];
            uint result;

            input.Read(buffer, 0, blength);

            switch (blength)
            {
                case 1: result = (uint)buffer[0]; break;
                case 2: result = (uint)ByteSwap.Swap(BitConverter.ToUInt16(buffer, 0)); break;
                default: result = ByteSwap.Swap(BitConverter.ToUInt32(buffer, 0)); break;
            }

            return result;
        }

        private void PerformBlockSearch(Dump blockDump, List<DumpRange> dumpranges)
        {
            this.blockDump = true;

            totalBlockSize = 0;
            blocksDumpedSize = 0;
            for (int i = 0; i < dumpranges.Count; i++)
                totalBlockSize += dumpranges[i].rangeLength;

            blockCount = dumpranges.Count;

            gecko.CancelDump = false;

            for (int i = 0; i < dumpranges.Count && !gecko.CancelDump; i++)
            {
                blockID = i + 1;
                blockStart = dumpranges[i].startAddress;
                blockEnd = dumpranges[i].endAddress;

                SafeDump(dumpranges[i].startAddress, dumpranges[i].endAddress, blockDump);



                blocksDumpedSize += dumpranges[i].rangeLength;
            }

            this.blockDump = false;
        }

        private List<DumpRange> FindDumpRanges(uint startAddress, byte valueLength, int lowIndex, int highIndex)
        {
            const uint blockSize = 0x3E000;

            List<DumpRange> dumpranges = new List<DumpRange>();

            uint lastAddress;

            if (resultAddressList.Count > 0)
            {
                lastAddress = resultAddressList[lowIndex];
            }
            else
            {
                lastAddress = startAddress;
            }

            DumpRange addRange = new DumpRange(lastAddress);
            addRange.streamOffset = lastAddress - startAddress;

            for (int i = lowIndex + 1; i <= highIndex; i++)
            {
                if (resultAddressList[i] >= lastAddress + blockSize)
                {
                    addRange.endAddress = lastAddress + valueLength;
                    addRange.rangeLength =
                        addRange.endAddress - addRange.startAddress;
                    dumpranges.Add(addRange);
                    lastAddress = resultAddressList[i];
                    addRange = new DumpRange(lastAddress);
                    addRange.streamOffset = lastAddress - startAddress;
                }
                lastAddress = resultAddressList[i];
            }
            addRange.endAddress = lastAddress + valueLength;
            addRange.rangeLength =
                addRange.endAddress - addRange.startAddress;
            dumpranges.Add(addRange);
            return dumpranges;
        }

        private bool Compare(uint given, uint loExpected, uint hiExpected, bool useHigh,
            ComparisonType cType, uint diffBy, bool floatCompare)
        {
            if (floatCompare)
            {
                float givenSingle = GlobalFunctions.UIntToSingle(given),
                    loExpectedSingle = GlobalFunctions.UIntToSingle(loExpected),
                    diffBySingle = GlobalFunctions.UIntToSingle(diffBy);
                if (float.IsNaN(givenSingle) || float.IsNaN(loExpectedSingle) || float.IsNaN(diffBySingle))
                {
                    return false;
                }

                switch (cType)
                {
                    case ComparisonType.Equal: return (givenSingle == loExpectedSingle);
                    case ComparisonType.NotEqual: return (givenSingle != loExpectedSingle);
                    case ComparisonType.Greater: return (givenSingle > loExpectedSingle);
                    case ComparisonType.GreaterEqual: return (givenSingle >= loExpectedSingle);
                    case ComparisonType.Lower: return (givenSingle < loExpectedSingle);
                    case ComparisonType.LowerEqual: return (givenSingle <= loExpectedSingle);
                    case ComparisonType.DifferentBy: return (loExpectedSingle - diffBySingle == givenSingle || loExpectedSingle + diffBySingle == givenSingle);
                    case ComparisonType.DifferentByLess: return (loExpectedSingle - diffBySingle < givenSingle && givenSingle < loExpectedSingle + diffBySingle);
                    case ComparisonType.DifferentByMore: return (givenSingle < loExpectedSingle - diffBySingle || givenSingle > loExpectedSingle + diffBySingle);
                    default: return (givenSingle == loExpectedSingle);
                }
            }
            else if (useHigh)
            {
                switch (cType)
                {
                    case ComparisonType.Equal: return (given >= loExpected && given <= hiExpected);
                    case ComparisonType.NotEqual: return (given < loExpected || given > hiExpected);
                    case ComparisonType.Greater: return (given > hiExpected);
                    case ComparisonType.GreaterEqual: return (given >= hiExpected);
                    case ComparisonType.Lower: return (given < loExpected);
                    case ComparisonType.LowerEqual: return (given <= loExpected);
                    default: return (given >= loExpected && given <= hiExpected);
                }
            }
            else
            {
                switch (cType)
                {
                    case ComparisonType.Equal: return (given == loExpected);
                    case ComparisonType.NotEqual: return (given != loExpected);
                    case ComparisonType.Greater: return (given > loExpected);
                    case ComparisonType.GreaterEqual: return (given >= loExpected);
                    case ComparisonType.Lower: return (given < loExpected);
                    case ComparisonType.LowerEqual: return (given <= loExpected);
                    case ComparisonType.DifferentBy: return (loExpected - diffBy == given || loExpected + diffBy == given);
                    case ComparisonType.DifferentByLess: return (loExpected - diffBy < given && given < loExpected + diffBy);
                    case ComparisonType.DifferentByMore: return (given < loExpected - diffBy || given > loExpected + diffBy);
                    default: return (given == loExpected);
                }
            }
        }

        private bool CompareRefactored(uint newDumpVal, uint oldDumpVal, uint UndoDumpVal, List<SearchComparisonInfo> comparisons, bool floatCompare)
        {
            bool success = true;
            int others = 0;
            int GT = 0;
            int LT = 0;
            bool reverseGTLT = false;
            uint GTValue = 0, LTValue = 0;
            foreach (SearchComparisonInfo comp in comparisons)
            {
                uint LHS = newDumpVal;
                uint RHS = comp.value;

                SearchType sType = comp.searchType;

                if (sType == SearchType.Unknown)
                {
                    RHS = oldDumpVal;
                }
                else if (sType == SearchType.Old)
                {
                    RHS = UndoDumpVal;
                }
                else if (sType == SearchType.Diff)
                {
                    LHS = newDumpVal - oldDumpVal;
                }

                success = CompareRefactored(LHS, RHS, comp.comparisonType, comp.value, floatCompare);

                if (comp.comparisonType == ComparisonType.Equal)
                {
                    if (success) return true;
                }
                else if (comp.comparisonType == ComparisonType.GreaterEqual || comp.comparisonType == ComparisonType.Greater)
                {
                    GTValue = comp.value;

                    if (success) GT = 1;
                    else GT = -1;

                    if (LT != 0 && GTValue > LTValue)
                    {
                        reverseGTLT = true;
                    }
                }
                else if (comp.comparisonType == ComparisonType.Lower || comp.comparisonType == ComparisonType.LowerEqual)
                {
                    LTValue = comp.value;

                    if (success) LT = 1;
                    else LT = -1;

                    if (GT != 0 && GTValue > LTValue)
                    {
                        reverseGTLT = true;
                    }
                }
                else
                {
                    if (others != -1 && success) others = 1;
                    else others = -1;
                }
            }

            if (others < 0) return false;

            if (LT > 0 || GT > 0)
            {
                if (reverseGTLT)
                {
                    return true;
                }
                else
                {
                    return LT > -1 && GT > -1;
                }
            }

            if (LT < 0 || GT < 0) return false;

            return (others > 0);
        }

        private bool CompareRefactored(uint given, uint loExpected, ComparisonType cType, uint diffBy, bool floatCompare)
        {
            if (floatCompare)
            {
                float givenSingle = GlobalFunctions.UIntToSingle(given),
                    loExpectedSingle = GlobalFunctions.UIntToSingle(loExpected),
                    diffBySingle = GlobalFunctions.UIntToSingle(diffBy);
                if (float.IsNaN(givenSingle) || float.IsNaN(loExpectedSingle) || float.IsNaN(diffBySingle))
                {
                    return false;
                }

                switch (cType)
                {
                    case ComparisonType.Equal: return (givenSingle == loExpectedSingle);
                    case ComparisonType.NotEqual: return (givenSingle != loExpectedSingle);
                    case ComparisonType.Greater: return (givenSingle > loExpectedSingle);
                    case ComparisonType.GreaterEqual: return (givenSingle >= loExpectedSingle);
                    case ComparisonType.Lower: return (givenSingle < loExpectedSingle);
                    case ComparisonType.LowerEqual: return (givenSingle <= loExpectedSingle);
                    case ComparisonType.DifferentBy: return (loExpectedSingle - diffBySingle == givenSingle || loExpectedSingle + diffBySingle == givenSingle);
                    case ComparisonType.DifferentByLess: return (loExpectedSingle - diffBySingle < givenSingle && givenSingle < loExpectedSingle + diffBySingle);
                    case ComparisonType.DifferentByMore: return (givenSingle < loExpectedSingle - diffBySingle || givenSingle > loExpectedSingle + diffBySingle);
                    default: return (givenSingle == loExpectedSingle);
                }
            }
            else
            {
                switch (cType)
                {
                    case ComparisonType.Equal: return (given == loExpected);
                    case ComparisonType.NotEqual: return (given != loExpected);
                    case ComparisonType.Greater: return (given > loExpected);
                    case ComparisonType.GreaterEqual: return (given >= loExpected);
                    case ComparisonType.Lower: return (given < loExpected);
                    case ComparisonType.LowerEqual: return (given <= loExpected);
                    case ComparisonType.DifferentBy: return (loExpected - diffBy == given || loExpected + diffBy == given);
                    case ComparisonType.DifferentByLess: return (loExpected - diffBy < given && given < loExpected + diffBy);
                    case ComparisonType.DifferentByMore: return (given < loExpected - diffBy || given > loExpected + diffBy);
                    default: return (given == loExpected);
                }
            }
        }

        private void FindPairs(uint sAddress, uint eAddress, byte valSize, out uint firstAddress, out uint lastAddress, out int firstAddressIndex, out int lastAddressIndex)
        {
            firstAddress = sAddress;
            lastAddress = eAddress;
            firstAddressIndex = 0;
            lastAddressIndex = resultAddressList.Count - 1;
            for (int i = 0; i < resultAddressList.Count; i++)
            {
                if (sAddress <= resultAddressList[i])
                {
                    firstAddress = resultAddressList[i];
                    firstAddressIndex = i;
                    break;
                }
            }
            for (int i = resultAddressList.Count - 1; i >= 0; i--)
            {
                if (eAddress >= resultAddressList[i] + valSize)
                {
                    lastAddress = resultAddressList[i] + valSize;
                    lastAddressIndex = i;
                    break;
                }
            }
        }

        public void Reset()
        {
            NewSearch = true;
            InitialSearch = false;
            nxButton.Enabled = false;
            prvButton.Enabled = false;
            resLab.Text = string.Empty;
            resultAddressList.Clear();
            undoList.Clear();
            gView.Rows.Clear();
            if (newDump != null)
            {
                newDump = null;
            }
            if (oldDump != null)
            {
                oldDump = null;
            }
            if (undoDump != null)
            {
                undoDump = null;
            }

            dumpNum = 0;
        }

        public bool Search(uint sAddress, uint eAddress, uint lValue, uint hValue,
            bool useHValue, SearchType sType, SearchSize sSize, ComparisonType cType,
            uint differentBy)
        {
            blockDump = false;

            resLab.Text = "Searching";
            byte bufferlength = 0;

            switch (sSize)
            {
                case (SearchSize.Bit8): bufferlength = 1; break;
                case (SearchSize.Bit16): bufferlength = 2; break;
                default: bufferlength = 4; break;
            }

            bool floatCompare = sSize == SearchSize.Single;

            int oldSortedColumn = 0;
            SortOrder oldSortOrder = SortOrder.Ascending;
            SearchResultComparer comparer = new SearchResultComparer();
            if (gView.SortedColumn != null)
            {
                oldSortedColumn = gView.SortedColumn.Index;
                oldSortOrder = gView.SortOrder;
            }
            if (oldSortedColumn != 0 || oldSortOrder != SortOrder.Ascending)
            {
                comparer.sortedColumn = 0;
                comparer.descending = false;
                resultAddressList.Sort(comparer);
            }

            this.sSize = sSize;

            bool doBlockSearch = false;
            bool doCompare = false;

            Dump searchDump = newDump;
            uint dumpStart, dumpEnd, dumpOffset;

            dumpStart = sAddress;
            dumpEnd = eAddress;
            dumpOffset = 0;

            if (NewSearch || (UnknownStart && sType == SearchType.Exact))
            {
                InitialSearch = true;
                dumpNum = 0;

                if (newDump != null)
                {
                    newDump = null;
                }
                resultAddressList.Clear();
                if (oldDump != null)
                {
                    oldDump = null;
                }

                if (sType == SearchType.Exact)
                {
                    doCompare = true;
                }
                else
                {
                    UnknownLAddress = sAddress;
                    UnknownHAddress = eAddress;
                    UnknownStart = true;
                    NewSearch = false;
                }
            }
            else
            {
                InitialSearch = false;
                doCompare = true;
                if (UnknownStart)
                {
                    dumpStart = Math.Max(UnknownLAddress, sAddress);
                    dumpEnd = Math.Min(UnknownHAddress, eAddress);
                    dumpOffset = dumpStart - UnknownLAddress;
                }
                else
                {
                    doBlockSearch = true;
                }
            }

            if (undoDump != null)
            {
            }
            undoDump = oldDump;
            oldDump = newDump;

            if (undoList != resultAddressList)
            {
                undoList.Clear();
            }
            undoList = resultAddressList;

            try
            {
                if (doBlockSearch)
                {
                    uint startAddress, endAddress;
                    int startAddressIndex, endAddressIndex;
                    FindPairs(sAddress, eAddress, bufferlength, out startAddress, out endAddress, out startAddressIndex, out endAddressIndex);
                    List<DumpRange> dumpRanges = FindDumpRanges(startAddress, bufferlength, startAddressIndex, endAddressIndex);
                    newDump = new Dump(startAddress, endAddress, dumpNum);
                    PerformBlockSearch(newDump, dumpRanges);
                }
                else
                {
                    newDump = new Dump(dumpStart, dumpEnd, dumpNum);
                    gecko.Dump(newDump);
                }
            }
            catch (ETCPGeckoException e)
            {
                exceptionHandling.HandleException(e);
            }

            if (doCompare)
            {
                if (sType != SearchType.Exact && sType != SearchType.Diff)
                {
                    hValue = 0;
                    useHValue = false;
                }

                uint val, cmpVal;
                cmpVal = lValue;

                if (resultAddressList.Count > 0)
                {
                    List<uint> tempAddressList = new List<uint>();
                    foreach (uint compareAddress in resultAddressList)
                    {
                        val = newDump.ReadAddress(compareAddress, bufferlength);
                        if (sType == SearchType.Unknown)
                        {
                            cmpVal = oldDump.ReadAddress(compareAddress, bufferlength);
                        }
                        else if (sType == SearchType.Old)
                        {
                            cmpVal = undoDump.ReadAddress(compareAddress, bufferlength);
                        }
                        else if (sType == SearchType.Diff)
                        {
                            val = val - oldDump.ReadAddress(compareAddress, bufferlength);
                        }

                        if (Compare(val, cmpVal, hValue, useHValue, cType, differentBy, floatCompare))
                        {
                            tempAddressList.Add(compareAddress);
                        }
                    }

                    resultAddressList = tempAddressList;
                }
                else
                {
                    for (uint i = newDump.StartAddress; i < newDump.EndAddress; i += bufferlength)
                    {
                        val = newDump.ReadAddress(i, bufferlength);
                        if (sType != SearchType.Exact)
                        {
                            cmpVal = oldDump.ReadAddress(i, bufferlength);
                        }

                        if (Compare(val, cmpVal, hValue, useHValue, cType, differentBy, floatCompare))
                        {
                            resultAddressList.Add(i);
                        }
                    }
                }
            }


            if (UnknownStart && !InitialSearch)
            {
                UnknownStart = false;
            }

            dumpNum++;


            if (resultAddressList.Count == 0 && !UnknownStart)
            {
                NewSearch = true;
                nxButton.Enabled = false;
                prvButton.Enabled = false;
                resLab.Text = "No results found";
                Reset();
                return false;
            }

            NewSearch = false;

            UpdateGridViewPage(true);

            return true;
        }

        public bool SearchRefactored(uint sAddress, uint eAddress, List<SearchComparisonInfo> comparisons, SearchSize searchSize, uint val)
        {
            blockDump = false;

            resLab.Text = "Searching";
            byte bufferlength = 0;

            switch (searchSize)
            {
                case (SearchSize.Bit8): bufferlength = 1; break;
                case (SearchSize.Bit16): bufferlength = 2; break;
                default: bufferlength = 4; break;
            }

            this.sSize = searchSize;

            bool floatCompare = searchSize == SearchSize.Single;

            int oldSortedColumn = 0;
            SortOrder oldSortOrder = SortOrder.Ascending;
            SearchResultComparer comparer = new SearchResultComparer();
            if (gView.SortedColumn != null)
            {
                oldSortedColumn = gView.SortedColumn.Index;
                oldSortOrder = gView.SortOrder;
            }
            if (oldSortedColumn != 0 || oldSortOrder != SortOrder.Ascending)
            {
                comparer.sortedColumn = 0;
                comparer.descending = false;
                resultAddressList.Sort(comparer);
            }

            SearchType sType = comparisons[0].searchType;

            bool doBlockSearch = false;
            bool doCompare = false;

            Dump searchDump = newDump;
            uint dumpStart, dumpEnd, dumpOffset;

            dumpStart = sAddress;
            dumpEnd = eAddress;
            dumpOffset = 0;

            if (NewSearch || (UnknownStart && sType == SearchType.Exact))
            {
                InitialSearch = true;
                dumpNum = 0;

                if (newDump != null)
                {
                    newDump = null;
                }
                resultAddressList.Clear();
                if (oldDump != null)
                {
                    oldDump = null;
                }

                if (sType == SearchType.Exact)
                {
                    doCompare = true;
                }
                else
                {
                    UnknownLAddress = sAddress;
                    UnknownHAddress = eAddress;
                    UnknownStart = true;
                    NewSearch = false;
                }
            }
            else
            {
                InitialSearch = false;
                doCompare = true;
                if (UnknownStart)
                {
                    dumpStart = Math.Max(UnknownLAddress, sAddress);
                    dumpEnd = Math.Min(UnknownHAddress, eAddress);
                    dumpOffset = dumpStart - UnknownLAddress;
                }
                else
                {
                    doBlockSearch = true;
                }
            }

            undoDump = oldDump;
            oldDump = newDump;
            undoList = resultAddressList;

            if (doBlockSearch)
            {
                uint startAddress, endAddress;
                int startAddressIndex, endAddressIndex;
                FindPairs(dumpStart, dumpEnd, bufferlength, out startAddress, out endAddress, out startAddressIndex, out endAddressIndex);
                List<DumpRange> dumpRanges = FindDumpRanges(startAddress, bufferlength, startAddressIndex, endAddressIndex);
                newDump = new Dump(startAddress, endAddress, dumpNum);
                PerformBlockSearch(newDump, dumpRanges);
            }
            else
            {
                newDump = new Dump(dumpStart, dumpEnd, dumpNum);
                SafeDump(dumpStart, dumpEnd, newDump);
            }

            if (doCompare)
            {
                uint cmpVal;
                cmpVal = comparisons[0].value;

                if (resultAddressList.Count > 0)
                {
                    List<uint> tempAddressList = new List<uint>();
                    foreach (uint compareAddress in resultAddressList)
                    {
                        uint newDumpVal = newDump.ReadAddress(compareAddress, bufferlength);
                        uint oldDumpVal = oldDump.ReadAddress(compareAddress, bufferlength);
                        uint UndoDumpVal;
                        if (undoDump != null)
                        {
                            UndoDumpVal = undoDump.ReadAddress(compareAddress, bufferlength);
                        }
                        else
                        {
                            UndoDumpVal = oldDumpVal;
                        }
                        if (CompareRefactored(newDumpVal, oldDumpVal, UndoDumpVal, comparisons, floatCompare))
                        {
                            tempAddressList.Add(compareAddress);
                        }
                    }

                    resultAddressList = tempAddressList;
                }
                else
                {
                    for (uint i = newDump.StartAddress; i < newDump.EndAddress; i += bufferlength)
                    {
                        uint newDumpVal = newDump.ReadAddress(i, bufferlength);
                        uint oldDumpVal = newDumpVal;
                        uint UndoDumpVal = newDumpVal;
                        if (sType != SearchType.Exact)
                        {
                            oldDumpVal = oldDump.ReadAddress(i, bufferlength);
                            UndoDumpVal = oldDumpVal;
                        }

                        if (CompareRefactored(newDumpVal, oldDumpVal, UndoDumpVal, comparisons, floatCompare))
                        {
                            resultAddressList.Add(i);
                        }
                    }
                }
            }

            if (UnknownStart && !InitialSearch)
            {
                UnknownStart = false;
            }

            dumpNum++;

            if (resultAddressList.Count == 0 && !UnknownStart)
            {
                DialogResult result = MessageBox.Show(null, "No search results!\n\nTo undo, press Yes\nTo restart, press No", "No search results!", MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation);
                bool UndoSuccess = false;
                if (result == DialogResult.Yes)
                {
                    UndoSuccess = UndoSearch();
                    if (!UndoSuccess)
                    {
                        MessageBox.Show("Could not undo!  Restarting search");
                    }
                }

                if (!UndoSuccess)
                {
                    NewSearch = true;
                    nxButton.Enabled = false;
                    prvButton.Enabled = false;
                    resLab.Text = "No results found";
                    Reset();
                    return false;
                }
            }

            NewSearch = false;

            UpdateGridViewPage(true);

            return true;
        }

        public void SafeDump(uint startdump, uint enddump, Dump memdump)
        {
            bool finished = false;
            while (!finished)
            {
                try
                {
                    gecko.Dump(startdump, enddump, memdump);
                    finished = true;
                }
                catch (ETCPGeckoException e)
                {
                    exceptionHandling.HandleException(e);
                    if (startdump == memdump.ReadCompletedAddress)
                    {
                        finished = true;
                    }
                    else
                    {
                        startdump = memdump.ReadCompletedAddress;
                    }
                }
            }
        }



        public bool UndoSearch()
        {
            if (newDump == null || oldDump == null || undoDump == null)
            {
                return false;
            }
            newDump = oldDump;
            oldDump = undoDump;
            undoDump = null;
            resultAddressList.Clear();
            resultAddressList = new List<uint>(undoList);

            UpdateGridViewPage(true);
            return true;
        }

        public bool SaveSearch(string path)
        {
            return SaveSearch(path, true);
        }

        public bool SaveSearch(string path, bool compressed)
        {
            System.Runtime.Serialization.Formatters.Binary.BinaryFormatter serializeResults = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();

            if (!compressed)
            {
                FileStream resultFile = new FileStream(path, FileMode.Create);
                serializeResults.Serialize(resultFile, sSize);
                resultFile.Close();
                return true;
            }

            ZipOutputStream resultStream = new ZipOutputStream(path);
            resultStream.CompressionLevel = Ionic.Zlib.CompressionLevel.BestSpeed;
            resultStream.PutNextEntry("ResList");

            serializeResults.Serialize(resultStream, sSize);
            resultStream.Close();
            resultStream.Dispose();
            return true;
        }

        public void LoadIndexIntoOldSearchDump(int index)
        {
            oldDump = searchHistory.LoadSearchDump(index);
        }

        public void LoadIndexIntoNewSearchDump(int index)
        {
            newDump = searchHistory.LoadSearchDump(index);
        }

        public void LoadIndexIntoSearchList(int index)
        {
            resultAddressList = searchHistory.LoadSearchList(index);
        }

        public void SaveSearchToIndex(int index)
        {
            searchHistory.SaveSearchBackground(index, resultAddressList, newDump);
        }

        public bool LoadSearchHistory(string path)
        {
            searchHistory.LoadHistory(path, out dumpNum, out sSize);
            if (dumpNum > 0)
            {
                newDump = searchHistory.LoadSearchDump(dumpNum);
                resultAddressList = searchHistory.LoadSearchList(dumpNum);
            }
            if (dumpNum > 1)
                oldDump = searchHistory.LoadSearchDump(dumpNum - 1);
            if (dumpNum > 2)
                undoDump = searchHistory.LoadSearchDump(dumpNum - 2);
            return dumpNum == 0;
        }

        public bool SaveSearchHistory(string path)
        {
            searchHistory.SaveHistory(path, dumpNum, sSize);
            return true;
        }

        public bool LoadSearch(string path, bool compressed)
        {

            int oldSortedColumn = 0;
            SortOrder oldSortOrder = SortOrder.Ascending;
            if (gView.SortedColumn != null)
            {
                oldSortedColumn = gView.SortedColumn.Index;
                oldSortOrder = gView.SortOrder;
            }

            System.Runtime.Serialization.Formatters.Binary.BinaryFormatter serializeResults = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();

            if (!compressed)
            {
                FileStream resultFile = new FileStream(path, FileMode.Open);
                sSize = (SearchSize)serializeResults.Deserialize(resultFile);
                resultFile.Close();
            }
            else
            {
                ZipFile infile = ZipFile.Read(path);
                MemoryStream resultStream = new MemoryStream();
                infile["ResList"].Extract(resultStream);
                sSize = (SearchSize)serializeResults.Deserialize(resultStream);
                infile.Dispose();
                resultStream.Close();
                resultStream.Dispose();
            }

            {
                NewSearch = true;
                nxButton.Enabled = false;
                prvButton.Enabled = false;
                resLab.Text = "No results found";
                return false;
            }
        }

        public void DeleteResults(DataGridViewSelectedRowCollection deletingCollection)
        {
            int pageOffset = cPage * pageSize;
            List<int> deletedIndices = new List<int>();

            foreach (DataGridViewRow row in gView.SelectedRows)
            {
                deletedIndices.Add(row.Index);
            }

            deletedIndices.Sort();
            deletedIndices.Reverse();

            for (int i = 0; i < deletedIndices.Count; i++)
            {
                resultAddressList.RemoveAt(pageOffset + deletedIndices[i]);
            }

            UpdateGridViewPage();
        }

        public void DeleteResult(int index)
        {
            resultAddressList.RemoveAt(cPage * pageSize + index);

            UpdateGridViewPage();
        }

        public void UpdateGridViewPage()
        {
            UpdateGridViewPage(false);
        }

        public void UpdateGridViewPage(bool ResizeGridView)
        {
            int PageCount = resultAddressList.Count / 256;
            if (resultAddressList.Count % 256 != 0) PageCount++;
            cPages = PageCount;
            pageUpDown.Maximum = Convert.ToDecimal(cPages);

            bool HadSelectedCells = gView.Rows.GetRowCount(DataGridViewElementStates.Selected) > 0;
            if (HadSelectedCells)
            {
                oldSelectedRow = Math.Min(gView.SelectedRows[0].Index, gView.SelectedRows[gView.SelectedRows.Count - 1].Index);
            }

            PrintPageAlt();
            if (HadSelectedCells && gView.Rows.Count > 0)
            {
                if (oldSelectedRow >= gView.Rows.Count)
                {
                    oldSelectedRow = gView.Rows.Count - 1;
                }

                gView.Rows[0].Selected = false;
                foreach (DataGridViewRow row in gView.SelectedRows)
                {
                    row.Selected = false;
                }

                gView.Rows[oldSelectedRow].Selected = true;

                gView.CurrentCell = gView.SelectedRows[0].Cells[0];
            }

            if (ResizeGridView)
            {
                int col1Width = gView.Columns[1].Width, col2Width = gView.Columns[2].Width, col3Width = gView.Columns[3].Width;
                gView.AutoResizeColumn(1, DataGridViewAutoSizeColumnMode.AllCells);
                gView.AutoResizeColumn(2, DataGridViewAutoSizeColumnMode.AllCells);
                gView.AutoResizeColumn(3, DataGridViewAutoSizeColumnMode.AllCells);

                gView.Columns[1].Width = Math.Max(col1Width, gView.Columns[1].Width);
                gView.Columns[2].Width = Math.Max(col2Width, gView.Columns[2].Width);
                gView.Columns[3].Width = Math.Max(col3Width, gView.Columns[3].Width);
            }

            gView.Update();
        }

        public bool CanUndo()
        {
            return undoDump != null;
        }

        public void SortResults()
        {
            SearchResultComparer comparer = new SearchResultComparer();
            comparer.sortedColumn = gView.SortedColumn.Index;
            if (gView.SortOrder == SortOrder.Descending)
            {
                comparer.descending = true;
            }

            comparer.oldDump = oldDump;
            comparer.newDump = newDump;

            resultAddressList.Sort(comparer);

            PrintPageAlt();
        }

        public class SearchResultComparer : IComparer<uint>
        {
            public int sortedColumn = 0;
            public bool descending = false;
            public Dump oldDump, newDump;
            public int Compare(uint x, uint y)
            {
                if (x == y)
                {
                    return 0;
                }
                else
                {
                    int retval = 0;
                    switch (sortedColumn)
                    {
                        case 0: retval = x.CompareTo(y); break;
                        case 1: if (oldDump != null) retval = oldDump.ReadAddress32(x).CompareTo(oldDump.ReadAddress32(y)); break;
                        case 2: if (newDump != null) retval = newDump.ReadAddress32(x).CompareTo(newDump.ReadAddress32(y)); break;
                        case 3: if (oldDump != null && newDump != null) retval = (newDump.ReadAddress32(x) - oldDump.ReadAddress32(x)).CompareTo(newDump.ReadAddress32(y) - oldDump.ReadAddress32(y)); break;
                        default: retval = 0; break;
                    }
                    if (retval == 0)
                    {
                        retval = x.CompareTo(y);
                    }

                    if (descending)
                    {
                        retval *= -1;
                    }
                    return retval;
                }
            }
        }
    }

}

