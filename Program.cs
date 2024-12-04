using System.Text;

namespace XDVDForenSic
{
    internal struct XDVDFSFileInfo
    {
        public uint NoIdea;
        public int Sector;
        public int Size;
        public byte Flags;
        public string Filename;
    }

    internal class Program
    {
        static void SeekToSector(Stream stream, int sector)
        {
            stream.Seek((long)sector * 0x800, SeekOrigin.Begin);
        }

        // Reads an XDVDFS folder structure. Does not do any recursion.
        static XDVDFSFileInfo[] ReadFolderFromSector(Stream stream, int sector)
        {
            List<XDVDFSFileInfo> files = new List<XDVDFSFileInfo>();

            SeekToSector(stream, sector);

            while (true)
            {
                uint noIdea = stream.ReadUInt32LE();
                if (noIdea == 0xFFFFFFFF)
                    break;
                int fileSector = stream.ReadInt32LE();
                if (fileSector == 0)
                {
                    Console.WriteLine("Folder broken, stopping read.");
                    break;
                }
                int fileSize = stream.ReadInt32LE();
                byte fileFlags = stream.ReadUInt8();
                string fileName = stream.ReadByteLengthPrefixedString(Encoding.ASCII);
                files.Add(new XDVDFSFileInfo
                {
                    NoIdea = noIdea,
                    Sector = fileSector,
                    Size = fileSize,
                    Flags = fileFlags,
                    Filename = fileName
                });
                if (stream.Position % 4 != 0)
                    stream.Position += 4 - (stream.Position % 4);
            }

            return files.ToArray();
        }

        // Reads the contents of a sector into the output buffer, which must be 0x800 bytes long
        static void ReadSectorData(Stream stream, int sector, byte[] output)
        {
            SeekToSector(stream, sector);
            stream.Read(output, 0, 0x800);
        }

        /*
         * A missing sector will be entirely 0x00 or 0xFF.
         * Now, a legitimate file could also be this. But if it is, we probably don't want it anyway
         */
        static bool IsSectorPossiblyFuckedUp(byte[] sectorData)
        {
            for (int i = 0; i < 0x800; i++)
            {
                if (sectorData[i] != 0x00 && sectorData[i] != 0xFF)
                    return false;
            }
            return true;
        }

        /*
         * A good risk flag is if the sector immediately before a file didn't end with a NULL byte,
         * the actual sector offset might be invalid. Of course this could be a false flag, but we flag it anyway
         */
        static bool DidSectorComeAfterCleanData(byte[] previousSectorData)
        {
            if (previousSectorData[0x7FF] == 0x00)
                return true;
            else
                return false;
        }

        static int GetSectorNumber(uint fileOffset)
        {
            fileOffset = fileOffset - (fileOffset % 0x800);
            return (int)(fileOffset / 0x800);
        }

        static void Main(string[] args)
        {
            Console.WriteLine("XDVDForenSic - XDVDFS data recovery tool");
            Console.WriteLine("https://github.com/InvoxiPlayGames/XDVDForenSic");
            Console.WriteLine();
            string? fileName;
            uint fileOffset;
            string? mode;
            string? outputPath;
            if (args.Length < 3)
            {
                //fileName = "C:\\Users\\Emma\\Downloads\\MoosesGamerGunkGuitarHeroPrototype.img";
                //fileOffset = Convert.ToUInt32("0x28800", 16);
                //mode = "extract";
                Console.WriteLine("usage: ./XDVDForenSic [/path/to/damaged.img] [directory start address] [list|extract]");
                return;
            }
            else
            {
                fileName = args[0];
                fileOffset = Convert.ToUInt32(args[1], 16);
                mode = args[2];
                if (args.Length >= 4)
                    outputPath = args[3];
            }

            // TODO: implement outputpath

            if (!File.Exists(fileName))
            {
                Console.WriteLine("File '{0}' does not exist.", fileName);
                return;
            }

            if (mode != "list" && mode != "extract")
            {
                Console.WriteLine("Please specify either 'list' or 'extract' as a mode.");
                return;
            }

            bool extractMode = mode == "extract";

            FileStream fs = File.OpenRead(fileName);

            int fileSector = GetSectorNumber(fileOffset);

            Console.WriteLine("Reading directory structure at sector 0x{0} from '{1}'...", fileSector.ToString("X"), fileName);

            XDVDFSFileInfo[] files = ReadFolderFromSector(fs, fileSector);

            string recoveryFolderName = $"recovered_0x{fileSector:X}";
            if (!Directory.Exists(recoveryFolderName))
                Directory.CreateDirectory(recoveryFolderName);

            byte[] readSectorBuffer = new byte[0x800];

            foreach (var item in files)
            {
                bool isDirectory = item.Flags == 0x10;
                string type = isDirectory ? "Directory" : "File";
                Console.Write($"{item.Filename} - 0x{(item.Sector * 0x800):X8} - {type}");
                if (item.Flags == 0x80)
                    Console.WriteLine($" - {item.Size} bytes");
                else
                    Console.WriteLine();

                if (fs.Length < (item.Sector * 0x800))
                {
                    Console.WriteLine(" ! sector goes past the boundaries of the disc! skipping");
                    continue;
                }

                if (extractMode && item.Flags == 0x80)
                {
                    string outputFilename = Path.Join(recoveryFolderName, item.Filename);
                    // read the previous sector to see if that's a risk
                    ReadSectorData(fs, item.Sector - 1, readSectorBuffer);
                    if (!DidSectorComeAfterCleanData(readSectorBuffer))
                        Console.WriteLine(" ! sector before the file had non-zero data!");
                    // now start reading our secctors
                    int totalSectorCount = item.Size < 0x800 ? 1 : (item.Size / 0x800) + ((item.Size % 0x800) > 0 ? 1 : 0);
                    int remainingFileSize = item.Size;
                    FileStream outf = File.OpenWrite(outputFilename);
                    for (int i = 0; i < totalSectorCount; i++)
                    {
                        ReadSectorData(fs, item.Sector + i, readSectorBuffer);
                        // check if the sector might be bunk
                        if (i == 0 && IsSectorPossiblyFuckedUp(readSectorBuffer))
                            Console.WriteLine(" !! first sector of the file is zeroed! data is probably missing.");
                        outf.Write(readSectorBuffer, 0, remainingFileSize < 0x800 ? remainingFileSize : 0x800);
                        remainingFileSize -= 0x800;
                    }
                    outf.Close();
                    Console.WriteLine(" extracted!");
                }
            }

            fs.Close();
        }
    }
}
