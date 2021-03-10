
## The MatterControl Library

Within the Library folder you will find a wide range of printable content, things like:
- Design Apps
- Local Library
- The Downloads folder
- Your Cloud Library
- Calibration Parts
- and more...

## Extending the Downloads folder

If you are looking to add links to other folders on your hard drive you can easily add a file named [any name].library to your downloads folders and it will show up when you look in the Downloads folder.

To create a link to a local folder:
- Add a file named C Drive.library (any name will work as long as the extension is .library)
- Edit the file and write the following into it
```
{
  "type": "local",
  "path": "C:\\"
}
```

You can also create a link to a GitHub repository:
- Add a file named Benchy.library (you can change the name to anything)
- Edit the file and write the following into it 
```
{
  "type": "GitHub",
  "owner": "CreativeTools",
  "repository": "3DBenchy",
  "path": ""
}
```