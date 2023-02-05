using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace altair_disk_manager.altair_disk_image
{
    /* Configuration for MITS 8" controller which writes to the raw sector */
    public class disk_offsets
    {
        public int start_track { get; set; }        /* starting track which this offset applies */
        public int end_track { get; set; }          /* ending track */
        public int off_data { get; set; }           /* offset of the data portion */
        public int off_track_nr { get; set; }       /* offset of track number */
        public int off_sect_nr { get; set; }        /* offset of sector number */
        public int off_stop { get; set; }           /* offset of stop byte */
        public int off_zero { get; set; }           /* offset of zero byte */
        public int off_csum { get; set; }           /* offset of checksum */
        public int csum_method { get; set; }        /* Checksum method. Only supports method 1 Altair 8" */

        public disk_offsets() { }
        public disk_offsets(int _start_track, int _end_track, int _off_data, int _off_track_nr, int _off_sect_nr, int _off_stop, int _off_zero, int _off_csum, int _csum_method)
        {
            start_track = _start_track;
            end_track = _end_track;
            off_data = _off_data;
            off_track_nr = _off_track_nr;
            off_sect_nr = _off_sect_nr;
            off_stop = _off_stop;
            off_zero = _off_zero;
            off_csum = _off_csum;
            csum_method = _csum_method;
        }
    };
}
