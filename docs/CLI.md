# Command Line And Git

```powershell
DiffBeacon.exe --help
DiffBeacon.exe left.txt right.txt
DiffBeacon.exe --mode text left.txt right.txt
DiffBeacon.exe --mode folder C:\Left C:\Right
DiffBeacon.exe --mode binary left.bin right.bin
DiffBeacon.exe --mode table left.csv right.csv
DiffBeacon.exe --check left.bin right.bin
```

`--check` compares entire files byte-for-byte using bounded buffers. Exit 0 means equal, 1 means different, and 2 means invalid input or an IO error. GUI mode exits 0 after normal closure; it is not an automated equality assertion. Missing/invalid arguments exit 2. Paths containing spaces must be quoted. Use `--smoke SCREENSHOT.png` only for automated regression/render verification.

For Git difftool, replace the executable path below with your actual extracted location. Configuration is opt-in and the app never edits Git settings:

```powershell
git config --global diff.tool diffbeacon
git config --global difftool.diffbeacon.cmd '"C:/Tools/DiffBeacon/DiffBeacon.exe" "$LOCAL" "$REMOTE"'
git difftool
```

Git mergetool is **planned**. This build has two-way text editing but no base/local/remote conflict-resolution contract and must not be configured as an automatic mergetool. Git status, branch, blame and version-browser windows are also planned. HTML export currently uses the GUI; `--report` is not implemented.
