using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace altair_disk_manager.altair_disk_image
{


    public class mits5mbhdd1024_disk_type : Disk_Type
    {

        public const string name_type = "MITS_HDD_5MB_1024";
        public const int size = 4988928;

        // MITS 5MB HDD Format
        public mits5mbhdd1024_disk_type()
        {
            type = name_type;
            sector_len = 128;
            sector_data_len = 128;
            num_tracks = 406;
            reserved_tracks = 1;
            sectors_per_track = 96;
            block_size = 4096;
            num_directories = 1024;
            da = 8;
            image_size = size;
            skew_table_size = mits5mbhdd_disk_type.hd5mb_skew_table.Length;
            skew_table = mits5mbhdd_disk_type.hd5mb_skew_table;
            //skew_function = &standard_skew_function,
            //format_function = &format_disk,
            offsets = new disk_offsets[2]{
        new disk_offsets(0, 406, 0, -1, -1, -1, -1, -1, -1),
        new disk_offsets(-1, -1, 0, -1, -1, -1, -1, -1, -1)};
        }

        // Skew table for the 5MB HDD. Note that this requires a 
        public override int skew_function(int track, int logical_sector)
        {
            return skew_table[logical_sector] + 1;
        }

        //Create a newly formatted disk / format an existing disk.
        //The standard function which fills every byte with 0xE5
        //void format_disk(int fd)
        public override void format_function()
        {
            reset_sector_buffer();

            fileData = new byte[size];

            sector_data = Enumerable.Repeat((byte)0xe5, disk_sector_len()).ToArray();

            for (int track = 0; track < disk_num_tracks(); track++)
            {
                for (int sector = 0; sector < disk_sectors_per_track(); sector++)
                {
                    write_raw_sector(track, sector + 1);
                }
            }

        }

    }
}
