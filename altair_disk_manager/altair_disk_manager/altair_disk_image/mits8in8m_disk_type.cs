using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace altair_disk_manager.altair_disk_image
{


    public class mits8in8m_disk_type : Disk_Type
    {

        public const string name_type = "MITS_FDD_8IN_8MB";
        public const int size = 8978432;

        public mits8in8m_disk_type()
        {
            type = name_type;
            sector_len = 137;
            sector_data_len = 128;
            num_tracks = 2048;
            reserved_tracks = 2;
            sectors_per_track = 32;
            block_size = 4096;
            num_directories = 512;
            da = 2;
            image_size = size;
            skew_table_size = mits8in_disk_type.mits_skew_table.Length;
            skew_table = mits8in_disk_type.mits_skew_table;

            offsets = new disk_offsets[2]{
                new disk_offsets(0,  5, 3, 0, 0, 131, 133, 132, 0),
                new disk_offsets(6, 77, 7, 0, 1, 135, 136, 4, 1)
            };
        }

        //int mits8in_skew_function(int track, int logical_sector)
        public override int skew_function(int track, int logical_sector)
        {
            if (track < 6)
            {
                return mits8in_disk_type.mits_skew_table[logical_sector];
            }
            /* This additional skew is required for strange historical reasons */
            return (((mits8in_disk_type.mits_skew_table[logical_sector] - 1) * 17) % 32) + 1;
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

            sector_data[1] = 0x00;
            sector_data[2] = 0x01;

            /* Stop byte = 0xff */
            sector_data[disk_off_stop(0)] = 0xff;

            /* From zero byte to end of sector must be set to 0x00 */
            Buffer.BlockCopy(sector_data, disk_off_stop(0) + 1,
                Enumerable.Repeat((byte)0x00, disk_sector_len() - disk_off_zero(0)).ToArray(),
                0, disk_sector_len() - disk_off_zero(0));

            for (int track = 0; track < disk_num_tracks(); track++)
            {
                //if (track == 6)
                if (track == 2)
                {
                    sector_data = Enumerable.Repeat((byte)0xe5, disk_sector_len()).ToArray();

                    sector_data[2] = 0x01;
                    sector_data[disk_off_stop(6)] = 0xff;
                    sector_data[disk_off_zero(6)] = 0x00;

                    Buffer.BlockCopy(sector_data, disk_off_stop(0) + 1,
                        Enumerable.Repeat((byte)0x00, disk_sector_len() - disk_off_zero(6)).ToArray(),
                        0, disk_sector_len() - disk_off_zero(6));
                }
                for (int sector = 0; sector < disk_sectors_per_track(); sector++)
                {
                    //if (track < 6)
                    if (track < 2)
                    {
                        sector_data[disk_off_track_nr(0)] = (byte)(track | 0x80);
                        sector_data[disk_off_csum(0)] = calc_checksum(sector_data, disk_off_data(0) + 1);
                    }
                    else
                    {
                        sector_data[disk_off_track_nr(6)] = (byte)(track | 0x80);
                        sector_data[disk_off_sect_nr(6)] = (byte)((sector * 17) % 32);
                        byte checksum = calc_checksum(sector_data, disk_off_data(6) + 1);
                        checksum += sector_data[2];
                        checksum += sector_data[3];
                        checksum += sector_data[5];
                        checksum += sector_data[6];
                        sector_data[disk_off_csum(6)] = checksum;
                    }

                    write_raw_sector(track, sector + 1);
                }
            }
        }

    }


}
