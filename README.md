# CBZTool

CBZTool is an easy command line tool for creating and manipulating Comic Books stored in the .cbz and .cbr formats.

It can be used to extract pages from books, create new books from image files, combine multiple books into one, convert comics to PDF, and more.

## Installing

Stable builds will be periodically posted on the [Releases](https://github.com/dan200/CBZTool/releases) page. Simply extract the contents to a folder of your choice and invoke CBZTool.exe from the command line.
Alternatively, you may compile the solution file provided in any modern version of Visual Studio.

## Example usage

Extract all the images from a book:
```
CBZTool extract MyComic.cbz
```

Create a new book from a folder of images:
```
CBZTool compress MyFolder
```

Combine the first 6 pages of one book with the second 6 pages of another:
```
CBZTool extract MyFirstComic.cbz -o MyCombinedComic -p 1-6
CBZTool extract MySecondComic.cbz -o MyCombinedComic -p 7-12 -a
CBZTool compress MyCombinedComic
```

Create a copy of a book with improved image quality:
```
CBZTool extract MyComic.cbz -o MyEnhancedComic.cbz -denoise -whitebalance
```

## All options

```
CBZTool extract PATH... [options]
  -o [path]         Specify the directory, CBZ or PDF file to extract to (defaults to the input path minus the extension)
  -p [range]        Specify the range of pages to extract (ex: 1-10 2,4,6 7-*) (default=*)
  -a                Appends the extracted pages to the end of the directory, instead of replacing them (default=0)
  -denoise          Runs a noise reduction algorithm on the images when extracting (default=0)
  -whitebalance     Runs a white balancing algorithm on the images when extracting (default=0)
  -flipX/Y          Flips the images when extracting (default=0)
  -rot90/180/270    Rotates the images when extracting (default=0)
  -metadata         Specify that metadata (ComicInfo.xml) should also be extracted (default=0)

CBZTool compress PATH... [options]
  -o [directory]    Specify the file to compress to (defaults to the input path with the .cbz extension appended)
  -p [range]        Specify the range of pages to compress (ex: 1-10 2,4,6 7-*) (default=*)
  -a                Appends the extracted pages to the end of the archive, instead of replacing it (default=0)
  -metadata         Specify that metadata (ComicInfo.xml) should also be added (default=0)
```
