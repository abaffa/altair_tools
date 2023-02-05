using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace altair_disk_manager
{
    //https://retrocmp.de/hardware/altair-8800/altair-floppy.htm
    public class DiskDataSector
    {
        /*
        // Tracks 0-5 are formatted as "System Tracks" (regardless of
        // how they are actually used). Sectors on these tracks are
        // formmatted as follows:
        //
        //     Byte Value
        //      0      Track number and 80h
        //     1-2     Number of bytes in boot file
        //    3-130    Data
        //     131     0FFh(Stop Byte)
        //     132     Checksum of 3-130
        //    133-136  Not used
        //
        // Tracks 6-76 (except track 70) are "Data Tracks." Sectors
        // on these tracks are formatted as follows:
        //
        //  Byte Value
        //     0      Track number and 80h
        //     1      Skewed sector = (Sector number* 17) MOD 32
        //     2      File number in directory
        //     3      Data byte count
        //     4      Checksum of 2-3 & 5-134
        //    5-6     Pointer to next data group
        //   7-134    Data
        //    135     0FFh(Stop Byte)
        //    136     Not used
        */

        //     0      Track number and 80h
        public byte _track_number { get; set; }
        //     1      Skewed sector = (Sector number* 17) MOD 32
        public byte _skewed_sector { get; set; }
        //     2      File number in directory
        public byte _file_number { get; set; }
        //     3      Data byte count
        public byte _data_count { get; set; }
        //     4      Checksum of 2-3 & 5-134
        public byte _checksum { get; set; }
        //    5-6     Pointer to next data group
        public byte[] _pointer_next { get; set; }
        //   7-134    Data
        public byte[] _data { get; set; }

        public DiskDataSector(byte[] sec)
        {
            _track_number = sec[0x00];
            _skewed_sector = sec[0x01];
            _file_number = sec[0x02];
            _data_count = sec[0x03];
            _checksum = sec[0x04];

            _pointer_next = new byte[2];
            _pointer_next[0] = sec[0x05];
            _pointer_next[1] = sec[0x06];

            _data = new byte[128];
            for (int i = 0; i < 128; i++)
                _data[i] = sec[0x07 + i];

        }

        public byte[] GetDataSector()
        {
            byte[] ret = new byte[137];
            if (_data == null)
                return null;

            //     0      Track number and 80h
            //     1      Skewed sector = (Sector number* 17) MOD 32
            //     2      File number in directory
            //     3      Data byte count
            //     4      Checksum of 2-3 & 5-134
            //    5-6     Pointer to next data group
            //   7-134    Data
            //    135     0FFh(Stop Byte)
            //    136     Not used


            ret[0x00] = _track_number;
            ret[0x01] = _skewed_sector;
            ret[0x02] = _file_number;
            ret[0x03] = _data_count;
            ret[0x04] = _checksum;

            ret[0x05] = _pointer_next[0];
            ret[0x06] = _pointer_next[1];
            for (int i = 0; i < 128; i++)
                ret[0x07 + i] = _data[i];

            ret[0x087] = 0xe5;
            ret[0x088] = 0xe5;

            return ret;
        }

    }
}
