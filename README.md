# FileEraser
Sanitize your files with NIST SP 800-88 Rev. 1 standard, which recommends to overwrite file's content with full 0x00 bytes. In this implementation, I've also added verification process. After the first and the only pass, program checks if file's content is full of 0x00 bytes, which is full binary zeros. If verification fails, program skips deleting that file.
>For storage devices containing magnetic media, a single overwrite pass with a fixed pattern such as binary zeros typically hinders recovery of data even if state of the art laboratory techniques are applied to attempt to retrieve the data.
>
>&mdash; [NIST SP 800-88 Rev. 1 - 2.4 Trends in Sanitization](https://nvlpubs.nist.gov/nistpubs/SpecialPublications/NIST.SP.800-88r1.pdf)

# Usage
Just drag and drop the folder(s) you want to delete, to the executable file.
Or alternatively, you can specify a path(s) from the command line.
```
FileEraser.exe C:\path\to\your\mom C:\another\path\to\your\mom C:\still\path\to\your\mom
```
