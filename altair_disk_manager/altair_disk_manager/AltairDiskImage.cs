/*
 *****************************************************************************
 * ALTAIR Disk Tools 
 *
 * Manipulate Altair CPM Disk Images
 * 
 *****************************************************************************
*/
/*
 * MIT License
 *
 * Copyright (c) 2023 Paul Hatchman
 *
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 *
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
 */
/* TODO: Validate if filename, only has an extension */
/* TODO: Test test heading alignment for large directory listings */

using altair_disk_manager.altair_disk_image;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace altair_disk_manager
{
    public class AltairDiskImage
    {

        public static bool VERBOSE = true;


        public const UInt16 MAX_SECT_SIZE = 256;     /* Maximum size of a disk sector read */
        public const UInt16 MAX_DIRS = 1024;     /* Maximum size of directory table */
                                                 /* There is a 5MB HDD format with 1024 extents, but not yet supported */
                                                 /* Current max is 8MB FDC+ format with 512 entries */
        public const UInt16 MAX_ALLOCS = 2048;   /* Maximum size of allocation table */
                                                 /* 8MB FDC+ type uses 2046 blocks */
        public const byte DIR_ENTRY_LEN = 32;      /* Length of a single directory entry (extent)*/
        public const byte ALLOCS_PER_EXT = 16;     /* Number of allocations in a directory entry (extent) */
        public const byte RECORD_MAX = 128;        /* Max records per directory entry (extent) */

        public const byte FILENAME_LEN = 8;
        public const byte TYPE_LEN = 3;
        public const byte FULL_FILENAME_LEN = (FILENAME_LEN + TYPE_LEN + 2);
        public const byte MAX_USER = 15;
        public const byte DELETED_FLAG = 0xe5;





        /* On-disk representation of a directory entry */
        public class raw_dir_entry
        {
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
            public int[] allocation = new int[ALLOCS_PER_EXT];     /* Only 8 of the 16 are used. As the 2-byte allocs 
													 * in the raw_entry are converted to a single value */
            public cpm_dir_entry next_entry; /* pointer to next directory entry if multiple */


        }


        cpm_dir_entry[] dir_table;          /* Directory entires in order read from "disk" */
        cpm_dir_entry[] sorted_dir_table;      /* Pointers to entries, sorted by name+type and extent nr*/
        byte[] alloc_table = new byte[MAX_ALLOCS];        /* Allocation table. 0 = Unused, 1 = Used */

        Disk_Type _disk_type;                   /* Pointer to the disk image type */



        Byte[] fileData;


        List<FileEntry> FAT = new List<FileEntry>();
        Dictionary<string, List<FileEntry>> file_entries = new Dictionary<string, List<FileEntry>>();
        public bool ReadImageFile(String fileName, ToolStripProgressBar progressBar = null)
        {
            try
            {
                if (File.Exists(fileName))
                {

                    FileInfo fi = new FileInfo(fileName);


                    // try to auto-detect type 

                    //fileData = new byte[fi.Length];

                    if (progressBar != null)
                    {
                        progressBar.Minimum = 0;
                        progressBar.Maximum = (int)fi.Length;
                        progressBar.Visible = true;
                    }

                    fileData = new byte[fi.Length];

                    using (BinaryReader b = new BinaryReader(
                    File.Open(fileName, FileMode.Open)))
                    {
                        // 2.
                        // Position and length variables.
                        int pos = 0;
                        // 2A.
                        // Use BaseStream.
                        int length = (int)b.BaseStream.Length;
                        while (pos < length && pos < fileData.Length)
                        {

                            fileData[pos] = b.ReadByte();
                            // 3.
                            // Read integer.
                            //int v = b.ReadInt32();
                            //Console.WriteLine(v);

                            // 4.
                            // Advance our position variable.

                            if (progressBar != null)
                            {
                                if (pos % 1000000 == 0)
                                {
                                    progressBar.Value = pos;
                                    Application.DoEvents();
                                }
                            }

                            pos += sizeof(byte);
                        }
                    }

                    int _format = Disk_Type.disk_detect_type(fileData.Length);
                    if (_format < 0)
                    {
                        // For format we default to mits 8IN
                        _disk_type = new mits8in_disk_type();
                        //Console.Write("Defaulting to disk type: {0}\n", _disk_type.type);
                    }

                    else
                    {
                        _disk_type = Disk_Type.disk_set_type(Disk_Type.disk_get_type((Disk_Type.disk_format)_format));
                    }

                    // Initial allocation table 
                    for (int i = 0; i < _disk_type.da; ++i)
                        alloc_table[i] = 1;

                    if (progressBar != null)
                        progressBar.Visible = false;

                    return true;
                }
            }
            catch (Exception Ex)
            {
                MessageBox.Show("An error occurred while reading the image file.\nPlease check the file.", "Reading Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            if (progressBar != null)
                progressBar.Visible = false;

            return false;
        }

        public bool SaveImageFile(string fileName, ToolStripProgressBar progressBar = null)
        {
            try
            {
                if (progressBar != null)
                {
                    progressBar.Minimum = 0;
                    progressBar.Maximum = (int)fileData.Length;
                    progressBar.Visible = true;
                }


                using (BinaryWriter b = new BinaryWriter(File.Open(fileName, FileMode.Create)))
                {
                    for (int pos = 0; pos < fileData.Length; pos++)
                    {
                        b.Write(fileData[pos]);
                        if (progressBar != null)
                        {
                            if (pos % 1000000 == 0)
                            {
                                progressBar.Value = pos;
                                Application.DoEvents();
                            }
                        }
                    }
                }


                if (progressBar != null)
                    progressBar.Visible = false;

                return true;

            }
            catch
            {
                MessageBox.Show("An error occurred while writing the image file.\nPlease check the file.", "Writing Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            if (progressBar != null)
                progressBar.Visible = false;

            return false;
        }

        public List<FileEntry> cmd_ls()
        {
            load_directory_table();

            FAT.Clear();
            file_entries.Clear();

            int file_count = 0;
            int kb_used = 0;
            //int kb_free = 0;
            int entry_count = 0;

            cpm_dir_entry entry = null;
            int this_records = 0;
            int this_allocs = 0;
            int this_kb = 0;
            string last_filename = "";


            for (int i = 0; i < _disk_type.disk_num_directories(); i++)
            {
                /* Valid entries are sorted before invalid ones. So stop on first invalid */
                entry = sorted_dir_table[i];
                if (!entry.valid)
                {
                    break;
                }
                entry_count++;
                /* If this is the first record for this file, then reset the file totals */
                if (entry.full_filename.ToString() != last_filename)
                {
                    file_count++;
                    this_records = 0;
                    this_allocs = 0;
                    this_kb = 0;
                    last_filename = entry.full_filename.ToString();
                }

                this_records += entry.num_records;
                this_allocs += entry.num_allocs;

                /* If there are no more dir entries, print out the file details */
                if (entry.next_entry == null)
                {
                    this_kb += (this_allocs * _disk_type.disk_recs_per_alloc() * _disk_type.disk_data_sector_len()) / 1024;
                    kb_used += this_kb;

                    Console.Write("{0} {1} {2:d7}B {3:d3}K {4:d} {5}\n",
                        entry.filename,
                         entry.type,
                        this_records * _disk_type.disk_sector_len(),
                        this_kb,
                        entry.user,
                         (new string(entry.attribs)).Trim('\0'));

                    FileEntry f = new FileEntry()
                    {
                        _entry = i,
                        _status = entry.user,

                        _name = entry.filename,
                        _ext = entry.type,
                        _ex = entry.raw_entry.extent_l,
                        _s1 = entry.raw_entry.reserved,
                        _s2 = entry.raw_entry.extent_h,
                        _rc = entry.raw_entry.num_records,
                        _entry_number = 0,
                        _num_records = 0,
                        _size = this_records * _disk_type.disk_sector_len(),
                        _start = 0,
                        //_al = entry.raw_entry.allocation
                    };


                    if (!file_entries.ContainsKey(entry.full_filename))
                    {
                        FAT.Add(f);
                        file_entries.Add(entry.full_filename, new List<FileEntry>(new FileEntry[] { f }));
                    }
                    else
                    {
                        file_entries[entry.full_filename].Add(f);
                    }
                }
            }


            return FAT;
        }

        public List<FileEntry> GetFileEntry(string filename)
        {
            if (file_entries.ContainsKey(filename))
                return file_entries[filename];
            return null;
        }




        public byte[] GetFile(string from_filename)
        {
            int text_mode = -1;

            // does the file exist in CPM? 
            cpm_dir_entry entry = find_dir_by_filename(from_filename, null, false);
            if (entry == null)
            {
                Console.Write("Error copying file {0}\n", from_filename);
            }
            // finally copy the file from disk image
            byte[] file_data = copy_from_cpm(entry, text_mode);

            return file_data;
        }


        public Disk_Type GetDisk()
        {
            return _disk_type;
        }

        /// <summary>
        /// ///////////////
        /// </summary>




        // Copy a file from CPM disk to host
        // dir_entry - The first directory entry for the file to be copied.
        // text_mode - -1 = auto-detect, 0 = binary, 1 = text

        byte[] copy_from_cpm(cpm_dir_entry dir_entry, int text_mode)
        {
            List<byte> file_data = new List<byte>();

            sector_data = new byte[MAX_SECT_SIZE];
            int data_len = _disk_type.disk_data_sector_len();
            while (dir_entry != null)
            {
                int num_records = ((_disk_type.disk_recs_per_extent() > 128) && (dir_entry.num_allocs > 4)) ?
                                    128 + dir_entry.num_records : dir_entry.num_records;

                for (int recnr = 0; recnr < num_records; recnr++)
                {
                    int alloc = dir_entry.allocation[recnr / _disk_type.disk_recs_per_alloc()];
                    // if no more allocations, then done with this extent 
                    if (alloc == 0)
                        break;

                    // get data for this allocation and record number 
                    read_sector(alloc, recnr);

                    // If in auto-detect mode or if in text_mode and this is the last sector 
                    if ((text_mode == -1) ||
                        ((text_mode == 1) && (recnr == num_records - 1)))
                    {
                        for (int i = 0; i < _disk_type.disk_data_sector_len(); i++)
                        {
                            // If auto-detecting text mode, check if char is "text"
                            //where "text" means 7 bit only
                            if (text_mode == -1)
                            {
                                if ((sector_data[i] & 0x80) > 0)
                                {
                                    // not "text", so set to binary mode 
                                    text_mode = 0;
                                    break;
                                }
                            }
                            // If in text mode and on last block, then check for ^Z for EOF 
                            //Set data_len to make sure that data stop writing prior to first ^ Z
                            if (text_mode > 0 && (recnr == num_records - 1) &&
                            sector_data[i] == 0x1a)
                            {
                                data_len = i;
                                break;
                            }
                        }
                    }

                    if (recnr <= num_records - 1)
                        // write out current sector 
                        for (int j = 0; j < data_len; j++)
                            file_data.Add(sector_data[j]);
                }
                dir_entry = dir_entry.next_entry;
            }


            int l = file_data.Count - 1;
            while (l > 0 && (file_data[l] == 0x0 || file_data[l] == 0x1A || file_data[l] == 0xe5))
                --l;

            // now foo[i] is the last non-zero byte
            byte[] bar = new byte[l + 1];
            Array.Copy(file_data.ToArray(), bar, l + 1);

            return bar.Length > 0 ? bar : null;
        }


        // Returns TRUE if this is the first extent for a file
        bool is_first_extent(cpm_dir_entry dir_entry)
        {
            return ((_disk_type.disk_recs_per_extent() > 128) && (dir_entry.num_allocs > 4) && dir_entry.extent_nr == 1) ||
                    (dir_entry.extent_nr == 0);
        }



        // Check if 2 filenames match, using wildcard matching.
        // Only s1 can contain wildcard characters. * and ? are supported.
        // Note that A* E* is interpreted as A*
        // This doesn't work identically to CPM, but I prefer this as 
        // copying '*' will copy everything, rather than needing '*.*'

        int filename_equals(string s1, string s2, bool wildcards)
        {
            bool found_dot = false;  // have we found the dot separator between filename and type
            int i1 = 0;
            int i2 = 0;

            while (i1 < s1.Length && i2 < s2.Length)
            {
                // If it's a '*' wildcard it matches everything here onwards, so return equal
                // if we've already found the '.'. Otherwise keep searching from '.' onwards 
                if (wildcards && s1[i1] == '*')
                {
                    if (found_dot)
                    {
                        return 0;
                    }
                    else
                    {
                        i1 = s1.IndexOf('.');
                        // if wildcard has no extension e.g. T* then equal 
                        if (i1 < 0)
                            return 0;
                        i2 = s2.IndexOf('.');
                        if (i2 < 0)
                            i2 = s2.Length;
                    }
                }
                // ? matches 1 character, process next char 
                else if (wildcards && s1[i1] == '?')
                {
                    i1++;
                    i2++;
                    continue;
                }
                else
                {
                    if (s2[i2] == '.')
                        found_dot = true;
                    int result = Char.ToUpper(s1[i1]) - Char.ToUpper(s2[i2]);
                    // If chars are not equal, return not equal 
                    if (result > 0)
                        return result;
                }
                i1++;
                i2++;
            }
            // If equal, both will be at end of string 
            if (i1 == s1.Length && i2 == s2.Length)
                return 0;
            // Special case for filenames ending in '.' 
            //Treat ABC.and ABC as equal
            if (i1 == s1.Length && s2[i2] == '.' && (i2 + 1) == s2.Length)
                return 0;
            if (i2 == s2.Length && s1[i1] == '.' && (i1 + 1) == s1.Length)
                return 0;
            return 1;   // not equal 
        }


        // Find the directory entry related to a filename.
        //  If prev_entry != NULL, start searching from the next entry after prev_entry
        //  If wildcards = 1, allow wildcard characters* and ? to be used when matching to the filename
        cpm_dir_entry find_dir_by_filename(string full_filename, cpm_dir_entry prev_entry, bool wildcards)
        {
            int start_index = (prev_entry == null) ? 0 : prev_entry.index + 1;
            for (int i = start_index; i < _disk_type.disk_num_directories(); i++)
            {
                // Is this the first extent for a file? 
                //if (dir_table[i].valid && is_first_extent(dir_table[i]))
                if (sorted_dir_table[i].valid && is_first_extent(sorted_dir_table[i]))
                {
                    // If filename matches, return it 
                    //if (filename_equals(full_filename.Trim(), dir_table[i].full_filename.Trim(), wildcards) == 0)
                    if (filename_equals(full_filename.Trim(), sorted_dir_table[i].full_filename.Trim(), wildcards) == 0)
                    {
                        //return dir_table[i];
                        return sorted_dir_table[i];
                    }
                }
            }
            // No matching filename found 
            return null;
        }



        /*
         * Print nicely formatted directory listing 
         */
        void directory_list()
        {
            int file_count = 0;
            int kb_used = 0;
            int kb_free = 0;
            int entry_count = 0;
            Console.Write("Name     Ext   Length Used U At\n");

            cpm_dir_entry entry = null;
            int this_records = 0;
            int this_allocs = 0;
            int this_kb = 0;
            string last_filename = "";

            for (int i = 0; i < _disk_type.disk_num_directories(); i++)
            {
                /* Valid entries are sorted before invalid ones. So stop on first invalid */
                entry = sorted_dir_table[i];
                if (!entry.valid)
                {
                    break;
                }
                entry_count++;
                /* If this is the first record for this file, then reset the file totals */
                if (entry.full_filename.ToString() != last_filename)
                {
                    file_count++;
                    this_records = 0;
                    this_allocs = 0;
                    this_kb = 0;
                    last_filename = entry.full_filename.ToString();
                }

                this_records += entry.num_records;
                this_allocs += entry.num_allocs;

                /* If there are no more dir entries, print out the file details */
                if (entry.next_entry == null)
                {
                    this_kb += (this_allocs * _disk_type.disk_recs_per_alloc() * _disk_type.disk_data_sector_len()) / 1024;
                    kb_used += this_kb;

                    Console.Write("{0} {1} {2:d7}B {3:d3}K {4:d} {5}\n",
                        entry.filename,
                         entry.type,
                        this_records * _disk_type.disk_sector_len(),
                        this_kb,
                        entry.user,
                         (new string(entry.attribs)).Trim('\0'));
                }
            }
            for (int i = 0; i < _disk_type.disk_total_allocs(); i++)
            {
                if (alloc_table[i] == 0)
                {
                    kb_free += _disk_type.disk_recs_per_alloc() * _disk_type.disk_data_sector_len() / 1024;
                }
            }
            Console.Write("{0} file(s), occupying {1}K of {2}K total capacity\n",
                    file_count, kb_used, kb_used + kb_free);
            Console.Write("{0} directory entries and {1}K bytes remain\n",
                    _disk_type.disk_num_directories() - entry_count, kb_free);
        }




        //Convert allocation and record numbers into track and sector numbers

        public void convert_track_sector(int allocation, int record)
        {
            // Find the number of records this allocation and record number equals 
            //Each record = 1 sector.Divide number of records by number of sectors per track to get the track.
            //This works because we enforce that each sector is 128 bytes and each record is 128 bytes.

            //Note: For some disks the block size is not a multiple of the sectors/ track, so can't just
            //calculate allocs / track here.


            track = (allocation * _disk_type.disk_recs_per_alloc() + (record % _disk_type.disk_recs_per_alloc())) /
            _disk_type.disk_sectors_per_track() + _disk_type.disk_reserved_tracks();

            int logical_sector =
                    (allocation * _disk_type.disk_recs_per_alloc() + (record % _disk_type.disk_recs_per_alloc())) %
                    _disk_type.disk_sectors_per_track();

            if (VERBOSE)
                Console.Write("ALLOCATION[{0}], RECORD[{1}], LOGICAL[{2:x}], ", allocation, record, logical_sector);

            // Need to "skew" the logical sector into a physical sector 
            sector = _disk_type.disk_skew_sector(track, logical_sector);
        }


        int track;
        int sector;
        int offset;
        byte[] sector_data;
        // Read an allocation / record from disk.
        void read_sector(int alloc_num, int rec_num)
        {


            convert_track_sector(alloc_num, rec_num);
            offset = track * _disk_type.disk_track_len() + (sector - 1) * _disk_type.disk_sector_len();
            // For 8" floppy format data is offset from start of sector 
            offset += _disk_type.disk_off_data(track);

            if (VERBOSE)
                Console.Write("Reading from TRACK[{0}], SECTOR[{1}], OFFSET[{2:x}]\n", track, sector, offset);


            Buffer.BlockCopy(fileData, offset,
               sector_data, 0,
                   _disk_type.disk_data_sector_len());

            /*
            if (lseek(fd, offset, SEEK_SET) < 0)
            {
                error_exit(errno, "read_sector: Error seeking");
            }
            if (read(fd, buffer, disk_data_sector_len()) < 0)
            {
                error_exit(errno, "read_sector: Error on read");
            }
            */
            //Console.WriteLine(System.Text.Encoding.Default.GetString(sector_data));


        }



        //Convert a raw allocation into the int representing that allocation
        int get_raw_allocation(raw_dir_entry raw, int entry_nr)
        {
            if (_disk_type.disk_total_allocs() <= 256)
            {
                // an 8 bit allocation number 
                return raw.allocation[entry_nr];
            }
            else
            {
                // a 16 bit allocation number. Low byte first 
                return raw.allocation[entry_nr] | (raw.allocation[entry_nr + 1] << 8);
            }
        }


        // Convert each cpm directory entry (extent) into an structure that is
        // easier to work with.
        void raw_to_cpmdir(cpm_dir_entry entry)
        {
            raw_dir_entry raw = entry.raw_entry;
            entry.next_entry = null;
            entry.user = raw.user;
            entry.extent_nr = raw.extent_h * 32 + raw.extent_l;

            entry.filename = System.Text.Encoding.Default.GetString(raw.filename);

            if (entry.filename.Length > FILENAME_LEN)
                entry.filename = entry.filename.Substring(0, FILENAME_LEN);

            entry.type = "";
            for (int i = 0; i < TYPE_LEN; i++)
            {
                // remove the top bit as it encodes file attributes
                entry.type += (char)(raw.type[i] & 0x7f);
            }

            // If high bit is set on 1st TYPE char, then read-only, otherwise read-write 
            entry.attribs[0] = (raw.type[0] & 0x80) > 0 ? 'R' : 'W';
            // If high bit is set on 2nd TYPE char, then this is a "system/hidden" file 
            entry.attribs[1] = (raw.type[1] & 0x80) > 0 ? 'S' : ' ';
            entry.attribs[2] = '\0';

            entry.full_filename = entry.filename.Trim();

            // Only add a '.' if there really is an extension 
            if (entry.type[0] != ' ')
            {
                entry.full_filename += ".";
                entry.full_filename += entry.type.ToString();
            }
            entry.num_records = raw.num_records;
            int num_allocs = 0;
            for (int i = 0; i < ALLOCS_PER_EXT; i++)
            {
                int alloc_nr = get_raw_allocation(entry.raw_entry, i);
                if (_disk_type.disk_total_allocs() <= 256)
                {
                    // an 8 bit allocation number 
                    entry.allocation[i] = alloc_nr;
                }
                else
                {
                    // a 16 bit allocation number. 
                    entry.allocation[i / 2] = alloc_nr;
                    i++;
                }
                // zero allocation means there are no more allocations to come 
                if (alloc_nr == 0)
                    break;

                num_allocs++;
            }
            entry.num_allocs = num_allocs;
            entry.valid = true;
        }


        // Loads all of the directory entries into dir_table 
        // sorted pointers stored to sorted_dir_table
        // Related directory entries are linked through next_entry.
        void load_directory_table()
        {
            sector_data = new byte[MAX_SECT_SIZE];
            dir_table = new cpm_dir_entry[_disk_type.disk_num_directories()];
            sorted_dir_table = new cpm_dir_entry[_disk_type.disk_num_directories()];

            for (int sect_nr = 0; sect_nr < (_disk_type.disk_num_directories()) / _disk_type.disk_dirs_per_sector(); sect_nr++)
            {
                // Read each sector containing a directory entry 
                // All directory data is on first 16 sectors of TRACK 2
                int allocation = sect_nr / _disk_type.disk_recs_per_alloc();
                int record = (sect_nr % _disk_type.disk_recs_per_alloc());

                read_sector(allocation, record);

                for (int dir_nr = 0; dir_nr < _disk_type.disk_dirs_per_sector(); dir_nr++)
                {
                    // Calculate which directory entry number this is 
                    int index = sect_nr * _disk_type.disk_dirs_per_sector() + dir_nr;

                    if (dir_table[index] == null) dir_table[index] = new cpm_dir_entry();

                    cpm_dir_entry entry = dir_table[index];
                    entry.index = index;
                    entry.raw_entry = new raw_dir_entry();
                    /*
                    Buffer.BlockCopy(entry.raw_entry, 0,
                sector_data, (DIR_ENTRY_LEN * dir_nr)+1,
                    DIR_ENTRY_LEN);
                    */
                    // colocar os entries aqui

                    int d = (DIR_ENTRY_LEN * dir_nr);
                    entry.raw_entry.user = sector_data[d + 0];

                    Buffer.BlockCopy(sector_data, d + 1, entry.raw_entry.filename, 0, 8);

                    Buffer.BlockCopy(sector_data, d + 9, entry.raw_entry.type, 0, 3);

                    entry.raw_entry.extent_l = sector_data[d + 12];
                    entry.raw_entry.reserved = sector_data[d + 13];
                    entry.raw_entry.extent_h = sector_data[d + 14];
                    entry.raw_entry.num_records = sector_data[d + 15];
                    Buffer.BlockCopy(sector_data, d + 16, entry.raw_entry.allocation, 0, ALLOCS_PER_EXT);

                    sorted_dir_table[index] = entry;

                    if (entry.raw_entry.user <= MAX_USER)
                    {
                        raw_to_cpmdir(entry);

                        // Mark off the used allocations 
                        for (int alloc_nr = 0; alloc_nr < ALLOCS_PER_EXT; alloc_nr++)
                        {
                            int alloc = entry.allocation[alloc_nr];

                            // Allocation of 0, means no more allocations used by this entry 
                            if (alloc == 0)
                                break;

                            // otherwise mark the allocation as used 
                            alloc_table[alloc] = 1;
                        }
                    }
                }
            }

            // Create a list of pointers to the directory table, sorted by:
            //Valid, Filename, Type, Extent

            //qsort(&sorted_dir_table, disk_num_directories(), sizeof(cpm_dir_entry*), compare_sort_ptr);
            sorted_dir_table = sorted_dir_table.Where(p => p.full_filename != null).OrderBy(p => p.full_filename).ThenBy(p => p.extent_nr)
                .Concat(sorted_dir_table.Where(p => p.full_filename == null)).ToArray();

            for(int i =0; i < sorted_dir_table.Length; i++)
            {
                if (sorted_dir_table[i] != null)

                    Console.WriteLine("{0}\t{1}", sorted_dir_table[i].valid, sorted_dir_table[i].full_filename);
            }
            // link related directory entries 
            // No need to check last entry, it can't be related to anything 
            for (int i = 0; i < _disk_type.disk_num_directories() - 1; i++)
            {
                cpm_dir_entry entry = sorted_dir_table[i];
                cpm_dir_entry next_entry = sorted_dir_table[i + 1];

                if (entry.valid)
                {
                    // Check if there are more extents for this file 
                    if (next_entry.full_filename != null && entry.full_filename.ToString() == next_entry.full_filename.ToString())
                    {
                        Console.WriteLine("{0}\t{1}", entry.full_filename, next_entry.full_filename);
                        // If this entry is a full extent, and the next entry has an
                        //an entr nr +1
                        entry.next_entry = next_entry;
                    }
                }
            }
        }

        // Print raw directory table.

        void raw_directory_list()
        {
            Console.Write("IDX:U:FILENAME:TYP:AT:EXT:REC:[ALLOCATIONS]\n");
            for (int i = 0; i < _disk_type.disk_num_directories(); i++)
            {
                cpm_dir_entry entry = dir_table[i];
                if (entry.valid)
                {
                    Console.Write("{0:d3}:{1}:{2}:{3}:{4}:{5:n3}:{6:n3}:[",
                        entry.index,
                        entry.user, entry.filename, entry.type,
                        entry.attribs,
                        entry.extent_nr, entry.num_records);
                    for (int j = 0; i < ALLOCS_PER_EXT / 2; j++)    // Only 8 of the 16 entries are used 
                    {
                        if (j < ALLOCS_PER_EXT / 2 - 1)
                        {
                            Console.Write("{0},", entry.allocation[j]);
                        }
                        else
                        {
                            Console.Write("{0}", entry.allocation[j]);
                        }
                    }
                    Console.Write("]\n");
                }
            }
            Console.Write("FREE ALLOCATIONS:\n");
            int nr_output = 0;
            for (int i = 0; i < _disk_type.disk_total_allocs(); i++)
            {
                if (alloc_table[i] == 0)
                {
                    Console.Write("{0:d3} ", i);
                    if ((++nr_output % 16) == 0)
                    {
                        Console.Write("\n");
                    }
                }
            }
            Console.Write("\n");
        }

        public void cmd_chuser(String filename, int newuser)
        {
            change_user_file(filename, newuser);
        }

        void change_user_file(string cpm_filename, int newuser)
        {

            if (newuser < 0 || newuser > 15)
                return;

                cpm_dir_entry entry = find_dir_by_filename(cpm_filename, null, false);
            if (entry == null)
            {
                Console.WriteLine("Error renaming {0}", cpm_filename);
                return;
            }
            

            // Set user on all directory entries for this file to "new user" 
            do
            {
                entry.raw_entry.user = (byte)newuser;

                write_dir_entry(entry);
            } while ((entry = entry.next_entry) != null);
        }



        public bool cmd_rename(string from, string to)
        {
            return rename_file(from, to);
        }

        bool rename_file(string cpm_filename, string new_filename)
        {
            cpm_dir_entry entry = find_dir_by_filename(cpm_filename, null, false);
            if (entry == null)
            {
                Console.WriteLine("Error renaming {0}", cpm_filename);
                return false;
            }

            string valid_filename = validate_cpm_filename(new_filename);

            // rename all directory entries for this file
            do
            {
                copy_filename(entry.raw_entry, valid_filename);

                write_dir_entry(entry);
            } while ((entry = entry.next_entry) != null);

            return true;
        }

        public void cmd_mkbin(string cpm_filename, byte[] data)
        {
            copy_to_cpm(cpm_filename, data);
        }

        // Copy file from host to Altair CPM disk image 
        void copy_to_cpm(string cpm_filename, byte[] data)
        {
            sector_data = new byte[MAX_SECT_SIZE];
            string valid_filename = validate_cpm_filename(cpm_filename);

            if (cpm_filename.ToLower() != valid_filename.ToLower())
            {
                Console.WriteLine("Converting filename {0} to {1}\n", cpm_filename, valid_filename);
            }
            if (find_dir_by_filename(valid_filename, null, false) != null)
            {
                Console.WriteLine("Error creating file {0}", valid_filename);
            }

            int rec_nr = 0;
            int nr_extents = 0;
            int allocation = 0;
            int nr_allocs = 0;

            cpm_dir_entry dir_entry = null;

            // Fill the sector with Ctrl-Z (EOF) in case not fully filled by read from host
            Buffer.BlockCopy(Enumerable.Repeat((byte)0x1a, _disk_type.disk_data_sector_len()).ToArray(), 0,
                sector_data, 0, _disk_type.disk_data_sector_len());

            int src_offset = 0;

            //while ((nbytes = read(host_fd, &sector_data, disk_data_sector_len())) > 0)
            while (src_offset < data.Length)
            {
                int current_len = Math.Min(_disk_type.disk_data_sector_len(), (data.Length - src_offset));

                Buffer.BlockCopy(data, src_offset,
                sector_data, 0, current_len);

                src_offset += _disk_type.disk_data_sector_len();

                // Is this a new Extent (i.e directory entry) ? 
                if ((rec_nr % _disk_type.disk_recs_per_extent()) == 0)
                {
                    // if there is a previous directory entry, write it to disk 
                    if (dir_entry != null)
                    {
                        raw_to_cpmdir(dir_entry);
                        write_dir_entry(dir_entry);
                    }
                    // Get new directory entry 
                    dir_entry = find_free_dir_entry();
                    if (dir_entry == null)
                    {
                        Console.WriteLine("Error writing {0}: No free directory entries", cpm_filename);
                    }
                    // Initialise the directory entry 
                    dir_entry.raw_entry = new raw_dir_entry();

                    copy_filename(dir_entry.raw_entry, valid_filename);
                    nr_allocs = 0;
                }
                // Is this a new allocation? 
                if ((rec_nr % _disk_type.disk_recs_per_alloc()) == 0)
                {
                    allocation = find_free_alloc();
                    if (allocation < 0)
                    {
                        // No free allocations! 
                        // write out directory entry(if it has any allocations) before exit
                        if (get_raw_allocation(dir_entry.raw_entry, 0) != 0)
                        {
                            raw_to_cpmdir(dir_entry);
                            write_dir_entry(dir_entry);
                        }
                        Console.WriteLine("Error writing {0}: No free allocations", valid_filename);
                    }
                    set_raw_allocation(dir_entry.raw_entry, nr_allocs, allocation);
                    nr_allocs++;
                }
                dir_entry.raw_entry.num_records = (byte)((rec_nr % RECORD_MAX) + 1);
                dir_entry.raw_entry.extent_l = (byte)(nr_extents % 32);
                dir_entry.raw_entry.extent_h = (byte)(nr_extents / 32);

                write_sector(allocation, rec_nr);

                Buffer.BlockCopy(Enumerable.Repeat((byte)0x1a, _disk_type.disk_data_sector_len()).ToArray(), 0,
                    sector_data, 0, _disk_type.disk_data_sector_len());

                rec_nr++;

                if ((rec_nr % RECORD_MAX) == 0)
                {
                    nr_extents++;
                }
            }
            // File is done. Write out the last directory entry 
            raw_to_cpmdir(dir_entry);
            write_dir_entry(dir_entry);
        }


        // Write an newly formatted sector
        // Must contain all sector data, including checksum, stop bytes etc.
        void write_raw_sector(int track, int sector)
        {
            // offset to start of sector 
            int sector_offset = track * _disk_type.disk_track_len() + (sector - 1) * _disk_type.disk_sector_len();

            if (VERBOSE)
                Console.Write("Writing to TRACK[{0}], SECTOR[{1}], OFFSET[{2}] (RAW)\n", track, sector, sector_offset);

            // write the data 
            /*
            if (lseek(fd, sector_offset, SEEK_SET) < 0)
            {
                error_exit(errno, "write_raw_sector: Error seeking");
            }
            if (write(fd, buffer, disk_sector_len()) < 0)
            {
                error_exit(errno, "write_raw_sector: Error on write");
            }
            */
            Buffer.BlockCopy(sector_data, 0,
                fileData, sector_offset,
                _disk_type.disk_data_sector_len());
        }



        // Erase a file

        public void cmd_rmdir(string cpm_filename)
        {
            erase_file(cpm_filename);
        }
        void erase_file(string cpm_filename)
        {
            cpm_dir_entry entry = find_dir_by_filename(cpm_filename, null, false);
            if (entry == null)
            {
                Console.WriteLine("Error erasing {0}", cpm_filename);
                return;
            }
            // Set user on all directory entries for this file to "DELETED" 
            do
            {
                entry.raw_entry.user = DELETED_FLAG;
                write_dir_entry(entry);
            } while ((entry = entry.next_entry) != null);
        }




        // Find an unused directory entry.

        cpm_dir_entry find_free_dir_entry()
        {
            for (int i = 0; i < _disk_type.disk_num_directories(); i++)
            {
                if (!dir_table[i].valid)
                {
                    return dir_table[i];
                }
            }
            return null;
        }



        // Find a free allocation. Mark is as used.

        int find_free_alloc()
        {
            for (int i = 0; i < _disk_type.disk_total_allocs(); i++)
            {
                if (alloc_table[i] == 0)
                {
                    alloc_table[i] = 1;
                    return i;
                }
            }
            return -1;
        }

        string truncate(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value)) return value;
            return value.Length <= maxLength ? value : value.Substring(0, maxLength);
        }

        // Copy a "file.ext" format filename to a directory entry
        // converting to upper case.

        void copy_filename(raw_dir_entry entry, string filename)
        {
            String _filename = "";
            String _ext = "";

            filename = filename.ToUpper();

            int dotpos = filename.IndexOf('.');
            if (dotpos > -1)
            {
                _filename = filename.Substring(0, dotpos);

                if (dotpos < filename.Length - 1)
                    _ext = filename.Substring(dotpos + 1);
            }

            _filename = truncate(_filename, 8);
            _ext = truncate(_ext, 3);

            _filename = _filename.PadRight(8);
            _ext = _ext.PadRight(3);

            entry.filename = Encoding.ASCII.GetBytes(_filename);
            entry.type = Encoding.ASCII.GetBytes(_ext);

        }

        // Write the directory entry to the disk image.

        void write_dir_entry(cpm_dir_entry entry)
        {
            sector_data = new byte[MAX_SECT_SIZE];

            int allocation = entry.index / _disk_type.disk_dirs_per_alloc();
            int record = entry.index / _disk_type.disk_dirs_per_sector();
            // start_index is the index of this directory entry that is at 
            //the beginning of the sector

            int start_index = entry.index / _disk_type.disk_dirs_per_sector() * _disk_type.disk_dirs_per_sector();
            for (int i = 0; i < _disk_type.disk_dirs_per_sector(); i++)
            {
                // copy all directory entries for the sector 

                byte[] raw_byte_entry = dir_table[start_index + i].raw_entry.ToByteArray();

                Buffer.BlockCopy(raw_byte_entry, 0, sector_data, i * DIR_ENTRY_LEN, DIR_ENTRY_LEN);
            }

            write_sector(allocation, record);
        }


        public byte[] get_sector()
        {
            return fileData;
        }




        // Write an allocation / record to disk

        void write_sector(int alloc_num, int rec_num)
        {

            char[] checksum_buf = new char[7];       // additional checksum data for track 6 onwards 

            convert_track_sector(alloc_num, rec_num);

            // offset to start of sector 
            int sector_offset = track * _disk_type.disk_track_len() + (sector - 1) * _disk_type.disk_sector_len();

            // Get the offset to start of data, relative to the start of sector 
            int data_offset = sector_offset + _disk_type.disk_off_data(track);

            if (VERBOSE)
                Console.Write("Writing to TRACK[{0}], SECTOR[{0}], OFFSET[{0}]\n", track, sector, data_offset);

            // write the data 
            /*
            if (lseek(fd, data_offset, SEEK_SET) < 0)
            {
                error_exit(errno, "write_sector: Error seeking");
            }
            if (write(fd, buffer, disk_data_sector_len()) < 0)
            {
                error_exit(errno, "write_sector: Error on write");
            }
            */

            Buffer.BlockCopy(sector_data, 0,
        fileData, data_offset,
        _disk_type.disk_data_sector_len());

            if (_disk_type.disk_csum_method(track) > 0)
            {
                // calculate the checksum and offset if required 
                short csum = _disk_type.calc_checksum(sector_data, 0);
                int csum_offset = sector_offset + _disk_type.disk_off_csum(track);

                // For track 6 onwards, some non-data bytes are added to the checksum 
                if (track >= 6)
                {
                    /*
                    if (lseek(fd, sector_offset, SEEK_SET) < 0)
                    {
                        error_exit(errno, "write_sector: Error seeking");
                    }

                    if (read(fd, checksum_buf, 7) < 0)
                    {
                        error_exit(errno, "write_sector: Error on read checksum bytes");
                    }
                    */
                    Buffer.BlockCopy(fileData, sector_offset,
                        checksum_buf, 0, 7);

                    if (_disk_type.disk_csum_method(track) > 0)

                        csum += (short)checksum_buf[2];
                    csum += (short)checksum_buf[3];
                    csum += (short)checksum_buf[5];
                    csum += (short)checksum_buf[6];
                }

                // write the checksum 
                /*
                if (lseek(fd, csum_offset, SEEK_SET) < 0)
                {
                    error_exit(errno, "write_sector: Error seeking");
                }
                if (write(fd, &csum, 1) < 0)
                {
                    error_exit(errno, "write_sector: Error on write");
                }
                */
                fileData[csum_offset] = (byte)csum;
            }
        }


        //
        // Check that the passed in filename can be represented as "8.3"
        // CPM Manual says that filenames cannot include:
        // < > . , ; : = ? * [ ] % | ( ) / \
        // while all alphanumerics and remaining special characters are allowed.
        // We'll also enforce that it is at least a "printable" character
        bool isprint(char c)
        {
            return !Char.IsControl(c) || Char.IsWhiteSpace(c);
        }
        string validate_cpm_filename(string filename)
        {
            string in_char = filename;
            string out_char = "";
            bool found_dot = false;
            int char_count = 0;
            int ext_count = 0;

            int in_char_index = 0;

            while (in_char_index < in_char.Length)
            {
                if (isprint(in_char[in_char_index]) &&
                    in_char[in_char_index] != '<' && in_char[in_char_index] != '>' &&
                    in_char[in_char_index] != ',' && in_char[in_char_index] != ';' &&
                    in_char[in_char_index] != ':' && in_char[in_char_index] != '?' &&
                    in_char[in_char_index] != '*' && in_char[in_char_index] != '[' &&
                    in_char[in_char_index] != '[' && in_char[in_char_index] != ']' &&
                    in_char[in_char_index] != '%' && in_char[in_char_index] != '|' &&
                    in_char[in_char_index] != '(' && in_char[in_char_index] != ')' &&
                    in_char[in_char_index] != '/' && in_char[in_char_index] != '\\')
                {
                    if (in_char[in_char_index] == '.')
                    {
                        if (found_dot)
                        {
                            // Only process first '.' in filename 
                            in_char_index++;
                            continue;
                        }
                        found_dot = true;
                    }
                    // It's a valid filename character 
                    // copy the character 
                    out_char += char.ToUpper(in_char[in_char_index]);
                    char_count++;
                    // If we have a file filename (excluding ext), but not found a dot 
                    // then add a dot and ignore everything up until the next dot 
                    if (char_count == FILENAME_LEN && !found_dot)
                    {
                        // make sure filename contains a separator 
                        out_char += '.';
                        char_count++;
                        found_dot = true;
                        // skip multiple consecutive dots in filename e.g. m80......com 
                        while (in_char_index < in_char.Length && in_char[in_char_index] == '.')
                            in_char_index++;
                        // now go looking for the separator 
                        while (in_char_index < in_char.Length && in_char[in_char_index] != '.')
                            in_char_index++;
                    }
                    if (char_count == FULL_FILENAME_LEN - 1)
                    {
                        // otherwise end of filename. done 
                        break;
                    }
                    if (found_dot && ext_count++ == TYPE_LEN)
                    {
                        // max extension length reached 
                        break;
                    }
                }
                in_char_index++;
            }

            return out_char;
        }

        /*
        // Sort by valid = 1, filename+type, then extent_nr 

        int compare_sort_ptr(const void* a, const void* b)
        {
            cpm_dir_entry** first = (cpm_dir_entry**)a;
            cpm_dir_entry** second = (cpm_dir_entry**)b;
            // If neither a valid, they are equal 
            if (!(*first).valid && !(*second).valid)
            {
                return 0;
            }
            // Otherwise, sort on valid, filename, extent_nr 
            int result = (*second).valid - (*first).valid;
            if (result == 0)
            {
                result = strcmp((*first).full_filename, (*second).full_filename);
            }
            if (result == 0)
            {
                result = (*first).extent_nr - (*second).extent_nr;
            }
            return result;
        }
        */

        // Set the allocation number in the raw directory entry
        // For <=256 total allocs, this is set in the first 8 entries of the allocation array
        // Otherwise each entry in the allocation array is set in pairs of low byte, high byte

        void set_raw_allocation(raw_dir_entry entry, int entry_nr, int alloc)
        {
            if (_disk_type.disk_total_allocs() <= 256)
            {
                entry.allocation[entry_nr] = (byte)alloc;
            }
            else
            {
                entry.allocation[entry_nr * 2] = (byte)(alloc & 0xff);
                entry.allocation[entry_nr * 2 + 1] = (byte)((alloc >> 8) & 0xff);
            }
        }

        /*
        public void do_main()
        {
            
            //mode_t open_umask = 0666;

            // command line options 
            int opt;
            bool do_dir = false, do_raw = false, do_get = false;
            bool do_put = false, do_help = false, do_format = false;
            bool do_erase = false, do_multiput = false, do_multiget = false;
            bool has_type = false;               // has the user specified a type? 
            int text_mode = -1;             // default to auto-detect text/binary 
            string disk_filename = "";     // Altair disk image filename 
            string from_filename = "";   // filename to get / put 
            string to_filename = "";     // filename to get / put 
            string image_type = "";               // manually specify type of disk image 

            //dir 
            from_filename = "ASM.COM";
            do_get = true;
            do_dir = true;
            do_format = true;
            

            //_disk_type = new mts8in_disk_type();
            //_disk_type = new mits8in8m_disk_type();


            disk_filename = @"d:\backup\Desktop\RetroProjects\simh\Altair\cpm2.dsk";

            using (BinaryReader b = new BinaryReader(File.Open(disk_filename, FileMode.Open)))
            {
                fileData = Utils.ReadAllBytes(b);
            }


            // Try and work out what format this image is 
            if (has_type)
            {
                _disk_type = Disk_Type.disk_set_type(image_type);
            }
            else
            {
                // try to auto-detect type 
                if (Disk_Type.disk_detect_type(fileData.Length) < 0)
                {
                    if (!do_format)
                    {
                        Console.Write("Unknown disk image type. Use -h to see supported types and -T to force a type.");
                        return;
                    }
                    else
                    {
                        // For format we default to mits 8IN
                        _disk_type = new mits8in_disk_type();
                        Console.Write("Defaulting to disk type: {0}\n", _disk_type.type);
                    }
                }
            }

            _disk_type = new fdd1mb_simh_disk_type();

            if (VERBOSE)
                _disk_type.disk_dump_parameters();

            // Initial allocation table 
            for (int i = 0; i < _disk_type.da; ++i)
                alloc_table[i] = 1;

            do_format = false;
            // Read all directory entries - except for format command 
            if (!do_format)
            {
                load_directory_table();
            }


            // Formatted directory listing 
            if (do_dir)
            {
                directory_list();
                //exit(EXIT_SUCCESS);
            }

            // Copy file from disk image to host 
            if (do_get)
            {
                // does the file exist in CPM? 
                cpm_dir_entry entry = find_dir_by_filename(from_filename, null, false);
                if (entry == null)
                {
                    Console.Write("Error copying file {0}\n", from_filename);
                }
                // finally copy the file from disk image
                byte[] file_data = copy_from_cpm(entry, text_mode);

                File.WriteAllBytes("teste.dat", file_data);
                //exit(EXIT_SUCCESS);
            }
        }
        
        int domain(int argc, char** argv)
        {
            int open_mode;  // read or write depending on selected options 
            mode_t open_umask = 0666;

            // command line options 
            int opt;
            bool do_dir = false, do_raw = false, do_get = false;
            bool do_put = false, do_help = false, do_format = false;
            bool do_erase = false, do_multiput = false, do_multiget = false;
            int has_type = 0;               // has the user specified a type? 
            int text_mode = -1;             // default to auto-detect text/binary 
            char* disk_filename = NULL;     // Altair disk image filename 
            char from_filename[PATH_MAX];   // filename to get / put 
            char to_filename[PATH_MAX];     // filename to get / put 
            char* image_type;               // manually specify type of disk image 


            // Default to 8" floppy. This default should not be used. Just here for safety 
            disk_type = &MITS8IN_FORMAT;

            // parse command line options 
            while ((opt = getopt(argc, argv, "drhgGpPvFetbT:")) != -1)
            {
                switch (opt)
                {
                    case 'h':
                        do_help = true;
                        break;
                    case 'd':
                        do_dir = true;
                        //open_mode = O_RDONLY;
                        break;
                    case 'r':
                        do_raw = true;
                        //open_mode = O_RDONLY;
                        break;
                    case 'g':
                        do_get = true;
                        //open_mode = O_RDONLY;
                        break;
                    case 'G':
                        do_multiget = true;
                        //open_mode = O_RDONLY;
                        break;
                    case 'p':
                        do_put = true;
                        //open_mode = O_RDWR;
                        break;
                    case 'P':
                        do_multiput = true;
                        //open_mode = O_RDWR;
                        break;
                    case 'v':
                        VERBOSE = true;
                        break;
                    case 'e':
                        do_erase = true;
                        //open_mode = O_RDWR;
                        break;
                    case 'F':
                        do_format = true;
                        //open_mode = O_WRONLY | O_CREAT;
                        break;
                    case 't':
                        text_mode = true;
                        break;
                    case 'b':
                        text_mode = false;
                        break;
                    case 'T':
                        has_type = true;
                        image_type = optarg;
                        break;
                    case '?':
                        exit(EXIT_FAILURE);
                }
            }
            // make sure only one option is selected 
            int nr_opts = do_dir + do_raw + do_help +
                          do_put + do_get + do_format +
                          do_erase + do_multiget + do_multiput;
            if (nr_opts > 1)
            {
                fConsole.Write(stderr, "%s: Too many options supplied.\n", basename(argv[0]));
                exit(EXIT_FAILURE);
            }
            // default to directory listing if no option supplied 
            if (nr_opts == 0)
            {
                do_dir = 1;
            }
            if (do_help)
            {
                print_usage(argv[0]);
                exit(EXIT_SUCCESS);
            }

            // get the disk image filename 
            if (optind == argc)
            {
                fConsole.Write(stderr, "%s: <disk_image> not supplied.\n", basename(argv[0]));
                exit(EXIT_FAILURE);
            }
            else
            {
                // get the Altair disk image filename 
                disk_filename = argv[optind++];
            }

            // Get and Put need a from_filename and an optional to_filename
            // Erase just needs a from_filename 
            if (do_get || do_put || do_erase)
            {
                if (optind == argc)
                {
                    fConsole.Write(stderr, "%s: <filename> not supplied\n", basename(argv[0]));
                    exit(EXIT_FAILURE);
                }
                else
                {
                    strcpy(from_filename, argv[optind++]);
                    if (!do_erase && optind < argc)
                    {
                        strcpy(to_filename, argv[optind++]);
                    }
                    else
                    {
                        strcpy(to_filename, from_filename);
                    }
                }
            }
            // For multiget and multi-put, just make sure at least 1 filename supplied 
            // Filenames will be processed later 
            if (do_multiget || do_multiput)
            {
                if (optind == argc)
                {
                    fConsole.Write(stderr, "%s: <filename ...> not supplied\n", basename(argv[0]));
                    exit(EXIT_FAILURE);
                }
            }
            else if (optind != argc)
            {
                fConsole.Write(stderr, "%s: Too many arguments supplied.\n", basename(argv[0]));
                exit(EXIT_FAILURE);
            }

            //
             * Start of processing
             
            int fd_img = -1;        // fd of disk image 

            // Initialise allocation tables. First 2 allocs are reserved 
            //alloc_table[0] = alloc_table[1] = 1;

            // Open the Altair disk image
            if ((fd_img = open(disk_filename, open_mode, open_umask)) < 0)
            {
                error_exit(errno, "Error opening file %s", disk_filename);
            }

            // Try and work out what format this image is 
            if (has_type)
            {
                disk_set_type(image_type);
            }
            else
            {
                // try to auto-detect type 
                if (disk_detect_type(fd_img) < 0)
                {
                    if (!do_format)
                    {
                        error_exit(0, "Unknown disk image type. Use -h to see supported types and -T to force a type.");
                    }
                    else
                    {
                        // For format we default to mits 8IN
                        disk_type = &MITS8IN_FORMAT;
                        fConsole.Write(stderr, "Defaulting to disk type: %s\n", _disk_type.type);
                    }
                }
            }

            if (VERBOSE)
                disk_dump_parameters();

            // Initial allocation table 
            for (int i = 0; i < _disk_type.da; ++i)
                alloc_table[i] = 1;

            // Read all directory entries - except for format command 
            if (!do_format)
            {
                load_directory_table(fd_img);
            }

            // Raw Directory Listing 
            if (do_raw)
            {
                raw_directory_list();
                exit(EXIT_SUCCESS);
            }

            // Formatted directory listing 
            if (do_dir)
            {
                directory_list();
                exit(EXIT_SUCCESS);
            }

            // Copy file from disk image to host 
            if (do_get)
            {
                // does the file exist in CPM? 
                cpm_dir_entry* entry = find_dir_by_filename(basename(from_filename), NULL, 0);
                if (entry == NULL)
                {
                    error_exit(ENOENT, "Error copying file %s", from_filename);
                }
                // Try and remove file file we are about to get 
                if ((unlink(to_filename) < 0) && (errno != ENOENT))
                {
                    error_exit(errno, "Error removing old file %s", to_filename);
                }
                // open file to save into 
                int fd_file = open(to_filename, O_CREAT | O_WRONLY, 0666);
                if (fd_file < 0)
                {
                    error_exit(errno, "Error opening file %s", from_filename);
                }
                // finally copy the file from disk image
                copy_from_cpm(fd_img, fd_file, entry, text_mode);
                exit(EXIT_SUCCESS);
            }

            // Copy multiple files from disk image to host 
            if (do_multiget)
            {
                while (optind != argc)
                {
                    int idx = 0;
                    int file_found = 0;
                    cpm_dir_entry* entry = NULL;

                    strcpy(from_filename, argv[optind++]);
                    // process all filenames 
                    while (1)
                    {
                        // The filename may contain wildcards. If so, loop for each expanded filename 
                        entry = find_dir_by_filename(from_filename, entry, 1);

                        if (entry == NULL)
                        {
                            // error exit if there is not at least one file copied 
                            // otherwise no more files to copy. copy is complete 
                            if (!file_found)
                                error_exit(ENOENT, "Error copying %s", from_filename);
                            else
                                break;
                        }
                        char* this_filename = entry.full_filename;
                        file_found = 1;
                        // delete the host file we are about to copy into 
                        if ((unlink(this_filename) < 0) && (errno != ENOENT))
                        {
                            error_exit(errno, "Error removing old file %s", this_filename);
                        }
                        // create the file to copy into 
                        int fd_file = open(this_filename, O_CREAT | O_WRONLY, 0666);
                        if (fd_file < 0)
                        {
                            error_exit(errno, "Error opening file %s", this_filename);
                        }
                        // copy it 
                        copy_from_cpm(fd_img, fd_file, entry, text_mode);
                        close(fd_file);
                    }
                }
                exit(EXIT_SUCCESS);
            }

            // Copy file from host to disk image 
            if (do_put)
            {
                int fd_file = open(from_filename, O_RDONLY);
                if (fd_file < 0)
                {
                    error_exit(errno, "Error opening file %s", from_filename);
                }
                copy_to_cpm(fd_img, fd_file, basename(to_filename));
                exit(EXIT_SUCCESS);
            }

            // Copy multiple files from host to disk image 
            if (do_multiput)
            {
                // process for each file passed on the command file 
                while (optind != argc)
                {
                    strcpy(from_filename, argv[optind++]);
                    strcpy(to_filename, from_filename);

                    int fd_file = open(from_filename, O_RDONLY);
                    if (fd_file < 0)
                    {
                        error_exit(errno, "Error opening file %s", from_filename);
                    }
                    copy_to_cpm(fd_img, fd_file, basename(to_filename));
                }
                exit(EXIT_SUCCESS);
            }

            // erase a single file from the disk image 
            if (do_erase)
            {
                erase_file(fd_img, from_filename);
            }

            // format and existing image or create a newly formatted image 
            if (do_format)
            {
                // Call the disk-specific format function 
                disk_format_disk(fd_img);
                exit(EXIT_SUCCESS);
            }

            return 0;
        }

        //Usage information
         
        void print_usage(char* argv0)
        {
            char* progname = basename(argv0);
            Console.Write("%s: -[d|r|F]Tv      <disk_image>\n", progname);
            Console.Write("%s: -[g|p|e][t|b]Tv <disk_image> <src_filename> [dst_filename]\n", progname);
            Console.Write("%s: -[G|P][t|b]Tv   <disk_image> <filename ...> \n", progname);
            Console.Write("%s: -h\n", progname);
            Console.Write("\t-d\tDirectory listing (default)\n");
            Console.Write("\t-r\tRaw directory listing\n");
            Console.Write("\t-F\tFormat existing or create new disk image. Defaults to %s\n", MITS8IN_FORMAT.type);
            Console.Write("\t-g\tGet - Copy file from Altair disk image to host\n");
            Console.Write("\t-p\tPut - Copy file from host to Altair disk image\n");
            Console.Write("\t-G\tGet Multiple - Copy multiple files from Altair disk image to host\n");
            Console.Write("\t  \t               wildcards * and ? are supported e.g '*.COM'\n");
            Console.Write("\t-P\tPut Multiple - Copy multiple files from host to Altair disk image\n");
            Console.Write("\t-e\tErase a file\n");
            Console.Write("\t-t\tPut/Get a file in text mode\n");
            Console.Write("\t-b\tPut/Get a file in binary mode\n");
            Console.Write("\t-T\tDisk image type. Auto-detected if possible. Supported types are:\n");
            Console.Write("\t\t\t* %s - MITS 8\" Floppy Disk (Default)\n", MITS8IN_FORMAT.type);
            Console.Write("\t\t\t* %s - MITS 5MB Hard Disk\n", MITS5MBHDD_FORMAT.type);
            Console.Write("\t\t\t* %s - Tarbell Floppy Disk\n", TARBELLFDD_FORMAT.type);
            Console.Write("\t\t\t* %s - FDC+ 1.5MB Floppy Disk\n", FDD15MB_FORMAT.type);
            Console.Write("\t\t\t* %s - FDC+ 8MB \"Floppy\" Disk\n", MITS8IN8MB_FORMAT.type);
            Console.Write("\t\t\t* %s - MITS 5MBH, 1024 directory\n", MITS5MBHDD1024_FORMAT.type);
            Console.Write("\t-v\tVerbose - Prints sector read/write information\n");
            Console.Write("\t-h\tHelp\n");
        }

        */
    }
}
