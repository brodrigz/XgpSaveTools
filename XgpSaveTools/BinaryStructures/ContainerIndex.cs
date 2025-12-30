using System.Text;
using XgpSaveTools.Extensions;

namespace XgpSaveTools.BinaryStructures
{
    // If anyone is able to fully reverse-engineer this memory layout we would then be able to safely remove and add new entries into containers
    // until then we're limited to replacing existing entries with new files
    // https://github.com/goatfungus/NMSSaveEditor/issues/306
    public class ContainerIndexBinaryModel
    {
        public int Version { get; set; } // unused
        public int EntryCount { get; set; } // unused
        public string PackageName { get; set; }
        public string StorePackage { get; set; }
        public byte[] UnknownHeader { get; set; }  // captures creation_date + unknown + stray UTF‑16 + unknown, unused
        public List<ContainerEntryBinaryModel> Entries { get; } = new();
    }

    public class ContainerEntryBinaryModel
    {
        public string Name1 { get; set; }
        public string Name2 { get; set; } // unused
        public string AddressHex { get; set; } // unknown hex address, unused
        public byte ContainerNum { get; set; } // unused
        public int UnknownCount { get; set; }   // the 4 bytes you read after containerNum, unused
        public Guid FileId { get; set; }   // the 16 byte GUID, unused
        public byte[] Padding { get; set; }   // the 8 byte FILETIME + 16 byte padding, unused
    }

    public static class ContainerIndexBinaryStructure
    {
        public static ContainerIndexBinaryModel Parse(string indexPath)
        {
            using var fs = File.OpenRead(indexPath);
            using var br = new BinaryReader(fs, Encoding.Unicode);

            var m = new ContainerIndexBinaryModel();

            // version & count
            m.Version = br.ReadInt32();
            m.EntryCount = br.ReadInt32();

            //  the two UTF‑16 names
            m.PackageName = br.ReadUtf16();   // pkg_display_name
            m.StorePackage = br.ReadUtf16();   // store_pkg_name

            // everything until the first slot record
            long slotStart = fs.Position;
            var creationDateBytes = br.ReadBytes(8);
            var unknown4 = br.ReadBytes(4);
            var strayUtf16 = br.ReadUtf16(); // just to advance the reader
            var strayUtf16Bytes = Encoding.Unicode.GetBytes(strayUtf16);
            var unknown8 = br.ReadBytes(8);

            long firstSlotPos = fs.Position;
            // now go back and record from slotStart to firstSlotPos:
            fs.Seek(slotStart, SeekOrigin.Begin);
            m.UnknownHeader = br.ReadBytes((int)(firstSlotPos - slotStart));

            // advance to first slot
            fs.Seek(firstSlotPos, SeekOrigin.Begin);

            // read slot records
            for (int i = 0; i < m.EntryCount; i++)
            {
                var s = new ContainerEntryBinaryModel
                {
                    Name1 = br.ReadUtf16(),
                    Name2 = br.ReadUtf16(),
                    AddressHex = br.ReadUtf16(),
                    ContainerNum = br.ReadByte(),
                    UnknownCount = br.ReadInt32(),
                    FileId = new Guid(br.ReadBytes(16)),
                    Padding = br.ReadBytes(24) // FILETIME(8)+padding(16)
                };
                m.Entries.Add(s);
            }

            return m;
        }

        // needs testing
        //public static void Write(string indexPath, ContainerIndexBinaryModel m, List<ContainerEntryBinaryModel> newSlots)
        //{
        //	using var fs = File.Create(indexPath);
        //	using var bw = new BinaryWriter(fs, Encoding.Unicode);

        //	bw.Write(m.Version);
        //	bw.Write(newSlots.Count);

        //	// two names
        //	bw.WriteUtf16(m.PackageName);
        //	bw.WriteUtf16(m.StorePackage);

        //	// dump the raw header back
        //	bw.Write(m.UnknownHeader);

        //	// now each slot
        //	foreach (var s in newSlots)
        //	{
        //		bw.WriteUtf16(s.Name1);
        //		bw.WriteUtf16(s.Name2);
        //		bw.WriteUtf16(s.AddressHex);

        //		bw.Write(s.ContainerNum);
        //		bw.Write(s.UnknownCount);

        //		bw.Write(s.FileId.ToByteArray());
        //		bw.Write(s.Padding);
        //	}
        //}
    }
}