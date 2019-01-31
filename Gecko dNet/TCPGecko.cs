#define DIRECT

using Ionic.Zip;
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace TCPTCPGecko
{
    public class ByteSwap
    {
        public static UInt16 Swap(UInt16 input)
        {
            if (BitConverter.IsLittleEndian)
                return ((UInt16)(
                    ((0xFF00 & input) >> 8) |
                    ((0x00FF & input) << 8)));
            else
                return input;
        }

        public static UInt32 Swap(UInt32 input)
        {
            if (BitConverter.IsLittleEndian)
                return (
                    ((0xFF000000 & input) >> 24) |
                    ((0x00FF0000 & input) >> 8) |
                    ((0x0000FF00 & input) << 8) |
                    ((0x000000FF & input) << 24));
            else
                return input;
        }

        public static UInt64 Swap(UInt64 input)
        {
            if (BitConverter.IsLittleEndian)
                return (
                    ((0xFF00000000000000 & input) >> 56) |
                    ((0x00FF000000000000 & input) >> 40) |
                    ((0x0000FF0000000000 & input) >> 24) |
                    ((0x000000FF00000000 & input) >> 8) |
                    ((0x00000000FF000000 & input) << 8) |
                    ((0x0000000000FF0000 & input) << 24) |
                    ((0x000000000000FF00 & input) << 40) |
                    ((0x00000000000000FF & input) << 56));
            else
                return input;
        }
    }

    public class Dump
    {
        public Dump(UInt32 theStartAddress, UInt32 theEndAddress)
        {
            Construct(theStartAddress, theEndAddress, 0);
        }

        public Dump(UInt32 theStartAddress, UInt32 theEndAddress, int theFileNumber)
        {
            Construct(theStartAddress, theEndAddress, theFileNumber);
        }

        private void Construct(UInt32 theStartAddress, UInt32 theEndAddress, int theFileNumber)
        {
            StartAddress = theStartAddress;
            EndAddress = theEndAddress;
            ReadCompletedAddress = theStartAddress;
            mem = new Byte[EndAddress - StartAddress];
            fileNumber = theFileNumber;
        }


        public UInt32 ReadAddress32(UInt32 addressToRead)
        {
            if (addressToRead < StartAddress) return 0;
            if (addressToRead > EndAddress - 4) return 0;
            Byte[] buffer = new Byte[4];
            Buffer.BlockCopy(mem, index(addressToRead), buffer, 0, 4);
            UInt32 result = BitConverter.ToUInt32(buffer, 0);

            return ByteSwap.Swap(result);
        }

        private int index(UInt32 addressToRead)
        {
            return (int)(addressToRead - StartAddress);
        }

        public UInt32 ReadAddress(UInt32 addressToRead, int numBytes)
        {
            if (addressToRead < StartAddress) return 0;
            if (addressToRead > EndAddress - numBytes) return 0;

            Byte[] buffer = new Byte[4];
            Buffer.BlockCopy(mem, index(addressToRead), buffer, 0, numBytes);

            switch (numBytes)
            {
                case 4:
                    UInt32 result = BitConverter.ToUInt32(buffer, 0);

                    return ByteSwap.Swap(result);

                case 2:
                    UInt16 result16 = BitConverter.ToUInt16(buffer, 0);

                    return ByteSwap.Swap(result16);

                default:
                    return buffer[0];
            }
        }

        public void WriteStreamToDisk()
        {
            string myDirectory = Environment.CurrentDirectory + @"\searchdumps\";
            if (!Directory.Exists(myDirectory))
            {
                Directory.CreateDirectory(myDirectory);
            }
            string myFile = myDirectory + "dump" + fileNumber.ToString() + ".dmp";

            WriteStreamToDisk(myFile);
        }

        public void WriteStreamToDisk(string filepath)
        {
            FileStream foo = new FileStream(filepath, FileMode.Create);
            foo.Write(mem, 0, (int)(EndAddress - StartAddress));
            foo.Close();
            foo.Dispose();
        }

        public void WriteCompressedStreamToDisk(string filepath)
        {
            ZipFile foo = new ZipFile(filepath);
            foo.AddEntry("mem", mem);
            foo.Dispose();
        }

        public Byte[] mem;
        public UInt32 StartAddress { get; private set; }
        public UInt32 EndAddress { get; private set; }
        public UInt32 ReadCompletedAddress { get; set; }
        private int fileNumber;
    }

    public enum ETCPErrorCode
    {
        FTDIQueryError,
        noFTDIDevicesFound,
        noTCPGeckoFound,
        FTDIResetError,
        FTDIPurgeRxError,
        FTDIPurgeTxError,
        FTDITimeoutSetError,
        FTDITransferSetError,
        FTDICommandSendError,
        FTDIReadDataError,
        FTDIInvalidReply,
        TooManyRetries,
        REGStreamSizeInvalid,
        CheatStreamSizeInvalid
    }

    public enum FTDICommand
    {
        CMD_ResultError,
        CMD_FatalError,
        CMD_OK
    }

    public enum WiiStatus
    {
        Running,
        Paused,
        Breakpoint,
        Loader,
        Unknown
    }

    public enum WiiLanguage
    {
        NoOverride,
        Japanese,
        English,
        German,
        French,
        Spanish,
        Italian,
        Dutch,
        ChineseSimplified,
        ChineseTraditional,
        Korean
    }
    public enum WiiPatches
    {
        NoPatches,
        PAL60,
        VIDTV,
        PAL60VIDTV,
        NTSC,
        NTSCVIDTV,
        PAL50,
        PAL50VIDTV
    }
    public enum WiiHookType
    {
        VI,
        WiiRemote,
        GamecubePad
    }

    public delegate void GeckoProgress(UInt32 address, UInt32 currentchunk, UInt32 allchunks, UInt32 transferred, UInt32 length, bool okay, bool dump);

    public class ETCPGeckoException : Exception
    {
        public ETCPErrorCode ErrorCode { get; private set; }

        public ETCPGeckoException(ETCPErrorCode code)
            : base()
        {
            ErrorCode = code;
        }
        public ETCPGeckoException(ETCPErrorCode code, string message)
            : base(message)
        {
            ErrorCode = code;
        }
        public ETCPGeckoException(ETCPErrorCode code, string message, Exception inner)
            : base(message, inner)
        {
            ErrorCode = code;
        }
    }

    public class TCPGecko
    {
        private tcpconn PTCP;

        private const UInt32 packetsize = 0x5000;
        private const UInt32 uplpacketsize = 0x5000;

        private const Byte cmd_poke08 = 0x01;
        private const Byte cmd_poke16 = 0x02;
        private const Byte cmd_pokemem = 0x03;
        private const Byte cmd_readmem = 0x04;
        private const Byte cmd_pause = 0x06;
        private const Byte cmd_unfreeze = 0x07;
        private const Byte cmd_breakpoint = 0x09;
        private const Byte cmd_writekern = 0x0b;
        private const Byte cmd_readkern = 0x0c;
        private const Byte cmd_breakpointx = 0x10;
        private const Byte cmd_cancelbp = 0x38;
        private const Byte cmd_sendcheats = 0x40;
        private const Byte cmd_upload = 0x41;
        private const Byte cmd_hook = 0x42;
        private const Byte cmd_hookpause = 0x43;
        private const Byte cmd_step = 0x44;
        private const Byte cmd_status = 0x50;
        private const Byte cmd_cheatexec = 0x60;
        private const Byte cmd_rpc = 0x70;
        private const Byte cmd_nbreakpoint = 0x89;
        private const Byte cmd_version = 0x99;
        private const Byte cmd_os_version = 0x9A;

        private const Byte GCBPHit = 0x11;
        private const Byte GCACK = 0xAA;
        private const Byte GCRETRY = 0xBB;
        private const Byte GCFAIL = 0xCC;

        private const Byte BlockZero = 0xB0;
        private const Byte GCNgcVer = 0x81;
        private const Byte GCWiiUVer = 0x82;

        private static readonly Byte[] GCAllowedVersions = new Byte[] { GCWiiUVer };

        private const Byte BPExecute = 0x03;
        private const Byte BPRead = 0x05;
        private const Byte BPWrite = 0x06;
        private const Byte BPReadWrite = 0x07;

        private event GeckoProgress PChunkUpdate;

        public event GeckoProgress chunkUpdate
        {
            add
            {
                PChunkUpdate += value;
            }
            remove
            {
                PChunkUpdate -= value;
            }
        }

        public bool connected { get; private set; }

        public bool CancelDump { get; set; }

        public string Host
        {
            get
            {
                return PTCP.Host;
            }
            set
            {
                if (!connected)
                {
                    PTCP = new tcpconn(value, PTCP.Port);
                }
            }
        }

        public TCPGecko(string host, int port)
        {
            PTCP = new tcpconn(host, port);
            connected = false;
            PChunkUpdate = null;
        }

        ~TCPGecko()
        {
            if (connected)
                Disconnect();
        }

        protected bool InitGecko()
        {
            return true;
        }

        public bool Connect()
        {
            if (connected)
                Disconnect();

            connected = false;

            try
            {
                PTCP.Connect();
            }
            catch (IOException)
            {
                Disconnect();
                throw new ETCPGeckoException(ETCPErrorCode.noTCPGeckoFound);
            }

            if (InitGecko())
            {
                System.Threading.Thread.Sleep(150);
                connected = true;
                return true;
            }
            else
                return false;
        }

        public void Disconnect()
        {
            connected = false;
            PTCP.Close();
        }

        protected FTDICommand GeckoRead(Byte[] recbyte, UInt32 nobytes)
        {
            UInt32 bytes_read = 0;

            try
            {
                PTCP.Read(recbyte, nobytes, ref bytes_read);
            }
            catch (IOException)
            {
                Disconnect();
                return FTDICommand.CMD_FatalError;
            }
            if (bytes_read != nobytes)
            {
                return FTDICommand.CMD_ResultError;
            }

            return FTDICommand.CMD_OK;
        }

        protected FTDICommand GeckoWrite(Byte[] sendbyte, Int32 nobytes)
        {
            UInt32 bytes_written = 0;

            try
            {
                PTCP.Write(sendbyte, nobytes, ref bytes_written);
            }
            catch (IOException)
            {
                Disconnect();
                return FTDICommand.CMD_FatalError;
            }
            if (bytes_written != nobytes)
            {
                return FTDICommand.CMD_ResultError;
            }

            return FTDICommand.CMD_OK;
        }

        protected void SendUpdate(UInt32 address, UInt32 currentchunk, UInt32 allchunks, UInt32 transferred, UInt32 length, bool okay, bool dump)
        {
            if (PChunkUpdate != null)
                PChunkUpdate(address, currentchunk, allchunks, transferred, length, okay, dump);
        }

        public void Dump(Dump dump)
        {
            Dump(dump.StartAddress, dump.EndAddress, dump);
        }

        public void Dump(UInt32 startdump, UInt32 enddump, Stream saveStream)
        {
            Stream[] tempStream = { saveStream };
            Dump(startdump, enddump, tempStream);
        }

        public void Dump(UInt32 startdump, UInt32 enddump, Stream[] saveStream)
        {
            InitGecko();

            if (GeckoApp.ValidMemory.rangeCheckId(startdump) != GeckoApp.ValidMemory.rangeCheckId(enddump))
            {
                enddump = GeckoApp.ValidMemory.ValidAreas[GeckoApp.ValidMemory.rangeCheckId(startdump)].high;
            }

            if (!GeckoApp.ValidMemory.validAddress(startdump)) return;

            UInt32 memlength = enddump - startdump;

            UInt32 fullchunks = memlength / packetsize;
            UInt32 lastchunk = memlength % packetsize;

            UInt32 allchunks = fullchunks;
            if (lastchunk > 0)
                allchunks++;

            UInt64 GeckoMemRange = ByteSwap.Swap((((UInt64)startdump << 32) + ((UInt64)enddump)));
            if (GeckoWrite(BitConverter.GetBytes(cmd_readmem), 1) != FTDICommand.CMD_OK)
                throw new ETCPGeckoException(ETCPErrorCode.FTDICommandSendError);

            Byte retry = 0;
            if (GeckoWrite(BitConverter.GetBytes(GeckoMemRange), 8) != FTDICommand.CMD_OK)
                throw new ETCPGeckoException(ETCPErrorCode.FTDICommandSendError);

            UInt32 chunk = 0;
            retry = 0;

            bool done = false;
            CancelDump = false;

            Byte[] buffer = new Byte[packetsize];
            while (chunk < fullchunks && !done)
            {
                SendUpdate(startdump + chunk * packetsize, chunk, allchunks, chunk * packetsize, memlength, retry == 0, true);
                Byte[] response = new Byte[1];
                if (GeckoRead(response, 1) != FTDICommand.CMD_OK)
                {
                    GeckoWrite(BitConverter.GetBytes(GCFAIL), 1);
                    throw new ETCPGeckoException(ETCPErrorCode.FTDIReadDataError);
                }
                Byte reply = response[0];
                if (reply == BlockZero)
                {
                    for (int i = 0; i < packetsize; i++)
                    {
                        buffer[i] = 0;
                    }
                }
                else
                {
                    FTDICommand returnvalue = GeckoRead(buffer, packetsize);
                    if (returnvalue == FTDICommand.CMD_ResultError)
                    {
                        retry++;
                        if (retry >= 3)
                        {
                            GeckoWrite(BitConverter.GetBytes(GCFAIL), 1);
                            throw new ETCPGeckoException(ETCPErrorCode.TooManyRetries);
                        }
                        continue;
                    }
                    else if (returnvalue == FTDICommand.CMD_FatalError)
                    {
                        GeckoWrite(BitConverter.GetBytes(GCFAIL), 1);
                        throw new ETCPGeckoException(ETCPErrorCode.FTDIReadDataError);
                    }
                }
                foreach (Stream stream in saveStream)
                {
                    stream.Write(buffer, 0, ((Int32)packetsize));
                }

                retry = 0;
                chunk++;

                if (!CancelDump)
                {
                }
                else
                {
                    GeckoWrite(BitConverter.GetBytes(GCFAIL), 1);
                    done = true;
                }
            }

            while (!done && lastchunk > 0)
            {
                SendUpdate(startdump + chunk * packetsize, chunk, allchunks, chunk * packetsize, memlength, retry == 0, true);
                Byte[] response = new Byte[1];
                if (GeckoRead(response, 1) != FTDICommand.CMD_OK)
                {
                    GeckoWrite(BitConverter.GetBytes(GCFAIL), 1);
                    throw new ETCPGeckoException(ETCPErrorCode.FTDIReadDataError);
                }
                Byte reply = response[0];
                if (reply == BlockZero)
                {
                    for (int i = 0; i < lastchunk; i++)
                    {
                        buffer[i] = 0;
                    }
                }
                else
                {
                    FTDICommand returnvalue = GeckoRead(buffer, lastchunk);
                    if (returnvalue == FTDICommand.CMD_ResultError)
                    {
                        retry++;
                        if (retry >= 3)
                        {
                            GeckoWrite(BitConverter.GetBytes(GCFAIL), 1);
                            throw new ETCPGeckoException(ETCPErrorCode.TooManyRetries);
                        }
                        continue;
                    }
                    else if (returnvalue == FTDICommand.CMD_FatalError)
                    {
                        GeckoWrite(BitConverter.GetBytes(GCFAIL), 1);
                        throw new ETCPGeckoException(ETCPErrorCode.FTDIReadDataError);
                    }
                }
                foreach (Stream stream in saveStream)
                {
                    stream.Write(buffer, 0, ((Int32)lastchunk));
                }
                retry = 0;
                done = true;
            }
            SendUpdate(enddump, allchunks, allchunks, memlength, memlength, true, true);
        }


        public void Dump(UInt32 startdump, UInt32 enddump, Dump memdump)
        {
            InitGecko();

            UInt32 memlength = enddump - startdump;

            UInt32 fullchunks = memlength / packetsize;
            UInt32 lastchunk = memlength % packetsize;

            UInt32 allchunks = fullchunks;
            if (lastchunk > 0)
                allchunks++;

            UInt64 GeckoMemRange = ByteSwap.Swap((((UInt64)startdump << 32) + ((UInt64)enddump)));
            if (GeckoWrite(BitConverter.GetBytes(cmd_readmem), 1) != FTDICommand.CMD_OK)
                throw new ETCPGeckoException(ETCPErrorCode.FTDICommandSendError);

            Byte retry = 0;
            if (GeckoWrite(BitConverter.GetBytes(GeckoMemRange), 8) != FTDICommand.CMD_OK)
                throw new ETCPGeckoException(ETCPErrorCode.FTDICommandSendError);

            UInt32 chunk = 0;
            retry = 0;

            bool done = false;
            CancelDump = false;

            Byte[] buffer = new Byte[packetsize];
            while (chunk < fullchunks && !done)
            {
                SendUpdate(startdump + chunk * packetsize, chunk, allchunks, chunk * packetsize, memlength, retry == 0, true);
                Byte[] response = new Byte[1];
                if (GeckoRead(response, 1) != FTDICommand.CMD_OK)
                {
                    GeckoWrite(BitConverter.GetBytes(GCFAIL), 1);
                    throw new ETCPGeckoException(ETCPErrorCode.FTDIReadDataError);
                }
                Byte reply = response[0];
                if (reply == BlockZero)
                {
                    for (int i = 0; i < packetsize; i++)
                    {
                        buffer[i] = 0;
                    }
                }
                else
                {
                    FTDICommand returnvalue = GeckoRead(buffer, packetsize);
                    if (returnvalue == FTDICommand.CMD_ResultError)
                    {
                        retry++;
                        if (retry >= 3)
                        {
                            GeckoWrite(BitConverter.GetBytes(GCFAIL), 1);
                            throw new ETCPGeckoException(ETCPErrorCode.TooManyRetries);
                        }
                        continue;
                    }
                    else if (returnvalue == FTDICommand.CMD_FatalError)
                    {
                        GeckoWrite(BitConverter.GetBytes(GCFAIL), 1);
                        throw new ETCPGeckoException(ETCPErrorCode.FTDIReadDataError);
                    }
                }
                Buffer.BlockCopy(buffer, 0, memdump.mem, (int)(chunk * packetsize + (startdump - memdump.StartAddress)), (int)packetsize);

                memdump.ReadCompletedAddress = ((chunk + 1) * packetsize + startdump);

                retry = 0;
                chunk++;

                if (!CancelDump)
                {
                }
                else
                {
                    GeckoWrite(BitConverter.GetBytes(GCFAIL), 1);
                    done = true;
                }
            }

            while (!done && lastchunk > 0)
            {
                SendUpdate(startdump + chunk * packetsize, chunk, allchunks, chunk * packetsize, memlength, retry == 0, true);
                Byte[] response = new Byte[1];
                if (GeckoRead(response, 1) != FTDICommand.CMD_OK)
                {
                    GeckoWrite(BitConverter.GetBytes(GCFAIL), 1);
                    throw new ETCPGeckoException(ETCPErrorCode.FTDIReadDataError);
                }
                Byte reply = response[0];
                if (reply == BlockZero)
                {
                    for (int i = 0; i < lastchunk; i++)
                    {
                        buffer[i] = 0;
                    }
                }
                else
                {
                    FTDICommand returnvalue = GeckoRead(buffer, lastchunk);
                    if (returnvalue == FTDICommand.CMD_ResultError)
                    {
                        retry++;
                        if (retry >= 3)
                        {
                            GeckoWrite(BitConverter.GetBytes(GCFAIL), 1);
                            throw new ETCPGeckoException(ETCPErrorCode.TooManyRetries);
                        }
                        continue;
                    }
                    else if (returnvalue == FTDICommand.CMD_FatalError)
                    {
                        GeckoWrite(BitConverter.GetBytes(GCFAIL), 1);
                        throw new ETCPGeckoException(ETCPErrorCode.FTDIReadDataError);
                    }
                }
                Buffer.BlockCopy(buffer, 0, memdump.mem, (int)(chunk * packetsize + (startdump - memdump.StartAddress)), (int)lastchunk);


                retry = 0;
                done = true;
            }
            SendUpdate(enddump, allchunks, allchunks, memlength, memlength, true, true);
        }

        public void Upload(UInt32 startupload, UInt32 endupload, Stream sendStream)
        {
            InitGecko();

            UInt32 memlength = endupload - startupload;

            UInt32 fullchunks = memlength / uplpacketsize;
            UInt32 lastchunk = memlength % uplpacketsize;

            UInt32 allchunks = fullchunks;
            if (lastchunk > 0)
                allchunks++;

            UInt64 GeckoMemRange = ByteSwap.Swap((((UInt64)startupload << 32) + ((UInt64)endupload)));
            if (GeckoWrite(BitConverter.GetBytes(cmd_upload), 1) != FTDICommand.CMD_OK)
                throw new ETCPGeckoException(ETCPErrorCode.FTDICommandSendError);

            Byte retry = 0;
            if (GeckoWrite(BitConverter.GetBytes(GeckoMemRange), 8) != FTDICommand.CMD_OK)
                throw new ETCPGeckoException(ETCPErrorCode.FTDICommandSendError);

            UInt32 chunk = 0;
            retry = 0;

            Byte[] buffer;
            while (chunk < fullchunks)
            {
                SendUpdate(startupload + chunk * packetsize, chunk, allchunks, chunk * packetsize, memlength, retry == 0, false);
                buffer = new Byte[uplpacketsize];
                sendStream.Read(buffer, 0, (int)uplpacketsize);
                FTDICommand returnvalue = GeckoWrite(buffer, (int)uplpacketsize);
                if (returnvalue == FTDICommand.CMD_ResultError)
                {
                    retry++;
                    if (retry >= 3)
                    {
                        Disconnect();
                        throw new ETCPGeckoException(ETCPErrorCode.TooManyRetries);
                    }
                    sendStream.Seek((-1) * ((int)uplpacketsize), SeekOrigin.Current);
                    continue;
                }
                else if (returnvalue == FTDICommand.CMD_FatalError)
                {
                    Disconnect();
                    throw new ETCPGeckoException(ETCPErrorCode.FTDIReadDataError);
                }
                retry = 0;
                chunk++;
            }

            while (lastchunk > 0)
            {
                SendUpdate(startupload + chunk * packetsize, chunk, allchunks, chunk * packetsize, memlength, retry == 0, false);
                buffer = new Byte[lastchunk];
                sendStream.Read(buffer, 0, (int)lastchunk);
                FTDICommand returnvalue = GeckoWrite(buffer, (int)lastchunk);
                if (returnvalue == FTDICommand.CMD_ResultError)
                {
                    retry++;
                    if (retry >= 3)
                    {
                        Disconnect();
                        throw new ETCPGeckoException(ETCPErrorCode.TooManyRetries);
                    }
                    sendStream.Seek((-1) * ((int)lastchunk), SeekOrigin.Current);
                    continue;
                }
                else if (returnvalue == FTDICommand.CMD_FatalError)
                {
                    Disconnect();
                    throw new ETCPGeckoException(ETCPErrorCode.FTDIReadDataError);
                }
                retry = 0;
                lastchunk = 0;
            }

            Byte[] response = new Byte[1];
            if (GeckoRead(response, 1) != FTDICommand.CMD_OK)
                throw new ETCPGeckoException(ETCPErrorCode.FTDIReadDataError);
            Byte reply = response[0];
            if (reply != GCACK)
                throw new ETCPGeckoException(ETCPErrorCode.FTDIInvalidReply);
            SendUpdate(endupload, allchunks, allchunks, memlength, memlength, true, false);
        }

        public bool Reconnect()
        {
            Disconnect();
            try
            {
                return Connect();
            }
            catch
            {
                return false;
            }
        }

        public FTDICommand RawCommand(Byte id)
        {
            return GeckoWrite(BitConverter.GetBytes(id), 1);
        }

        public void Pause()
        {
            if (RawCommand(cmd_pause) != FTDICommand.CMD_OK)
                throw new ETCPGeckoException(ETCPErrorCode.FTDICommandSendError);
        }

        public void SafePause()
        {
            bool WasRunning = (status() == WiiStatus.Running);
            while (WasRunning)
            {
                Pause();
                System.Threading.Thread.Sleep(100);
                WasRunning = (status() == WiiStatus.Running);
            }
        }

        public void Resume()
        {
            if (RawCommand(cmd_unfreeze) != FTDICommand.CMD_OK)
                throw new ETCPGeckoException(ETCPErrorCode.FTDICommandSendError);
        }

        public void SafeResume()
        {
            bool NotRunning = (status() != WiiStatus.Running);
            int failCounter = 0;
            while (NotRunning && failCounter < 10)
            {
                Resume();
                System.Threading.Thread.Sleep(100);
                try
                {
                    NotRunning = (status() != WiiStatus.Running);
                }
                catch (TCPTCPGecko.ETCPGeckoException ex)
                {
                    NotRunning = true;
                    failCounter++;
                }
            }
        }

        public void sendfail()
        {
            RawCommand(GCFAIL);
        }

        public void poke(UInt32 address, UInt32 value)
        {
            address &= 0xFFFFFFFC;

            UInt64 PokeVal = (((UInt64)address) << 32) | ((UInt64)value);

            PokeVal = ByteSwap.Swap(PokeVal);

            if (RawCommand(cmd_pokemem) != FTDICommand.CMD_OK)
                throw new ETCPGeckoException(ETCPErrorCode.FTDICommandSendError);

            if (GeckoWrite(BitConverter.GetBytes(PokeVal), 8) != FTDICommand.CMD_OK)
                throw new ETCPGeckoException(ETCPErrorCode.FTDICommandSendError);
        }

        public void poke32(UInt32 address, UInt32 value)
        {
            poke(address, value);
        }

        public void poke16(UInt32 address, UInt16 value)
        {
            address &= 0xFFFFFFFE;

            UInt64 PokeVal = (((UInt64)address) << 32) | ((UInt64)value);

            PokeVal = ByteSwap.Swap(PokeVal);

            if (RawCommand(cmd_poke16) != FTDICommand.CMD_OK)
                throw new ETCPGeckoException(ETCPErrorCode.FTDICommandSendError);

            if (GeckoWrite(BitConverter.GetBytes(PokeVal), 8) != FTDICommand.CMD_OK)
                throw new ETCPGeckoException(ETCPErrorCode.FTDICommandSendError);
        }

        public void poke08(UInt32 address, Byte value)
        {
            UInt64 PokeVal = (((UInt64)address) << 32) | ((UInt64)value);

            PokeVal = ByteSwap.Swap(PokeVal);

            if (RawCommand(cmd_poke08) != FTDICommand.CMD_OK)
                throw new ETCPGeckoException(ETCPErrorCode.FTDICommandSendError);

            if (GeckoWrite(BitConverter.GetBytes(PokeVal), 8) != FTDICommand.CMD_OK)
                throw new ETCPGeckoException(ETCPErrorCode.FTDICommandSendError);
        }

        public void poke_kern(UInt32 address, UInt32 value)
        {
            UInt64 PokeVal = (((UInt64)address) << 32) | ((UInt64)value);

            PokeVal = ByteSwap.Swap(PokeVal);

            if (RawCommand(cmd_writekern) != FTDICommand.CMD_OK)
                throw new ETCPGeckoException(ETCPErrorCode.FTDICommandSendError);

            if (GeckoWrite(BitConverter.GetBytes(PokeVal), 8) != FTDICommand.CMD_OK)
                throw new ETCPGeckoException(ETCPErrorCode.FTDICommandSendError);
        }

        public UInt32 peek_kern(UInt32 address)
        {
            address = ByteSwap.Swap(address);

            if (RawCommand(cmd_readkern) != FTDICommand.CMD_OK)
                throw new ETCPGeckoException(ETCPErrorCode.FTDICommandSendError);

            if (GeckoWrite(BitConverter.GetBytes(address), 4) != FTDICommand.CMD_OK)
                throw new ETCPGeckoException(ETCPErrorCode.FTDICommandSendError);

            Byte[] buffer = new Byte[4];
            if (GeckoRead(buffer, 4) != FTDICommand.CMD_OK)
                throw new ETCPGeckoException(ETCPErrorCode.FTDICommandSendError);

            return ByteSwap.Swap(BitConverter.ToUInt32(buffer, 0));
        }

        public WiiStatus status()
        {
            System.Threading.Thread.Sleep(100);
            if (!InitGecko())
                throw new ETCPGeckoException(ETCPErrorCode.FTDIResetError);

            if (RawCommand(cmd_status) != FTDICommand.CMD_OK)
                throw new ETCPGeckoException(ETCPErrorCode.FTDICommandSendError);

            Byte[] buffer = new Byte[1];
            if (GeckoRead(buffer, 1) != FTDICommand.CMD_OK)
                throw new ETCPGeckoException(ETCPErrorCode.FTDIReadDataError);

            switch (buffer[0])
            {
                case 0: return WiiStatus.Running;
                case 1: return WiiStatus.Paused;
                case 2: return WiiStatus.Breakpoint;
                case 3: return WiiStatus.Loader;
                default: return WiiStatus.Unknown;
            }
        }

        public void Step()
        {
            if (!InitGecko())
                throw new ETCPGeckoException(ETCPErrorCode.FTDIResetError);

            if (RawCommand(cmd_step) != FTDICommand.CMD_OK)
                throw new ETCPGeckoException(ETCPErrorCode.FTDICommandSendError);
        }

        protected void Breakpoint(UInt32 address, Byte bptype, bool exact)
        {
            InitGecko();

            UInt32 lowaddr = (address & 0xFFFFFFF8) | bptype;
            bool useGeckoBP = false;
            if (exact)
                useGeckoBP = (VersionRequest() != GCNgcVer);

            if (!useGeckoBP)
            {
                if (RawCommand(cmd_breakpoint) != FTDICommand.CMD_OK)
                    throw new ETCPGeckoException(ETCPErrorCode.FTDICommandSendError);

                UInt32 breakpaddr = ByteSwap.Swap(lowaddr);

                if (GeckoWrite(BitConverter.GetBytes(breakpaddr), 4) != FTDICommand.CMD_OK)
                    throw new ETCPGeckoException(ETCPErrorCode.FTDICommandSendError);
            }
            else
            {
                if (RawCommand(cmd_nbreakpoint) != FTDICommand.CMD_OK)
                    throw new ETCPGeckoException(ETCPErrorCode.FTDICommandSendError);

                UInt64 breakpaddr = ((UInt64)lowaddr) << 32 | ((UInt64)address);
                breakpaddr = ByteSwap.Swap(breakpaddr);

                if (GeckoWrite(BitConverter.GetBytes(breakpaddr), 8) != FTDICommand.CMD_OK)
                    throw new ETCPGeckoException(ETCPErrorCode.FTDICommandSendError);
            }
        }

        public void BreakpointR(UInt32 address, bool exact)
        {
            Breakpoint(address, BPRead, exact);
        }
        public void BreakpointR(UInt32 address)
        {
            Breakpoint(address, BPRead, true);
        }

        public void BreakpointW(UInt32 address, bool exact)
        {
            Breakpoint(address, BPWrite, exact);
        }
        public void BreakpointW(UInt32 address)
        {
            Breakpoint(address, BPWrite, true);
        }

        public void BreakpointRW(UInt32 address, bool exact)
        {
            Breakpoint(address, BPReadWrite, exact);
        }
        public void BreakpointRW(UInt32 address)
        {
            Breakpoint(address, BPReadWrite, true);
        }


        public void BreakpointX(UInt32 address)
        {
            InitGecko();

            UInt32 baddress = ByteSwap.Swap(((address & 0xFFFFFFFC) | BPExecute));

            if (RawCommand(cmd_breakpointx) != FTDICommand.CMD_OK)
                throw new ETCPGeckoException(ETCPErrorCode.FTDICommandSendError);

            if (GeckoWrite(BitConverter.GetBytes(baddress), 4) != FTDICommand.CMD_OK)
                throw new ETCPGeckoException(ETCPErrorCode.FTDICommandSendError);
        }

        public bool BreakpointHit()
        {
            Byte[] buffer = new Byte[1];

            if (GeckoRead(buffer, 1) != FTDICommand.CMD_OK)
                return false;

            return (buffer[0] == GCBPHit);
        }

        public void CancelBreakpoint()
        {
            if (RawCommand(cmd_cancelbp) != FTDICommand.CMD_OK)
                throw new ETCPGeckoException(ETCPErrorCode.FTDICommandSendError);
        }

        protected bool AllowedVersion(Byte version)
        {
            for (int i = 0; i < GCAllowedVersions.Length; i++)
                if (GCAllowedVersions[i] == version)
                    return true;
            return false;
        }

        public Byte VersionRequest()
        {
            InitGecko();

            if (RawCommand(cmd_version) != FTDICommand.CMD_OK)
                throw new ETCPGeckoException(ETCPErrorCode.FTDICommandSendError);

            Byte retries = 0;
            Byte result = 0;
            Byte[] buffer = new Byte[1];

            do
            {
                if (GeckoRead(buffer, 1) == FTDICommand.CMD_OK)
                {
                    if (AllowedVersion(buffer[0]))
                    {
                        result = buffer[0];
                        break;
                    }
                }
                retries++;
            } while (retries < 3);

            return result;
        }

        public UInt32 OsVersionRequest()
        {
            if (RawCommand(cmd_os_version) != FTDICommand.CMD_OK)
                throw new ETCPGeckoException(ETCPErrorCode.FTDICommandSendError);

            Byte[] buffer = new Byte[4];

            if (GeckoRead(buffer, 4) != FTDICommand.CMD_OK)
                throw new ETCPGeckoException(ETCPErrorCode.FTDICommandSendError);

            return ByteSwap.Swap(BitConverter.ToUInt32(buffer, 0));
        }

        public UInt32 peek(UInt32 address)
        {
            if (!GeckoApp.ValidMemory.validAddress(address))
            {
                return 0;
            }

            UInt32 paddress = address & 0xFFFFFFFC;

            MemoryStream stream = new MemoryStream();

            GeckoProgress oldUpdate = PChunkUpdate;
            PChunkUpdate = null;

            try
            {

                Dump(paddress, paddress + 4, stream);

                stream.Seek(0, SeekOrigin.Begin);
                Byte[] buffer = new Byte[4];
                stream.Read(buffer, 0, 4);

                UInt32 result = BitConverter.ToUInt32(buffer, 0);

                result = ByteSwap.Swap(result);

                return result;
            }
            finally
            {
                PChunkUpdate = oldUpdate;

                stream.Close();
            }
        }

        public void GetRegisters(Stream stream, uint contextAddress)
        {
            UInt32 bytesExpected = 0x1B0;

            MemoryStream buffer = new MemoryStream();
            Dump(contextAddress + 8, contextAddress + 8 + bytesExpected, buffer);

            byte[] bytes = buffer.ToArray();

            stream.Write(bytes, 0x80, 4);
            stream.Write(bytes, 0x8c, 4);
            stream.Write(bytes, 0x88, 4);
            stream.Write(new byte[8], 0, 8);
            stream.Write(bytes, 0x90, 8);
            stream.Write(bytes, 0x0, 4 * 32);
            stream.Write(bytes, 0x84, 4);
            stream.Write(bytes, 0xb0, 8 * 32);
        }

        public void SendRegisters(Stream sendStream, uint contextAddress)
        {
            MemoryStream buffer = new MemoryStream();
            byte[] bytes = new byte[0xA0];
            sendStream.Seek(0, SeekOrigin.Begin);
            sendStream.Read(bytes, 0, bytes.Length);
            buffer.Write(bytes, 0x1C, 4 * 32);
            buffer.Write(bytes, 0x0, 4);
            buffer.Write(bytes, 0x9C, 4);
            buffer.Write(bytes, 0x8, 4);
            buffer.Write(bytes, 0x4, 4);
            buffer.Write(bytes, 0x14, 8);

            buffer.Seek(0, SeekOrigin.Begin);

            Upload(contextAddress + 8, contextAddress + 8 + 0x98, buffer);
        }

        private UInt64 readInt64(Stream inputstream)
        {
            Byte[] buffer = new Byte[8];
            inputstream.Read(buffer, 0, 8);
            UInt64 result = BitConverter.ToUInt64(buffer, 0);
            result = ByteSwap.Swap(result);
            return result;
        }

        private void writeInt64(Stream outputstream, UInt64 value)
        {
            UInt64 bvalue = ByteSwap.Swap(value);
            Byte[] buffer = BitConverter.GetBytes(bvalue);
            outputstream.Write(buffer, 0, 8);
        }

        private void insertInto(Stream insertStream, UInt64 value)
        {
            MemoryStream tempstream = new MemoryStream();
            writeInt64(tempstream, value);
            insertStream.Seek(0, SeekOrigin.Begin);

            Byte[] streambuffer = new Byte[insertStream.Length];
            insertStream.Read(streambuffer, 0, (Int32)insertStream.Length);
            tempstream.Write(streambuffer, 0, (Int32)insertStream.Length);

            insertStream.Seek(0, SeekOrigin.Begin);
            tempstream.Seek(0, SeekOrigin.Begin);

            streambuffer = new Byte[tempstream.Length];
            tempstream.Read(streambuffer, 0, (Int32)tempstream.Length);
            insertStream.Write(streambuffer, 0, (Int32)tempstream.Length);

            tempstream.Close();
        }

        public void sendCheats(Stream inputStream)
        {
            MemoryStream cheatStream = new MemoryStream();
            Byte[] orgData = new Byte[inputStream.Length];
            inputStream.Seek(0, SeekOrigin.Begin);
            inputStream.Read(orgData, 0, (Int32)inputStream.Length);
            cheatStream.Write(orgData, 0, (Int32)inputStream.Length);

            UInt32 length = (UInt32)cheatStream.Length;
            if (length % 8 != 0)
            {
                cheatStream.Close();
                throw new ETCPGeckoException(ETCPErrorCode.CheatStreamSizeInvalid);
            }

            InitGecko();

            cheatStream.Seek(-8, SeekOrigin.End);
            UInt64 data = readInt64(cheatStream);
            data = data & 0xFE00000000000000;
            if ((data != 0xF000000000000000) &&
                 (data != 0xFE00000000000000))
            {
                cheatStream.Seek(0, SeekOrigin.End);
                writeInt64(cheatStream, 0xF000000000000000);
            }

            cheatStream.Seek(0, SeekOrigin.Begin);
            data = readInt64(cheatStream);
            if (data != 0x00D0C0DE00D0C0DE)
            {
                insertInto(cheatStream, 0x00D0C0DE00D0C0DE);
            }

            cheatStream.Seek(0, SeekOrigin.Begin);

            length = (UInt32)cheatStream.Length;

            if (GeckoWrite(BitConverter.GetBytes(cmd_sendcheats), 1) != FTDICommand.CMD_OK)
            {
                cheatStream.Close();
                throw new ETCPGeckoException(ETCPErrorCode.FTDICommandSendError);
            }

            UInt32 fullchunks = length / uplpacketsize;
            UInt32 lastchunk = length % uplpacketsize;

            UInt32 allchunks = fullchunks;
            if (lastchunk > 0)
                allchunks++;

            Byte retry = 0;
            while (retry < 10)
            {
                Byte[] response = new Byte[1];
                if (GeckoRead(response, 1) != FTDICommand.CMD_OK)
                {
                    cheatStream.Close();
                    throw new ETCPGeckoException(ETCPErrorCode.FTDIReadDataError);
                }
                Byte reply = response[0];
                if (reply == GCACK)
                    break;
                if (retry == 9)
                {
                    cheatStream.Close();
                    throw new ETCPGeckoException(ETCPErrorCode.FTDIInvalidReply);
                }
            }

            UInt32 blength = ByteSwap.Swap(length);
            if (GeckoWrite(BitConverter.GetBytes(blength), 4) != FTDICommand.CMD_OK)
            {
                cheatStream.Close();
                throw new ETCPGeckoException(ETCPErrorCode.FTDICommandSendError);
            }

            UInt32 chunk = 0;
            retry = 0;

            Byte[] buffer;
            while (chunk < fullchunks)
            {
                SendUpdate(0x00d0c0de, chunk, allchunks, chunk * packetsize, length, retry == 0, false);
                buffer = new Byte[uplpacketsize];
                cheatStream.Read(buffer, 0, (int)uplpacketsize);
                FTDICommand returnvalue = GeckoWrite(buffer, (int)uplpacketsize);
                if (returnvalue == FTDICommand.CMD_ResultError)
                {
                    retry++;
                    if (retry >= 3)
                    {
                        GeckoWrite(BitConverter.GetBytes(GCFAIL), 1);
                        cheatStream.Close();
                        throw new ETCPGeckoException(ETCPErrorCode.TooManyRetries);
                    }
                    cheatStream.Seek((-1) * ((int)uplpacketsize), SeekOrigin.Current);
                    GeckoWrite(BitConverter.GetBytes(GCRETRY), 1);
                    continue;
                }
                else if (returnvalue == FTDICommand.CMD_FatalError)
                {
                    GeckoWrite(BitConverter.GetBytes(GCFAIL), 1);
                    cheatStream.Close();
                    throw new ETCPGeckoException(ETCPErrorCode.FTDIReadDataError);
                }

                Byte[] response = new Byte[1];
                returnvalue = GeckoRead(response, 1);
                if ((returnvalue == FTDICommand.CMD_ResultError) || (response[0] != GCACK))
                {
                    retry++;
                    if (retry >= 3)
                    {
                        GeckoWrite(BitConverter.GetBytes(GCFAIL), 1);
                        cheatStream.Close();
                        throw new ETCPGeckoException(ETCPErrorCode.TooManyRetries);
                    }
                    cheatStream.Seek((-1) * ((int)uplpacketsize), SeekOrigin.Current);
                    GeckoWrite(BitConverter.GetBytes(GCRETRY), 1);
                    continue;
                }
                else if (returnvalue == FTDICommand.CMD_FatalError)
                {
                    GeckoWrite(BitConverter.GetBytes(GCFAIL), 1);
                    cheatStream.Close();
                    throw new ETCPGeckoException(ETCPErrorCode.FTDIReadDataError);
                }

                retry = 0;
                chunk++;
            }

            while (lastchunk > 0)
            {
                SendUpdate(0x00d0c0de, chunk, allchunks, chunk * packetsize, length, retry == 0, false);
                buffer = new Byte[lastchunk];
                cheatStream.Read(buffer, 0, (int)lastchunk);
                FTDICommand returnvalue = GeckoWrite(buffer, (Int32)lastchunk);
                if (returnvalue == FTDICommand.CMD_ResultError)
                {
                    retry++;
                    if (retry >= 3)
                    {
                        GeckoWrite(BitConverter.GetBytes(GCFAIL), 1);
                        cheatStream.Close();
                        throw new ETCPGeckoException(ETCPErrorCode.TooManyRetries);
                    }
                    cheatStream.Seek((-1) * ((int)lastchunk), SeekOrigin.Current);
                    GeckoWrite(BitConverter.GetBytes(GCRETRY), 1);
                    continue;
                }
                else if (returnvalue == FTDICommand.CMD_FatalError)
                {
                    GeckoWrite(BitConverter.GetBytes(GCFAIL), 1);
                    cheatStream.Close();
                    throw new ETCPGeckoException(ETCPErrorCode.FTDIReadDataError);
                }
                retry = 0;
                lastchunk = 0;
            }
            SendUpdate(0x00d0c0de, allchunks, allchunks, length, length, true, false);
            cheatStream.Close();
        }

        public void ExecuteCheats()
        {
            if (RawCommand(cmd_cheatexec) != FTDICommand.CMD_OK)
                throw new ETCPGeckoException(ETCPErrorCode.FTDICommandSendError);
        }

        public void Hook(bool pause, WiiLanguage language, WiiPatches patches, WiiHookType hookType)
        {
            InitGecko();

            Byte command;
            if (pause)
                command = cmd_hookpause;
            else
                command = cmd_hook;

            command += (Byte)hookType;
            if (RawCommand(command) != FTDICommand.CMD_OK)
                throw new ETCPGeckoException(ETCPErrorCode.FTDICommandSendError);

            if (language != WiiLanguage.NoOverride)
                command = (Byte)(language - 1);
            else
                command = 0xCD;

            if (RawCommand(command) != FTDICommand.CMD_OK)
                throw new ETCPGeckoException(ETCPErrorCode.FTDICommandSendError);

            command = (Byte)patches;
            if (RawCommand(command) != FTDICommand.CMD_OK)
                throw new ETCPGeckoException(ETCPErrorCode.FTDICommandSendError);
        }

        public void Hook()
        {
            Hook(false, WiiLanguage.NoOverride, WiiPatches.NoPatches, WiiHookType.VI);
        }

        private static Byte ConvertSafely(double floatValue)
        {
            return (Byte)Math.Round(Math.Max(0, Math.Min(floatValue, 255)));
        }

        private static Bitmap ProcessImage(UInt32 width, UInt32 height, Stream analyze)
        {

            Bitmap BitmapRGB = new Bitmap((int)width, (int)height, PixelFormat.Format24bppRgb);
            BitmapData bData = BitmapRGB.LockBits(new Rectangle(0, 0, (int)width, (int)height),
                                ImageLockMode.ReadWrite, PixelFormat.Format24bppRgb);

            int size = bData.Stride * bData.Height;

            Byte[] data = new Byte[size];

            System.Runtime.InteropServices.Marshal.Copy(bData.Scan0, data, 0, size);

            Byte[] bufferBytes = new Byte[width * height * 2];

            int y = 0;
            int u = 0;
            int v = 0;
            int yvpos = 0;
            int rgbpos = 0;

            analyze.Read(bufferBytes, 0, (int)(width * height * 2));
            for (int i = 0; i < width * height; i++)
            {
                yvpos = i * 2;
                if (i % 2 == 0)
                {
                    y = bufferBytes[yvpos];
                    u = bufferBytes[yvpos + 1];
                    v = bufferBytes[yvpos + 3];
                }
                else
                    y = bufferBytes[yvpos];
                rgbpos = (i * 3);
                data[rgbpos] = ConvertSafely(1.164 * (y - 16) + 2.017 * (u - 128));
                data[rgbpos + 1] = ConvertSafely(1.164 * (y - 16) - 0.392 * (u - 128) - 0.813 * (v - 128));
                data[rgbpos + 2] = ConvertSafely(1.164 * (y - 16) + 1.596 * (v - 128));
            }

            System.Runtime.InteropServices.Marshal.Copy(data, 0, bData.Scan0, data.Length);

            BitmapRGB.UnlockBits(bData);

            return BitmapRGB;
        }

        public Image Screenshot()
        {
            MemoryStream analyze;

            analyze = new MemoryStream();
            Dump(0xCC002000, 0xCC002080, analyze);
            analyze.Seek(0, SeekOrigin.Begin);
            Byte[] viregs = new Byte[128];
            analyze.Read(viregs, 0, 128);
            analyze.Close();

            UInt32 swidth = (UInt32)(viregs[0x49] << 3);
            UInt32 sheight = (UInt32)(((viregs[0] << 5) | (viregs[1] >> 3)) & 0x07FE);
            UInt32 soffset = (UInt32)((viregs[0x1D] << 16) | (viregs[0x1E] << 8) | viregs[0x1F]);
            if ((viregs[0x1C] & 0x10) == 0x10)
                soffset <<= 5;
            soffset += 0x80000000;
            soffset -= (UInt32)((viregs[0x1C] & 0xF) << 3);

            analyze = new MemoryStream();
            Dump(soffset, soffset + sheight * swidth * 2, analyze);
            analyze.Seek(0, SeekOrigin.Begin);

            if (sheight > 600)
            {
                sheight = sheight / 2;
                swidth = swidth * 2;
            }

            Bitmap b = ProcessImage(swidth, sheight, analyze);
            analyze.Close();

            return b;
        }


        public UInt32 rpc(UInt32 address, params UInt32[] args)
        {
            return (UInt32)(rpc64(address, args) >> 32);
        }

        public UInt64 rpc64(UInt32 address, params UInt32[] args)
        {
            Byte[] buffer = new Byte[4 + 8 * 4];

            address = ByteSwap.Swap(address);

            BitConverter.GetBytes(address).CopyTo(buffer, 0);

            for (int i = 0; i < 8; i++)
            {
                if (i < args.Length)
                {
                    BitConverter.GetBytes(ByteSwap.Swap(args[i])).CopyTo(buffer, 4 + i * 4);
                }
                else
                {
                    BitConverter.GetBytes(0xfecad0ba).CopyTo(buffer, 4 + i * 4);
                }
            }

            if (RawCommand(cmd_rpc) != FTDICommand.CMD_OK)
                throw new ETCPGeckoException(ETCPErrorCode.FTDICommandSendError);


            if (GeckoWrite(buffer, buffer.Length) != FTDICommand.CMD_OK)
                throw new ETCPGeckoException(ETCPErrorCode.FTDICommandSendError);

            if (GeckoRead(buffer, 8) != FTDICommand.CMD_OK)
                throw new ETCPGeckoException(ETCPErrorCode.FTDICommandSendError);

            return ByteSwap.Swap(BitConverter.ToUInt64(buffer, 0));
        }

    }
}