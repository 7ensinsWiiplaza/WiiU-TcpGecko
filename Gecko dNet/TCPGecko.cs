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
        public static ushort Swap(ushort input)
        {
            if (BitConverter.IsLittleEndian)
                return ((ushort)(
                    ((0xFF00 & input) >> 8) |
                    ((0x00FF & input) << 8)));
            else
                return input;
        }

        public static uint Swap(uint input)
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

        public static ulong Swap(ulong input)
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
        public Dump(uint theStartAddress, uint theEndAddress)
        {
            Construct(theStartAddress, theEndAddress, 0);
        }

        public Dump(uint theStartAddress, uint theEndAddress, int theFileNumber)
        {
            Construct(theStartAddress, theEndAddress, theFileNumber);
        }

        private void Construct(uint theStartAddress, uint theEndAddress, int theFileNumber)
        {
            StartAddress = theStartAddress;
            EndAddress = theEndAddress;
            ReadCompletedAddress = theStartAddress;
            mem = new byte[EndAddress - StartAddress];
            fileNumber = theFileNumber;
        }


        public uint ReadAddress32(uint addressToRead)
        {
            if (addressToRead < StartAddress) return 0;
            if (addressToRead > EndAddress - 4) return 0;
            byte[] buffer = new byte[4];
            Buffer.BlockCopy(mem, index(addressToRead), buffer, 0, 4);
            uint result = BitConverter.ToUInt32(buffer, 0);

            return ByteSwap.Swap(result);
        }

        private int index(uint addressToRead)
        {
            return (int)(addressToRead - StartAddress);
        }

        public uint ReadAddress(uint addressToRead, int numBytes)
        {
            if (addressToRead < StartAddress) return 0;
            if (addressToRead > EndAddress - numBytes) return 0;

            byte[] buffer = new byte[4];
            Buffer.BlockCopy(mem, index(addressToRead), buffer, 0, numBytes);

            switch (numBytes)
            {
                case 4:
                    uint result = BitConverter.ToUInt32(buffer, 0);

                    return ByteSwap.Swap(result);

                case 2:
                    ushort result16 = BitConverter.ToUInt16(buffer, 0);

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

        public byte[] mem;
        public uint StartAddress { get; private set; }
        public uint EndAddress { get; private set; }
        public uint ReadCompletedAddress { get; set; }
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

    public delegate void GeckoProgress(uint address, uint currentchunk, uint allchunks, uint transferred, uint length, bool okay, bool dump);

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

        private const uint Maxpacketsize = 0x5000;

        private const Byte cmd_poke08 = 0x01;
        private const Byte cmd_poke16 = 0x02;
        private const Byte cmd_pokemem = 0x03;
        private const Byte cmd_readmem = 0x04;
        private const Byte cmd_pause = 0x06;
        private const Byte cmd_unfreeze = 0x07;
        private const Byte cmd_breakpoint = 0x09;
        private const Byte cmd_breakpointx = 0xa;
        private const Byte cmd_writekern = 0x0b;
        private const Byte cmd_readkern = 0x0c;
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

        protected FTDICommand GeckoRead(byte[] recbyte, uint nobytes)
        {
            uint bytes_read = 0;

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

        protected FTDICommand GeckoWrite(byte[] sendbyte, int nobytes)
        {
            uint bytes_written = 0;

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

        protected void SendUpdate(uint address, uint currentchunk, uint allchunks, uint transferred, uint length, bool okay, bool dump)
        {
            if (PChunkUpdate != null)
                PChunkUpdate(address, currentchunk, allchunks, transferred, length, okay, dump);
        }

        public void Dump(Dump dump)
        {
            Dump(dump.StartAddress, dump.EndAddress, dump);
        }

        public void Dump(uint startdump, uint enddump, Stream saveStream)
        {
            Stream[] tempStream = { saveStream };
            Dump(startdump, enddump, tempStream);
        }

        public void Dump(uint startdump, uint enddump, Stream[] saveStream)
        {
            InitGecko();

            if (GeckoApp.ValidMemory.rangeCheckId(startdump) != GeckoApp.ValidMemory.rangeCheckId(enddump))
            {
                enddump = GeckoApp.ValidMemory.ValidAreas[GeckoApp.ValidMemory.rangeCheckId(startdump)].high;
            }

            if (!GeckoApp.ValidMemory.validAddress(startdump)) return;

            uint memlength = enddump - startdump;

            uint fullchunks = memlength / Maxpacketsize;
            uint lastchunk = memlength % Maxpacketsize;

            uint allchunks = fullchunks;
            if (lastchunk > 0)
                allchunks++;

            ulong GeckoMemRange = ByteSwap.Swap((((ulong)startdump << 32) + ((ulong)enddump)));
            if (GeckoWrite(BitConverter.GetBytes(cmd_readmem), 1) != FTDICommand.CMD_OK)
                throw new ETCPGeckoException(ETCPErrorCode.FTDICommandSendError);

            byte retry = 0;
            if (GeckoWrite(BitConverter.GetBytes(GeckoMemRange), 8) != FTDICommand.CMD_OK)
                throw new ETCPGeckoException(ETCPErrorCode.FTDICommandSendError);

            uint chunk = 0;
            retry = 0;

            bool done = false;
            CancelDump = false;

            byte[] buffer = new byte[Maxpacketsize];
            while (chunk < fullchunks && !done)
            {
                SendUpdate(startdump + chunk * Maxpacketsize, chunk, allchunks, chunk * Maxpacketsize, memlength, retry == 0, true);
                byte[] response = new byte[1];
                if (GeckoRead(response, 1) != FTDICommand.CMD_OK)
                {
                    GeckoWrite(BitConverter.GetBytes(GCFAIL), 1);
                    throw new ETCPGeckoException(ETCPErrorCode.FTDIReadDataError);
                }
                byte reply = response[0];
                if (reply == BlockZero)
                {
                    for (int i = 0; i < Maxpacketsize; i++)
                    {
                        buffer[i] = 0;
                    }
                }
                else
                {
                    FTDICommand returnvalue = GeckoRead(buffer, Maxpacketsize);
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
                    stream.Write(buffer, 0, ((int)Maxpacketsize));
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
                SendUpdate(startdump + chunk * Maxpacketsize, chunk, allchunks, chunk * Maxpacketsize, memlength, retry == 0, true);
                byte[] response = new byte[1];
                if (GeckoRead(response, 1) != FTDICommand.CMD_OK)
                {
                    GeckoWrite(BitConverter.GetBytes(GCFAIL), 1);
                    throw new ETCPGeckoException(ETCPErrorCode.FTDIReadDataError);
                }
                byte reply = response[0];
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
                    stream.Write(buffer, 0, ((int)lastchunk));
                }
                retry = 0;
                done = true;
            }
            SendUpdate(enddump, allchunks, allchunks, memlength, memlength, true, true);
        }


        public void Dump(uint startdump, uint enddump, Dump memdump)
        {
            InitGecko();

            uint memlength = enddump - startdump;

            uint fullchunks = memlength / Maxpacketsize;
            uint lastchunk = memlength % Maxpacketsize;

            uint allchunks = fullchunks;
            if (lastchunk > 0)
                allchunks++;

            ulong GeckoMemRange = ByteSwap.Swap((((ulong)startdump << 32) + ((ulong)enddump)));
            if (GeckoWrite(BitConverter.GetBytes(cmd_readmem), 1) != FTDICommand.CMD_OK)
                throw new ETCPGeckoException(ETCPErrorCode.FTDICommandSendError);

            byte retry = 0;
            if (GeckoWrite(BitConverter.GetBytes(GeckoMemRange), 8) != FTDICommand.CMD_OK)
                throw new ETCPGeckoException(ETCPErrorCode.FTDICommandSendError);

            uint chunk = 0;
            retry = 0;

            bool done = false;
            CancelDump = false;

            byte[] buffer = new byte[Maxpacketsize];
            while (chunk < fullchunks && !done)
            {
                SendUpdate(startdump + chunk * Maxpacketsize, chunk, allchunks, chunk * Maxpacketsize, memlength, retry == 0, true);
                byte[] response = new byte[1];
                if (GeckoRead(response, 1) != FTDICommand.CMD_OK)
                {
                    GeckoWrite(BitConverter.GetBytes(GCFAIL), 1);
                    throw new ETCPGeckoException(ETCPErrorCode.FTDIReadDataError);
                }
                byte reply = response[0];
                if (reply == BlockZero)
                {
                    for (int i = 0; i < Maxpacketsize; i++)
                    {
                        buffer[i] = 0;
                    }
                }
                else
                {
                    FTDICommand returnvalue = GeckoRead(buffer, Maxpacketsize);
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
                Buffer.BlockCopy(buffer, 0, memdump.mem, (int)(chunk * Maxpacketsize + (startdump - memdump.StartAddress)), (int)Maxpacketsize);

                memdump.ReadCompletedAddress = ((chunk + 1) * Maxpacketsize + startdump);

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
                SendUpdate(startdump + chunk * Maxpacketsize, chunk, allchunks, chunk * Maxpacketsize, memlength, retry == 0, true);
                byte[] response = new byte[1];
                if (GeckoRead(response, 1) != FTDICommand.CMD_OK)
                {
                    GeckoWrite(BitConverter.GetBytes(GCFAIL), 1);
                    throw new ETCPGeckoException(ETCPErrorCode.FTDIReadDataError);
                }
                byte reply = response[0];
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
                Buffer.BlockCopy(buffer, 0, memdump.mem, (int)(chunk * Maxpacketsize + (startdump - memdump.StartAddress)), (int)lastchunk);


                retry = 0;
                done = true;
            }
            SendUpdate(enddump, allchunks, allchunks, memlength, memlength, true, true);
        }

        public void Upload(uint startupload, uint endupload, Stream sendStream)
        {
            InitGecko();

            uint memlength = endupload - startupload;

            uint fullchunks = memlength / Maxpacketsize;
            uint lastchunk = memlength % Maxpacketsize;

            uint allchunks = fullchunks;
            if (lastchunk > 0)
                allchunks++;

            ulong GeckoMemRange = ByteSwap.Swap((((ulong)startupload << 32) + ((ulong)endupload)));
            if (GeckoWrite(BitConverter.GetBytes(cmd_upload), 1) != FTDICommand.CMD_OK)
                throw new ETCPGeckoException(ETCPErrorCode.FTDICommandSendError);

            byte retry = 0;
            if (GeckoWrite(BitConverter.GetBytes(GeckoMemRange), 8) != FTDICommand.CMD_OK)
                throw new ETCPGeckoException(ETCPErrorCode.FTDICommandSendError);

            uint chunk = 0;
            retry = 0;

            byte[] buffer;
            while (chunk < fullchunks)
            {
                SendUpdate(startupload + chunk * Maxpacketsize, chunk, allchunks, chunk * Maxpacketsize, memlength, retry == 0, false);
                buffer = new byte[Maxpacketsize];
                sendStream.Read(buffer, 0, (int)Maxpacketsize);
                FTDICommand returnvalue = GeckoWrite(buffer, (int)Maxpacketsize);
                if (returnvalue == FTDICommand.CMD_ResultError)
                {
                    retry++;
                    if (retry >= 3)
                    {
                        Disconnect();
                        throw new ETCPGeckoException(ETCPErrorCode.TooManyRetries);
                    }
                    sendStream.Seek((-1) * ((int)Maxpacketsize), SeekOrigin.Current);
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
                SendUpdate(startupload + chunk * Maxpacketsize, chunk, allchunks, chunk * Maxpacketsize, memlength, retry == 0, false);
                buffer = new byte[lastchunk];
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

            byte[] response = new byte[1];
            if (GeckoRead(response, 1) != FTDICommand.CMD_OK)
                throw new ETCPGeckoException(ETCPErrorCode.FTDIReadDataError);
            byte reply = response[0];
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

        public FTDICommand RawCommand(byte id)
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

        public void poke(uint address, uint value)
        {
            address &= 0xFFFFFFFC;

            ulong PokeVal = (((ulong)address) << 32) | ((ulong)value);

            PokeVal = ByteSwap.Swap(PokeVal);

            if (RawCommand(cmd_pokemem) != FTDICommand.CMD_OK)
                throw new ETCPGeckoException(ETCPErrorCode.FTDICommandSendError);

            if (GeckoWrite(BitConverter.GetBytes(PokeVal), 8) != FTDICommand.CMD_OK)
                throw new ETCPGeckoException(ETCPErrorCode.FTDICommandSendError);
        }

        public void poke32(uint address, uint value)
        {
            poke(address, value);
        }

        public void poke16(uint address, ushort value)
        {
            address &= 0xFFFFFFFE;

            ulong PokeVal = (((ulong)address) << 32) | ((ulong)value);

            PokeVal = ByteSwap.Swap(PokeVal);

            if (RawCommand(cmd_poke16) != FTDICommand.CMD_OK)
                throw new ETCPGeckoException(ETCPErrorCode.FTDICommandSendError);

            if (GeckoWrite(BitConverter.GetBytes(PokeVal), 8) != FTDICommand.CMD_OK)
                throw new ETCPGeckoException(ETCPErrorCode.FTDICommandSendError);
        }

        public void poke08(uint address, byte value)
        {
            ulong PokeVal = (((ulong)address) << 32) | ((ulong)value);

            PokeVal = ByteSwap.Swap(PokeVal);

            if (RawCommand(cmd_poke08) != FTDICommand.CMD_OK)
                throw new ETCPGeckoException(ETCPErrorCode.FTDICommandSendError);

            if (GeckoWrite(BitConverter.GetBytes(PokeVal), 8) != FTDICommand.CMD_OK)
                throw new ETCPGeckoException(ETCPErrorCode.FTDICommandSendError);
        }

        public void poke_kern(uint address, uint value)
        {
            ulong PokeVal = (((ulong)address) << 32) | ((ulong)value);

            PokeVal = ByteSwap.Swap(PokeVal);

            if (RawCommand(cmd_writekern) != FTDICommand.CMD_OK)
                throw new ETCPGeckoException(ETCPErrorCode.FTDICommandSendError);

            if (GeckoWrite(BitConverter.GetBytes(PokeVal), 8) != FTDICommand.CMD_OK)
                throw new ETCPGeckoException(ETCPErrorCode.FTDICommandSendError);
        }

        public uint peek_kern(uint address)
        {
            address = ByteSwap.Swap(address);

            if (RawCommand(cmd_readkern) != FTDICommand.CMD_OK)
                throw new ETCPGeckoException(ETCPErrorCode.FTDICommandSendError);

            if (GeckoWrite(BitConverter.GetBytes(address), 4) != FTDICommand.CMD_OK)
                throw new ETCPGeckoException(ETCPErrorCode.FTDICommandSendError);

            byte[] buffer = new byte[4];
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

            byte[] buffer = new byte[1];
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

        protected void Breakpoint(uint address, byte bptype, bool exact)
        {
            InitGecko();

            uint lowaddr = (address & 0xFFFFFFF8) | bptype;
            bool useGeckoBP = false;
            if (exact)
                useGeckoBP = (VersionRequest() != GCNgcVer);

            if (!useGeckoBP)
            {
                if (RawCommand(cmd_breakpoint) != FTDICommand.CMD_OK)
                    throw new ETCPGeckoException(ETCPErrorCode.FTDICommandSendError);

                uint breakpaddr = ByteSwap.Swap(lowaddr);

                if (GeckoWrite(BitConverter.GetBytes(breakpaddr), 4) != FTDICommand.CMD_OK)
                    throw new ETCPGeckoException(ETCPErrorCode.FTDICommandSendError);
            }
            else
            {
                if (RawCommand(cmd_nbreakpoint) != FTDICommand.CMD_OK)
                    throw new ETCPGeckoException(ETCPErrorCode.FTDICommandSendError);

                ulong breakpaddr = ((ulong)lowaddr) << 32 | ((ulong)address);
                breakpaddr = ByteSwap.Swap(breakpaddr);

                if (GeckoWrite(BitConverter.GetBytes(breakpaddr), 8) != FTDICommand.CMD_OK)
                    throw new ETCPGeckoException(ETCPErrorCode.FTDICommandSendError);
            }
        }

        public void BreakpointR(uint address, bool exact)
        {
            Breakpoint(address, BPRead, exact);
        }
        public void BreakpointR(uint address)
        {
            Breakpoint(address, BPRead, true);
        }

        public void BreakpointW(uint address, bool exact)
        {
            Breakpoint(address, BPWrite, exact);
        }
        public void BreakpointW(uint address)
        {
            Breakpoint(address, BPWrite, true);
        }

        public void BreakpointRW(uint address, bool exact)
        {
            Breakpoint(address, BPReadWrite, exact);
        }
        public void BreakpointRW(uint address)
        {
            Breakpoint(address, BPReadWrite, true);
        }


        public void BreakpointX(uint address)
        {
            InitGecko();

            uint baddress = ByteSwap.Swap(((address & 0xFFFFFFFC) | BPExecute));

            if (RawCommand(cmd_breakpointx) != FTDICommand.CMD_OK)
                throw new ETCPGeckoException(ETCPErrorCode.FTDICommandSendError);

            if (GeckoWrite(BitConverter.GetBytes(baddress), 4) != FTDICommand.CMD_OK)
                throw new ETCPGeckoException(ETCPErrorCode.FTDICommandSendError);
        }

        public bool BreakpointHit()
        {
            byte[] buffer = new byte[1];

            if (GeckoRead(buffer, 1) != FTDICommand.CMD_OK)
                return false;

            return (buffer[0] == GCBPHit);
        }

        public void CancelBreakpoint()
        {
            if (RawCommand(cmd_cancelbp) != FTDICommand.CMD_OK)
                throw new ETCPGeckoException(ETCPErrorCode.FTDICommandSendError);
        }

        protected bool AllowedVersion(byte version)
        {
            for (int i = 0; i < GCAllowedVersions.Length; i++)
                if (GCAllowedVersions[i] == version)
                    return true;
            return false;
        }

        public byte VersionRequest()
        {
            InitGecko();

            if (RawCommand(cmd_version) != FTDICommand.CMD_OK)
                throw new ETCPGeckoException(ETCPErrorCode.FTDICommandSendError);

            byte retries = 0;
            byte result = 0;
            byte[] buffer = new byte[1];

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

        public uint OsVersionRequest()
        {
            if (RawCommand(cmd_os_version) != FTDICommand.CMD_OK)
                throw new ETCPGeckoException(ETCPErrorCode.FTDICommandSendError);

            byte[] buffer = new byte[4];

            if (GeckoRead(buffer, 4) != FTDICommand.CMD_OK)
                throw new ETCPGeckoException(ETCPErrorCode.FTDICommandSendError);

            return ByteSwap.Swap(BitConverter.ToUInt32(buffer, 0));
        }

        public uint peek(uint address)
        {
            if (!GeckoApp.ValidMemory.validAddress(address))
            {
                return 0;
            }

            uint paddress = address & 0xFFFFFFFC;

            MemoryStream stream = new MemoryStream();

            GeckoProgress oldUpdate = PChunkUpdate;
            PChunkUpdate = null;

            try
            {

                Dump(paddress, paddress + 4, stream);

                stream.Seek(0, SeekOrigin.Begin);
                byte[] buffer = new byte[4];
                stream.Read(buffer, 0, 4);

                uint result = BitConverter.ToUInt32(buffer, 0);

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
            uint bytesExpected = 0x1B0;

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

        private ulong readInt64(Stream inputstream)
        {
            byte[] buffer = new byte[8];
            inputstream.Read(buffer, 0, 8);
            ulong result = BitConverter.ToUInt64(buffer, 0);
            result = ByteSwap.Swap(result);
            return result;
        }

        private void writeInt64(Stream outputstream, ulong value)
        {
            ulong bvalue = ByteSwap.Swap(value);
            byte[] buffer = BitConverter.GetBytes(bvalue);
            outputstream.Write(buffer, 0, 8);
        }

        private void insertInto(Stream insertStream, ulong value)
        {
            MemoryStream tempstream = new MemoryStream();
            writeInt64(tempstream, value);
            insertStream.Seek(0, SeekOrigin.Begin);

            byte[] streambuffer = new byte[insertStream.Length];
            insertStream.Read(streambuffer, 0, (int)insertStream.Length);
            tempstream.Write(streambuffer, 0, (int)insertStream.Length);

            insertStream.Seek(0, SeekOrigin.Begin);
            tempstream.Seek(0, SeekOrigin.Begin);

            streambuffer = new byte[tempstream.Length];
            tempstream.Read(streambuffer, 0, (int)tempstream.Length);
            insertStream.Write(streambuffer, 0, (int)tempstream.Length);

            tempstream.Close();
        }

        public void sendCheats(Stream inputStream)
        {
            MemoryStream cheatStream = new MemoryStream();
            byte[] orgData = new byte[inputStream.Length];
            inputStream.Seek(0, SeekOrigin.Begin);
            inputStream.Read(orgData, 0, (int)inputStream.Length);
            cheatStream.Write(orgData, 0, (int)inputStream.Length);

            uint length = (uint)cheatStream.Length;
            if (length % 8 != 0)
            {
                cheatStream.Close();
                throw new ETCPGeckoException(ETCPErrorCode.CheatStreamSizeInvalid);
            }

            InitGecko();

            cheatStream.Seek(-8, SeekOrigin.End);
            ulong data = readInt64(cheatStream);
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

            length = (uint)cheatStream.Length;

            if (GeckoWrite(BitConverter.GetBytes(cmd_sendcheats), 1) != FTDICommand.CMD_OK)
            {
                cheatStream.Close();
                throw new ETCPGeckoException(ETCPErrorCode.FTDICommandSendError);
            }

            uint fullchunks = length / Maxpacketsize;
            uint lastchunk = length % Maxpacketsize;

            uint allchunks = fullchunks;
            if (lastchunk > 0)
                allchunks++;

            byte retry = 0;
            while (retry < 10)
            {
                byte[] response = new byte[1];
                if (GeckoRead(response, 1) != FTDICommand.CMD_OK)
                {
                    cheatStream.Close();
                    throw new ETCPGeckoException(ETCPErrorCode.FTDIReadDataError);
                }
                byte reply = response[0];
                if (reply == GCACK)
                    break;
                if (retry == 9)
                {
                    cheatStream.Close();
                    throw new ETCPGeckoException(ETCPErrorCode.FTDIInvalidReply);
                }
            }

            uint blength = ByteSwap.Swap(length);
            if (GeckoWrite(BitConverter.GetBytes(blength), 4) != FTDICommand.CMD_OK)
            {
                cheatStream.Close();
                throw new ETCPGeckoException(ETCPErrorCode.FTDICommandSendError);
            }

            uint chunk = 0;
            retry = 0;

            byte[] buffer;
            while (chunk < fullchunks)
            {
                SendUpdate(0x00d0c0de, chunk, allchunks, chunk * Maxpacketsize, length, retry == 0, false);
                buffer = new byte[Maxpacketsize];
                cheatStream.Read(buffer, 0, (int)Maxpacketsize);
                FTDICommand returnvalue = GeckoWrite(buffer, (int)Maxpacketsize);
                if (returnvalue == FTDICommand.CMD_ResultError)
                {
                    retry++;
                    if (retry >= 3)
                    {
                        GeckoWrite(BitConverter.GetBytes(GCFAIL), 1);
                        cheatStream.Close();
                        throw new ETCPGeckoException(ETCPErrorCode.TooManyRetries);
                    }
                    cheatStream.Seek((-1) * ((int)Maxpacketsize), SeekOrigin.Current);
                    GeckoWrite(BitConverter.GetBytes(GCRETRY), 1);
                    continue;
                }
                else if (returnvalue == FTDICommand.CMD_FatalError)
                {
                    GeckoWrite(BitConverter.GetBytes(GCFAIL), 1);
                    cheatStream.Close();
                    throw new ETCPGeckoException(ETCPErrorCode.FTDIReadDataError);
                }

                byte[] response = new byte[1];
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
                    cheatStream.Seek((-1) * ((int)Maxpacketsize), SeekOrigin.Current);
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
                SendUpdate(0x00d0c0de, chunk, allchunks, chunk * Maxpacketsize, length, retry == 0, false);
                buffer = new byte[lastchunk];
                cheatStream.Read(buffer, 0, (int)lastchunk);
                FTDICommand returnvalue = GeckoWrite(buffer, (int)lastchunk);
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

            byte command;
            if (pause)
                command = cmd_hookpause;
            else
                command = cmd_hook;

            command += (byte)hookType;
            if (RawCommand(command) != FTDICommand.CMD_OK)
                throw new ETCPGeckoException(ETCPErrorCode.FTDICommandSendError);

            if (language != WiiLanguage.NoOverride)
                command = (byte)(language - 1);
            else
                command = 0xCD;

            if (RawCommand(command) != FTDICommand.CMD_OK)
                throw new ETCPGeckoException(ETCPErrorCode.FTDICommandSendError);

            command = (byte)patches;
            if (RawCommand(command) != FTDICommand.CMD_OK)
                throw new ETCPGeckoException(ETCPErrorCode.FTDICommandSendError);
        }

        public void Hook()
        {
            Hook(false, WiiLanguage.NoOverride, WiiPatches.NoPatches, WiiHookType.VI);
        }

        private static byte ConvertSafely(double floatValue)
        {
            return (byte)Math.Round(Math.Max(0, Math.Min(floatValue, 255)));
        }

        private static Bitmap ProcessImage(uint width, uint height, Stream analyze)
        {

            Bitmap BitmapRGB = new Bitmap((int)width, (int)height, PixelFormat.Format24bppRgb);
            BitmapData bData = BitmapRGB.LockBits(new Rectangle(0, 0, (int)width, (int)height),
                                ImageLockMode.ReadWrite, PixelFormat.Format24bppRgb);

            int size = bData.Stride * bData.Height;

            byte[] data = new byte[size];

            System.Runtime.InteropServices.Marshal.Copy(bData.Scan0, data, 0, size);

            byte[] bufferBytes = new byte[width * height * 2];

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
            byte[] viregs = new byte[128];
            analyze.Read(viregs, 0, 128);
            analyze.Close();

            uint swidth = (uint)(viregs[0x49] << 3);
            uint sheight = (uint)(((viregs[0] << 5) | (viregs[1] >> 3)) & 0x07FE);
            uint soffset = (uint)((viregs[0x1D] << 16) | (viregs[0x1E] << 8) | viregs[0x1F]);
            if ((viregs[0x1C] & 0x10) == 0x10)
                soffset <<= 5;
            soffset += 0x80000000;
            soffset -= (uint)((viregs[0x1C] & 0xF) << 3);

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


        public uint rpc(uint address, params uint[] args)
        {
            return (uint)(rpc64(address, args) >> 32);
        }

        public ulong rpc64(uint address, params uint[] args)
        {
            byte[] buffer = new byte[4 + 8 * 4];

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