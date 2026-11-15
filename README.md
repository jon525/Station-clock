# Station Clock

A full-screen screensaver modelled on the classic European railway station clock.

It behaves like the real thing:

- The red second hand sweeps smoothly round the dial in about 58.5 seconds, then waits at 12.
- On the full minute, the minute hand jumps forward one mark with a small mechanical bounce and the second hand sets off again.
- The hour hand nudges forward with each minute jump.

## Browser version

Open `index.html` in any modern browser. It shows your computer's local time.

- Click anywhere, or press **F**, to toggle full screen.
- The mouse pointer hides after three seconds without movement.
- The page asks the browser to keep the display awake while it is open.
- The clock drifts very slowly around the screen to avoid burn-in.

Everything is in the single `index.html` file, with no dependencies and no network access needed.

## Windows screensaver

The `windows` folder holds a real Windows screensaver (`.scr`) with the same clock.

1. Download `windows/StationClock.cs` and `windows/build.bat` into the same folder.
2. Double-click `build.bat`. It uses the C# compiler that ships with Windows, so nothing needs installing.
3. Right-click the new `StationClock.scr` and choose **Install**. Keep the file where it is; Windows runs it from there.

Right-click `StationClock.scr` and choose **Configure** to set the clock size, turn the slow burn-in drift on or off, and choose between every monitor or the main one only. Any key, click or mouse movement closes the screensaver.
