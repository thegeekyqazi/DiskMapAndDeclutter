

## Current Limitations

This project is an early visualization tool, not a full decluttering system. Current limitations:

- No file deletion
- No file moving
- No sorting controls
- No file-type breakdown
- No progress indicator for very large scans beyond the spinner
- No authentication or path restrictions beyond basic safety checks
- No packaged desktop app; it is a local HTML page plus local API server
- No dependency file such as `requirements.txt`

This structure is designed specifically for Plotly's sunburst chart.

## File Overview

- `ScannerEngine.py`: scanning logic and Plotly data conversion
- `server.py`: FastAPI bridge between scanner and frontend
- `index.html`: local UI and chart rendering

## In Plain English

This app is a **local storage map viewer**. You point it at a folder, it measures folder sizes recursively, and it turns the results into a visual chart so you can identify which directories are using the most space.

