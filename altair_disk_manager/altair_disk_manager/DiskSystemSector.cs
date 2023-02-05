using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace altair_disk_manager
{
    //https://retrocmp.de/hardware/altair-8800/altair-floppy.htm
    public class DiskSystemSector
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
   
        */

        //     0      Track number and 80h
        public byte _track_number { get; set; }

        //    1-2      File number in directory
        public byte[] _bytes_boot_file { get; set; }

        //    3-130    Data
        public byte[] _data { get; set; }

        //     132     Checksum of 3-130
        public byte _checksum { get; set; }
        

        public DiskSystemSector(byte[] sec)
        {
            _track_number = sec[0x00];

            _bytes_boot_file = new byte[2];
            _bytes_boot_file[0] = sec[0x01];
            _bytes_boot_file[1] = sec[0x02];

            _data = new byte[128];
            for (int i = 0; i < 128; i++)
                _data[i] = sec[0x03 + i];

            _checksum = sec[0x84];
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
            ret[0x01] = _bytes_boot_file[0];
            ret[0x02] = _bytes_boot_file[1];

            for (int i = 0; i < 128; i++)
                ret[0x03 + i] = _data[i];

            ret[0x84] = _checksum;

            ret[0x085] = 0xe5;
            ret[0x086] = 0xe5;
            ret[0x087] = 0xe5;
            ret[0x088] = 0xe5;

            return ret;
        }

    }
}
