using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace altair_disk_manager.altair_disk_image
{
    /* Disk format Parameters */
    public abstract class Disk_Type
    {
        public string type;       /* String type name */
        public int sector_len;         /* length of sector in bytes (must be 128) */
        public int sector_data_len;    /* length of data part of sector in bytes. Note only supports 128 */
        public int num_tracks;         /* Total tracks */
        public int reserved_tracks;    /* Number of tracks reserved by the OS */
        public int sectors_per_track;  /* Number of sectors per track */
        public int block_size;         /* Size of Block / Allocation */
        public int num_directories;    /* maximum number of directories / extents supported */
        public int da;     /* directory allocations */
        public int image_size;         /* size of disk image (for auto-detection) */
        public int skew_table_size;    /* number of entries in skew table */
        public int[] skew_table;        /* Pointer to the sector skew table */
        public disk_offsets[] offsets;      /* Raw sector offsets for MITS 8" controller */

        public abstract int skew_function(int track, int logical_sector);       /* logical to physical sector skew conversion */
        public abstract void format_function(int fd);       /* pointer to formatting function */



        /* 
         * Disk parameter support routines.
         */
        public int disk_sector_len()
        {
            return sector_len;
        }

        public int disk_data_sector_len()
        {
            return sector_data_len;
        }

        public int disk_num_tracks()
        {
            return num_tracks;
        }

        public int disk_reserved_tracks()
        {
            return reserved_tracks;
        }

        public int disk_sectors_per_track()
        {
            return sectors_per_track;
        }

        public int disk_block_size()
        {
            return block_size;
        }

        public int disk_num_directories()
        {
            return num_directories;
        }

        public int disk_skew_table_size()
        {
            return skew_table_size;
        }

        public int disk_skew_sector(int track_nr, int logical_sector)
        {
            return skew_function(track_nr, logical_sector);
        }

        public int disk_track_len()
        {
            return sector_len * sectors_per_track;
        }

        public int disk_total_allocs()
        {
            return (num_tracks - reserved_tracks) *
                sectors_per_track *
                sector_data_len /
                block_size;
        }

        public int disk_recs_per_alloc()
        {
            return block_size / sector_data_len;
        }

        public int disk_recs_per_extent()
        {
            /* 8 = nr of allocations per extent as per CP/M specification  */
            /* result rounded upwards to multiple of 128 */
            return ((disk_recs_per_alloc() * 8) + 127) / 128 * 128;
        }

        public int disk_dirs_per_sector()
        {
            return sector_data_len / AltairDiskImage.DIR_ENTRY_LEN;
        }

        public int disk_dirs_per_alloc()
        {
            return block_size / AltairDiskImage.DIR_ENTRY_LEN;
        }

        public disk_offsets disk_get_offsets(int track_nr)
        {
            /* Assumes only 2 offsets. Which is pretty safe. Only MITS 8IN formats use them */
            if ((track_nr >= offsets[0].start_track) &&
                (track_nr <= offsets[0].end_track))
            {
                return offsets[0];
            }
            return offsets[1];
        }

        public int disk_off_track_nr(int track_nr)
        {
            return disk_get_offsets(track_nr).off_track_nr;
        }

        public int disk_off_sect_nr(int track_nr)
        {
            return disk_get_offsets(track_nr).off_sect_nr;
        }

        public int disk_off_data(int track_nr)
        {
            return disk_get_offsets(track_nr).off_data;
        }

        public int disk_off_stop(int track_nr)
        {
            return disk_get_offsets(track_nr).off_stop;
        }

        public int disk_off_zero(int track_nr)
        {
            return disk_get_offsets(track_nr).off_zero;
        }

        public int disk_off_csum(int track_nr)
        {
            return disk_get_offsets(track_nr).off_csum;
        }

        public int disk_csum_method(int track_nr)
        {
            return disk_get_offsets(track_nr).csum_method;
        }



        public enum disk_format
        {
            MITS8IN_FORMAT,
            MITS5MBHDD_FORMAT,
            MITS5MBHDD1024_FORMAT,
            TARBELLFDD_FORMAT,
            FDD15MB_FORMAT,
            MITS8IN8MB_FORMAT,
            MITS8INSIMH_FORMAT
        }

        // Detect the image type based on the file size         
        public static int disk_detect_type(int length)
        {
            if (length <= 0)
            {
                return -1;
            }

            disk_format format;

            if (length == mits8in_disk_type.size)
            {
                format = disk_format.MITS8IN_FORMAT;
            }

            else if (length == mits5mbhdd_disk_type.size)
            {
                format = disk_format.MITS5MBHDD_FORMAT;
            }
            else if (length == mits5mbhdd1024_disk_type.size)
            {
                format = disk_format.MITS5MBHDD1024_FORMAT;
            }
            else if (length == tarbellfdd_disk_type.size)
            {
                format = disk_format.TARBELLFDD_FORMAT;
            }
            else if (length == fdd15mb_disk_type.size)
            {
                format = disk_format.FDD15MB_FORMAT;
            }

            else if (length == mits8in8m_disk_type.size)
            {
                format = disk_format.MITS8IN8MB_FORMAT;
            }
            else if (length == fdd1mb_simh_disk_type.size)
            {
                format = disk_format.MITS8INSIMH_FORMAT;
            }
            
            else
            {
                return -1;
            }

            if (AltairDiskImage.VERBOSE)
                Console.Write("Detected Format: {0}\n", format.ToString());

            return (int)format;
        }


        // Manually set the image type
        public static Disk_Type disk_set_type(string type)
        {
            if (type == mits8in_disk_type.name_type)
            {
                return new mits8in_disk_type();
            }
            
            else if (type == mits5mbhdd_disk_type.name_type)
            {
                return new mits5mbhdd_disk_type();
            }
            else if (type ==  mits5mbhdd1024_disk_type.name_type)
            {
                return new mits5mbhdd1024_disk_type();
            }
            else if (type == tarbellfdd_disk_type.name_type)
            {
                return new tarbellfdd_disk_type();
            }
            else if (type == fdd15mb_disk_type.name_type)
            {
                return new fdd15mb_disk_type();
            }

            else if (type == mits8in8m_disk_type.name_type)
            {
                return new mits8in8m_disk_type();
            }
            else if (type == fdd1mb_simh_disk_type.name_type)
            {
                return new fdd1mb_simh_disk_type();
            }
            else
            {
                //error_exit(0, "Invalid disk image type: %s", type);
                return null;
            }
        }

        public static string disk_get_type(disk_format _format)
        {


          if (_format == disk_format.MITS8IN_FORMAT)
            {
                return mits8in_disk_type.name_type;
    }

            else if (_format == disk_format.MITS5MBHDD_FORMAT)
            {
                 return mits5mbhdd_disk_type.name_type;
}
            else if (_format == disk_format.MITS5MBHDD1024_FORMAT)
            {
                 return mits5mbhdd1024_disk_type.name_type;
            }
            else if (_format == disk_format.TARBELLFDD_FORMAT)
            {
                 return tarbellfdd_disk_type.name_type;
            }
            else if (_format == disk_format.FDD15MB_FORMAT)
            {
                return fdd15mb_disk_type.name_type;
            }

            else if (_format == disk_format.MITS8IN8MB_FORMAT)
            {
                return mits8in8m_disk_type.name_type;
            }
            else if (_format == disk_format.MITS8INSIMH_FORMAT)
            {
                return fdd1mb_simh_disk_type.name_type;
            }
            
            else
            {
                return "";
            }

        }


        void disk_format_disk(int fd)
        {
            format_function(fd);
        }

        public void disk_dump_parameters()
        {
            Console.Write("Sector Len: {0}\n", disk_sector_len());
            Console.Write("Data Len  : {0}\n", disk_data_sector_len());
            Console.Write("Num Tracks: {0}\n", disk_num_tracks());
            Console.Write("Res Tracks: {0}\n", disk_reserved_tracks());
            Console.Write("Secs/Track: {0}\n", disk_sectors_per_track());
            Console.Write("Block Size: {0}\n", disk_block_size());
            Console.Write("Num Tracks: {0}\n", disk_num_tracks());
            Console.Write("Track Len : {0}\n", disk_track_len());
            Console.Write("Recs/Ext  : {0}\n", disk_recs_per_extent());
            Console.Write("Recs/Alloc: {0}\n", disk_recs_per_alloc());
            Console.Write("Dirs/Sect : {0}\n", disk_dirs_per_sector());
            Console.Write("Dirs/Alloc: {0}\n", disk_dirs_per_alloc());
            Console.Write("Num Dirs  : {0} [max: {1}]\n", disk_num_directories(), AltairDiskImage.MAX_DIRS);
            Console.Write("Tot Allocs: {0} [max: {1}]\n", disk_total_allocs(), AltairDiskImage.MAX_ALLOCS);
        }


        /*
         * Calculate the sector checksum for the data portion.
         * Note this is not the full checksum as 4 non-data bytes
         * need to be included in the checksum.
         */
        public byte calc_checksum(byte[] buffer, int sector_start)
        {
            byte csum = 0;
            for (int i = sector_start; i < disk_data_sector_len(); i++)
            {
                csum += buffer[i];
            }
            return csum;
        }



    }
}
