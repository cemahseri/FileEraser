# FileEraser
Sanitize your files with NIST SP 800-88 Rev. 1 standard, which recommends to overwrite file's content with 0. In this implementation, I've also added verification process. After the first and the only pass, program checks file's content is ful of 0x00 bytes, which is full 0 binary zeros. If verification fails, program skips deleting that file.

# Usage
Just drag and drop the folder you want to delete, to the executable file.
Or alternatively, you can specify a path from the command line.
```
FileEraser.exe C:\path\to\your\mom
```
