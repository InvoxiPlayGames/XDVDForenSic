XDVDForenSic - data "recovery" tool for messed up XDVDFS (Xbox/XGD/"XISO") media

usage:
  ./XDVDForenSic path/to/damaged.img [folder start address] [list/extract]
make a complete image with ddrescue (or similar) first!!
"folder start address" should be the hexadecimal starting address in hexadecimal
e.g. 0x28800

to find folders, search for valid filenames to see what sticks, and find
the start of the table (they're aligned to 0x800 byte boundaries)

"list" will list files, "extract" will extract them to disk

it is highly recommended you run list before running extract, as you may
end up creating corrupted files/folders on your PC if you do not

this is a work in progress tool, there's a lot i want to add
