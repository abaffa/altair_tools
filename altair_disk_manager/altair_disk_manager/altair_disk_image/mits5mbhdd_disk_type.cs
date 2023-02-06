using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace altair_disk_manager.altair_disk_image
{


    public class mits5mbhdd_disk_type : Disk_Type
    {


        // skew for each CPM sector. Not physical sector 
        public static int[] hd5mb_skew_table = {
            0,1,14,15,28,29,42,43,8,9,22,23,
            36,37,2,3,16,17,30,31,44,45,10,11,
            24,25,38,39,4,5,18,19,32,33,46,47,
            12,13,26,27,40,41,6,7,20,21,34,35,
            48,49,62,63,76,77,90,91,56,57,70,71,
            84,85,50,51,64,65,78,79,92,93,58,59,
            72,73,86,87,52,53,66,67,80,81,94,95,
            60,61,74,75,88,89,54,55,68,69,82,83
        };



        public const string name_type = "MITS_HDD_5MB";
        public const int size = 4988928;

        // MITS 5MB HDD Format
        public mits5mbhdd_disk_type()
        {
            type = name_type;
            sector_len = 128;
            sector_data_len = 128;
            num_tracks = 406;
            reserved_tracks = 1;
            sectors_per_track = 96;
            block_size = 4096;
            num_directories = 256;
            da = 2;
            image_size = size;
            skew_table_size = hd5mb_skew_table.Length;
            skew_table = hd5mb_skew_table;
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
