using Ionic.Zip;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading;
using TCPTCPGecko;

namespace GeckoApp
{
    public struct SearchItem
    {
        public List<uint> resultsList;
        public Dump searchDump;
        public int index;
    }

    public class SearchHistoryManager
    {
        public bool BackgroundWriting { get; private set; }

        public SearchHistoryManager()
        {
            BackgroundWriting = false;
        }

        public void SaveSearchBackground(int index, List<uint> resultsList, Dump searchDump)
        {
            SearchItem foo = new SearchItem();
            foo.resultsList = new List<uint>(resultsList);
            foo.searchDump = searchDump;
            foo.index = index;

            while (BackgroundWriting) ;

            Thread zipThread = new Thread(new ParameterizedThreadStart(SaveSearchBackground));

            BackgroundWriting = true;

            zipThread.Start(foo);
        }

        public void SaveSearchBackground(object searchItem)
        {
            SearchItem foo = (SearchItem)searchItem;

            SaveSearch(foo.index, foo.resultsList, foo.searchDump);

            BackgroundWriting = false;
        }

        public void SaveHistory(string path, int DumpNum, SearchSize size)
        {
            using (ZipFile zip = new ZipFile())
            {
                zip.CompressionLevel = Ionic.Zlib.CompressionLevel.None;
                zip.AddDirectory("DumpHistory");
                zip.Comment = DumpNum.ToString() + ":" + size.ToString();

                zip.Save(path);
            }
        }

        public void LoadHistory(string path, out int DumpNum, out SearchSize size)
        {
            int retVal;
            using (ZipFile zip = ZipFile.Read(path))
            {
                foreach (ZipEntry e in zip)
                {
                    e.Extract("DumpHistory", ExtractExistingFileAction.OverwriteSilently);
                }
                string comment = zip.Comment;
                string[] split = comment.Split(':');
                DumpNum = Convert.ToInt32(split[0]);
                switch (split[1])
                {
                    case "Bit16": size = SearchSize.Bit16; break;
                    case "Bit8": size = SearchSize.Bit8; break;
                    case "Single": size = SearchSize.Single; break;
                    case "Bit32": size = SearchSize.Bit32; break;
                    default: size = SearchSize.Bit32; break;
                }
            }
        }

        public void SaveSearch(int index, List<uint> resultsList, Dump searchDump)
        {
            char delim = Path.DirectorySeparatorChar;
            SaveSearch("DumpHistory" + delim + "DumpHistory" + index + ".zip", resultsList, searchDump);
        }

        public void SaveSearch(string filepath, List<uint> resultsList, Dump searchDump)
        {
            ZipOutputStream outstream = new ZipOutputStream(filepath);
            outstream.CompressionLevel = Ionic.Zlib.CompressionLevel.BestSpeed;
            BinaryFormatter formatter = new BinaryFormatter();

            outstream.PutNextEntry("dump");

            formatter.Serialize(outstream, searchDump.StartAddress);
            formatter.Serialize(outstream, searchDump.EndAddress);
            outstream.Write(searchDump.mem, 0, (int)(searchDump.EndAddress - searchDump.StartAddress));

            outstream.PutNextEntry("list");

            formatter.Serialize(outstream, resultsList);

            outstream.Close();
            outstream.Dispose();
        }

        public Dump LoadSearchDump(int index)
        {
            char delim = Path.DirectorySeparatorChar;
            return LoadSearchDump("DumpHistory" + delim + "DumpHistory" + index + ".zip");
        }

        public Dump LoadSearchDump(string filepath)
        {
            while (BackgroundWriting) ;

            ZipInputStream instream = new ZipInputStream(filepath);
            BinaryFormatter formatter = new BinaryFormatter();

            instream.GetNextEntry();
            Dump searchDump = new Dump((uint)formatter.Deserialize(instream), (uint)formatter.Deserialize(instream));
            instream.Read(searchDump.mem, 0, (int)(searchDump.EndAddress - searchDump.StartAddress));

            instream.Close();
            instream.Dispose();

            return searchDump;
        }

        public List<uint> LoadSearchList(int index)
        {
            char delim = Path.DirectorySeparatorChar;
            return LoadSearchList("DumpHistory" + delim + "DumpHistory" + index + ".zip");
        }

        public List<uint> LoadSearchList(string filepath)
        {
            while (BackgroundWriting) ;

            ZipInputStream instream = new ZipInputStream(filepath);
            BinaryFormatter formatter = new BinaryFormatter();

            instream.GetNextEntry();

            instream.GetNextEntry();

            List<uint> searchList = (List<uint>)formatter.Deserialize(instream);

            instream.Close();
            instream.Dispose();

            return searchList;
        }
    }

    public class SearchHistoryItem
    {
        private List<uint> resultsList;
        private Dump searchDump;


        public SearchHistoryItem()
        {
            resultsList = null;
            searchDump = null;
            BackgroundWriting = false;
        }

        public bool BackgroundWriting { get; private set; }

        public void WriteCompressedZipBackground(string filepath)
        {
            Thread zipThread = new Thread(new ParameterizedThreadStart(WriteCompressedZipBackgroundObj));

            BackgroundWriting = true;

            zipThread.Start(filepath);
        }

        public void WriteCompressedZipBackgroundObj(object filepath)
        {
            string path = filepath as string;

            WriteCompressedZip(path);
            BackgroundWriting = false;
        }

        public void WriteCompressedZip(string filepath)
        {
            ZipOutputStream outstream = new ZipOutputStream(filepath);
            outstream.CompressionLevel = Ionic.Zlib.CompressionLevel.BestSpeed;
            BinaryFormatter formatter = new BinaryFormatter();
            outstream.PutNextEntry("dump");


            DateTime startTime = DateTime.Now;

            formatter.Serialize(outstream, searchDump.StartAddress);
            formatter.Serialize(outstream, searchDump.EndAddress);
            outstream.Write(searchDump.mem, 0, (int)(searchDump.EndAddress - searchDump.StartAddress));

            DateTime endTime = DateTime.Now;
            outstream.PutNextEntry("list");

            startTime = DateTime.Now;

            List<uint> copy = new List<uint>(resultsList);

            endTime = DateTime.Now;
            startTime = DateTime.Now;

            formatter.Serialize(outstream, resultsList);

            endTime = DateTime.Now;
            outstream.Close();
            outstream.Dispose();
        }

        public void ReadCompressedZip(string filepath)
        {
            while (BackgroundWriting) ;

            ZipInputStream instream = new ZipInputStream(filepath);
            BinaryFormatter formatter = new BinaryFormatter();
            instream.GetNextEntry();
            searchDump = new TCPTCPGecko.Dump((uint)formatter.Deserialize(instream), (uint)formatter.Deserialize(instream));
            instream.Read(searchDump.mem, 0, (int)(searchDump.EndAddress - searchDump.StartAddress));

            instream.GetNextEntry();
            resultsList = (System.Collections.Generic.List<UInt32>)formatter.Deserialize(instream);

            instream.Close();
            instream.Dispose();
        }
    }
}
