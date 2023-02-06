using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace altair_disk_manager.altair_disk_image
{


    public class tarbellfdd_disk_type : Disk_Type
    {


        public static int[] tarbell_skew_table = {
             0,  6, 12, 18, 24,  4,
            10, 16, 22,  2,  8, 14,
            20,  1,  7, 13, 19, 25,
             5, 11, 17, 23,  3,  9,
            15, 21
        };



        public const string name_type = "TARBELL_FDD";
        public const int size = 256256;

        // MITS 5MB HDD Format
        public tarbellfdd_disk_type()
        {
            type = name_type;
            sector_len = 128;
            sector_data_len = 128;
            num_tracks = 77;
            reserved_tracks = 2;
            sectors_per_track = 26;
            block_size = 1024;
            num_directories = 64;
            da = 2;
            image_size = size;
            skew_table_size = tarbell_skew_table.Length;
            skew_table = tarbell_skew_table;
            //skew_function = &standard_skew_function,
            //format_function = &format_disk,
            offsets = new disk_offsets[2]{
                new disk_offsets(0, 77,  0,  -1, -1, -1, -1, -1, -1),
                new disk_offsets(-1, -1, 0, -1, -1, -1, -1, -1, -1) };
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
