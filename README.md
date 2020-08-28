# FileEraser
Wipe your files with DoD 5220.22-M (E) method, which overwrites file's content with 0's in first pass and verifies, then overwrites 1's in second pass and verifies, and finally overwrites with random bytes and verifies. In this implementation of DoD 5220.22-M (E), it doesn't verify the third pass, because why? I don't see any reason doing that, since if two passes are verified correctly, file is gone for sure. Anyways, I might add verification for the third pass later.

# Usage
Just drag and drop the folder you want to delete, to the executable file.
Or alternatively, you can specify a path from the command line.
```
FileEraser.exe C:\path\to\your\mom
```

# To-Do
- Add verification for third pass.
- Improve performance.
