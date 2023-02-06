using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace altair_disk_manager.altair_disk_image
{
    /* Sanitised version of a directory entry */
    public class cpm_dir_entry
    {
        public int index;              /* Zero based directory number */
        public bool valid;              /* Valid if used for a file */
        public raw_dir_entry raw_entry;            /* On-disk representation */
        public int extent_nr;
        public int user;
        public string filename;
        public string type;
        public char[] attribs = new char[3];            /* R - Read-Only, W - Read-Write, S - System */
        public string full_filename; /* filename.ext format */
        public int num_records;
        public int num_allocs;
        public int[] allocation = new int[raw_dir_entry.ALLOCS_PER_EXT];     /* Only 8 of the 16 are used. As the 2-byte allocs 
													 * in the raw_entry are converted to a single value */
        public cpm_dir_entry next_entry; /* pointer to next directory entry if multiple */


    }
}
