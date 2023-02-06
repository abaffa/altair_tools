using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace altair_disk_manager.altair_disk_image
{


    public class fdd1mb_simh_disk_type : Disk_Type
    {


        public static int[] mits_skew_table = {
            // 0,
            1,2,3,4,5,6,7,8,9,
            10,11,12,13,14,15,16,17,18,19,
            20,21,22,23,24,25,26,27,28,29,
            30,31,32,33,34,35,36,37,38,39,
            40,41,42,43,44,45,46,47,48,49,
            50,51,52,53,54,55,56,57,58,59,
            60,61,62,63,64,65,66,67,68,69,
            70,71,72,73,74,75,76,77,78,79};


        public const string name_type = "SIMH_FDD_8IN";
        public const int size = 0x0010fdc0;        

        /* Standard 8" floppy drive */
        public fdd1mb_simh_disk_type()
        {
            type = name_type;
            sector_len = 137;
            sector_data_len = 128;
            num_tracks = 254;
            reserved_tracks = 6;
            sectors_per_track = 32;
            block_size = 2048;
            num_directories = 256;
            da = 4;
            image_size = size;    /* Note images formatted in simh are 337664 */
            skew_table_size = mits_skew_table.Length;
            skew_table = mits_skew_table;
            //skew_function = &mits8in_skew_function,
            //format_function = &mits8in_format_disk,
            offsets = new disk_offsets[2]{
                new disk_offsets(0, 254,  3,  -1, -1, -1, -1, -1, -1),
                  new disk_offsets(-1, -1, 0, -1, -1, -1, -1, -1, -1)};
        }

        //int mits8in_skew_function(int track, int logical_sector)
        public override int skew_function(int track, int logical_sector)
        {
            if (track < 6)
            {
                return mits_skew_table[logical_sector];
            }
            /* This additional skew is required for strange historical reasons */
            return (((mits_skew_table[logical_sector] - 1) * 17) % 32) + 1;
        }




        /*
        * Create a newly formatted disk / format an existing disk.
        * This needs to format the raw disk sectors.
        */
        //void mits8in_format_disk(int fd)
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
