using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace altair_disk_manager.altair_disk_image
{


    public class fdd15mb_disk_type : Disk_Type
    {


        // skew for each CPM sector. Not physical sector 
        public static int[] fdd15mb_skew_table = {
            0,1,2,3,4,5,6,7,8,9,
            10,11,12,13,14,15,16,17,18,19,
            20,21,22,23,24,25,26,27,28,29,
            30,31,32,33,34,35,36,37,38,39,
            40,41,42,43,44,45,46,47,48,49,
            50,51,52,53,54,55,56,57,58,59,
            60,61,62,63,64,65,66,67,68,69,
            70,71,72,73,74,75,76,77,78,79
        };



        public const string name_type = "FDD_1.5MB";
        public const int size = 1525760;

        // MITS 5MB HDD Format
        public fdd15mb_disk_type()
        {
            type = name_type;
            sector_len = 128;
            sector_data_len = 128;
            num_tracks = 149;
            reserved_tracks = 1;
            sectors_per_track = 80;
            block_size = 4096;
            num_directories = 256;
            da = 2;
            image_size = size;
            skew_table_size = fdd15mb_skew_table.Length;
            skew_table = fdd15mb_skew_table;
            //skew_function = &standard_skew_function,
            //format_function = &format_disk,
            offsets = new disk_offsets[2]{
        new disk_offsets(0, 77,  0,  -1, -1, -1, -1, -1, -1),
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
