# 🚀 DeepDrive

A blazing-fast system utility designed to map storage, mathematically prove file duplication, safely hunt down orphaned Windows system cache, and surgically debloat your OS. 

Built with a lightweight **Python/FastAPI** backend and a premium, responsive **Vanilla JS/CSS** frontend.

---

## ✨ Core Features

### 1. 📊 Visual Disk Mapper
Stop guessing what is eating your hard drive. The Visual Mapper recursively scans any target directory and generates a beautiful, interactive **Plotly Sunburst Chart**. 
* **Bottom-up aggregation:** Calculates folder sizes instantly.
* **Interactive drill-down:** Click on any folder ring to zoom in and explore its nested contents.

### 2. 👯‍♂️ Smart Deduplicator (The 3-Stage Funnel)
Scanning thousands of files for duplicates can take hours. We built a highly optimized 3-stage mathematical funnel to do it in seconds:
* **Stage 1 (O(1) Size Grouping):** Instantly filters out unique files by grouping them by exact byte size. Ignores files under 1MB to optimize NTFS file system reads.
* **Stage 2 (4KB Fast-Hash):** Reads only the first 4,096 bytes (the header) of surviving files and runs an MD5 hash. If the headers differ, they are discarded.
* **Stage 3 (SHA-256 Cryptographic Hash):** Streams the remaining files in 64KB chunks to prevent RAM overflow and generates a full SHA-256 cryptographic signature. 

### 3. 👻 Windows Deep Clean
* **Ghost Hunter:** Scrapes the Windows Registry (`winreg`) and uses fuzzy string matching (`difflib`) to bi-directionally compare installed software against `AppData` folders, finding invisible orphaned data left behind by uninstalled apps.
* **Temp Cache Flusher:** Safely and aggressively wipes `AppData\Local\Temp` and `Windows\Temp`, gracefully skipping files currently locked by the OS.

### 4. 🛡️ OS Debloater & Privacy Shield
Inspired by the legendary CTT WinUtil project, this module safely removes pre-installed Windows bloat without breaking the OS.
* **AppxPackage Purge:** Uses background PowerShell commands to cross-reference installed apps against a highly curated, 40+ item whitelist of known OEM junk (Candy Crush, Xbox Overlays, etc.) and uninstalls them securely.
* **Telemetry Blocker:** Applies direct Windows Registry tweaks to disable Microsoft Telemetry and background data collection.

### 5. 📡 File Radar
The ultimate psychological decluttering tool.
* **Space Hogs:** Instantly surfaces the 50 absolute largest files in any target directory.
* **Time Machine (Stale Files):** Hunts down massive files (>50MB) that haven't been accessed or modified in over 365 days.

---

## 🔒 Safety & Guardrails
Deleting files and altering the registry is dangerous. DeepDrive is built with extreme safety constraints:
* **Native UAC Elevation:** Automatically triggers the native Windows Administrator prompt on boot to ensure registry tweaks are handled securely.
* **AppData Whitelists:** Hardcoded safeguards prevent the Ghost Hunter from flagging critical Windows folders.
* **Frontend Batch Rendering:** Instead of freezing the browser by injecting thousands of DOM elements iteratively, the UI compiles a single massive HTML string in the background and renders it in O(1) time.

---

## 🛠️ Tech Stack
* **Backend:** Python 3.8+, FastAPI, Uvicorn
* **OS Interfacing:** `ctypes`, `winreg`, `subprocess` (PowerShell)
* **Algorithms:** `hashlib` (Cryptography), `difflib` (Fuzzy String Matching)
* **Frontend:** Vanilla HTML5, CSS3, JavaScript (ES6+)
* **Data Visualization:** Plotly.js
* **Icons:** Google Material Icons

---

## 🚀 Quickstart Installation

Because the core engines rely entirely on Python's standard library, the dependency footprint is incredibly small.

**1. Clone the repository**
```bash
git clone [https://github.com/thegeekyqazi/DeepDrive.git](https://github.com/thegeekyqazi/DeepDrive.git)
cd DeepDrive
```

**2. Install the API requirements**
```bash
pip install fastapi uvicorn pydantic
```

**3. Boot the application**
```bash
python server.py
```
*Note: DeepDrive will automatically prompt you for Windows Administrator privileges (UAC), start the local server, and open the UI in your default web browser!*

---

## 🔮 Future Roadmap
* **Perceptual Image Hashing:** Finding duplicate photos that look identical even if their resolutions or file types differ.
* **Linux Port:** A `LinuxEngine.py` module to target `~/.cache`, Systemd Journals, and orphaned package dotfiles via `apt` and `pacman`.
* **Context Menu Integration:** Adding DeepDrive to the native Windows right-click menu.

---

### ⚠️ Disclaimer
*This tool is designed to permanently delete files, directories, and AppxPackages. While strict safety guardrails are implemented, users should always review flagged duplicates and flagged AppData folders carefully before executing a deletion. Use at your own risk.*