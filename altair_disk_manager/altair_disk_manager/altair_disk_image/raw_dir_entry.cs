using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace altair_disk_manager.altair_disk_image
{
    /* On-disk representation of a directory entry */
    public class raw_dir_entry
    {
        public const byte ALLOCS_PER_EXT = 16;     /* Number of allocations in a directory entry (extent) */

        public const byte FILENAME_LEN = 8;
        public const byte TYPE_LEN = 3;
        public const byte FULL_FILENAME_LEN = (FILENAME_LEN + TYPE_LEN + 2);
        public const byte MAX_USER = 15;
        public const byte DELETED_FLAG = 0xe5;


        public byte user;                   /* User (0-15). E5 = Deleted */
        public byte[] filename = new byte[FILENAME_LEN];
        public byte[] type = new byte[TYPE_LEN];
        public byte extent_l;               /* Extent number. */
        public byte reserved;
        public byte extent_h;               /* Not used */
        public byte num_records;            /* Number of sectors used for this directory entry */
        public byte[] allocation = new byte[ALLOCS_PER_EXT]; /* List of 2K Allocations used for the file */

        public void debug()
        {
            Console.WriteLine((int)user);
            Console.WriteLine(System.Text.Encoding.Default.GetString(filename));
            Console.WriteLine(System.Text.Encoding.Default.GetString(type));
            Console.WriteLine((int)extent_l);
            Console.WriteLine((int)reserved);
            Console.WriteLine((int)extent_h);
            Console.WriteLine((int)num_records);


        }

        public byte[] ToByteArray()
        {
            byte[] ret = new byte[32];

            ret[0] = user;
            ret[1] = filename[0];

            ret[2] = filename[1];
            ret[3] = filename[2];
            ret[4] = filename[3];
            ret[5] = filename[4];
            ret[6] = filename[5];
            ret[7] = filename[6];
            ret[8] = filename[7];

            ret[9] = type[0];
            ret[10] = type[1];
            ret[11] = type[2];

            ret[12] = extent_l;
            ret[13] = reserved;
            ret[14] = extent_h;
            ret[15] = num_records;

            for (int i = 0; i < ALLOCS_PER_EXT; i++)
                ret[16 + i] = allocation[i];



            return ret;
        }
    }
}
