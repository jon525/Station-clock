# Station Clock

A full-screen screensaver modelled on the classic European railway station clock.

It behaves like the real thing:

- The red second hand sweeps smoothly round the dial in about 58.5 seconds, then waits at 12.
- On the full minute, the minute hand jumps forward one mark with a small mechanical bounce and the second hand sets off again.
- The hour hand nudges forward with each minute jump.

## Using it

Open `index.html` in any modern browser. It shows your computer's local time.

- Click anywhere, or press **F**, to toggle full screen.
- The mouse pointer hides after three seconds without movement.
- The page asks the browser to keep the display awake while it is open.
- The clock drifts very slowly around the screen to avoid burn-in.

Everything is in the single `index.html` file, with no dependencies and no network access needed.
