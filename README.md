
# 🚀 DeepDrive

A blazing-fast system utility designed to map storage, mathematically prove file duplication, safely hunt down orphaned Windows system cache, and surgically debloat your OS. 

Built with a lightweight **Python/FastAPI** backend and a premium, responsive, **modular Vanilla JS/CSS** frontend.

---

## ✨ Core Features

### 1. 📊 Visual Disk Mapper
Stop guessing what is eating your hard drive. The Visual Mapper recursively scans any target directory and generates a beautiful, interactive **Plotly Sunburst Chart**. 
* **Dynamic Unit Scaling:** View your disk usage in Bytes, KB, MB, or GB via a real-time UI toggle.
* **Custom Ignore Lists:** Instantly bypass massive directories (e.g., `node_modules`, `steamapps`) using O(1) backend set lookups for lightning-fast scan times.
* **Interactive Drill-Down:** Click on any folder ring to zoom in and explore its nested contents.

### 2. 👯‍♂️ Smart Deduplicator (The 3-Stage Funnel)
Scanning thousands of files for duplicates can take hours. We built a highly optimized 3-stage mathematical funnel to do it in seconds:
* **Stage 1 (O(1) Size Grouping):** Instantly filters out unique files by grouping them by exact byte size. Ignores files under 1MB to optimize file system reads.
* **Stage 2 (4KB Fast-Hash):** Reads only the first 4,096 bytes (the header) of surviving files and runs an MD5 hash. If the headers differ, they are discarded.
* **Stage 3 (SHA-256 Cryptographic Hash):** Streams the remaining files in 64KB chunks to prevent RAM overflow and generates a full SHA-256 cryptographic signature. 

### 3. 📡 File Radar
The ultimate psychological decluttering tool.
* **Space Hogs:** Instantly surfaces the 50 absolute largest files in any target directory.
* **Time Machine (Stale Files):** Hunts down massive files (>50MB) that haven't been accessed or modified in over 365 days.

### 4. 👻 Windows Deep Clean (Windows Only)
* **Ghost Hunter:** Scrapes the Windows Registry (`winreg`) and uses fuzzy string matching to bi-directionally compare installed software against `AppData` folders, finding invisible orphaned data left behind by uninstalled apps.
* **Temp Cache Flusher:** Safely and aggressively wipes `AppData\Local\Temp` and `Windows\Temp`, gracefully skipping files currently locked by the OS.

### 5. 🛡️ OS Debloater & Privacy Shield (Windows Only)
Inspired by the legendary CTT WinUtil project, this module safely removes pre-installed Windows bloat without breaking the OS.
* **AppxPackage Purge:** Uses background PowerShell commands to cross-reference installed apps against a curated whitelist of known OEM junk and uninstalls them securely.
* **Telemetry Blocker:** Applies direct Windows Registry tweaks to disable Microsoft Telemetry and background data collection.

---

## 🔒 Safety & OS Guardrails
Deleting system files is dangerous. DeepDrive is built with extreme safety constraints and cross-platform awareness:

* **Linux Root Lockdown:** If you run the application with `sudo` (Please don't), DeepDrive will detect the elevated privileges and enter a strict **Read-Only System Mode**. A massive red UI banner will appear, allowing you to safely map the entire `/` filesystem while completely paralyzing all API deletion endpoints to protect the Linux kernel.
* **Virtual Filesystem Blacklist:** When running on Linux, the backend scanner employs a hardcoded blacklist (`/proc`, `/sys`, `/dev`, etc.) to prevent the app from getting trapped in infinite RAM/Kernel loops.
* **Smart OS UI Locks:** The frontend actively queries the backend OS on boot. If running on Linux, Windows-specific tabs (Registry/Appx) are visually locked, badged, and disabled.
* **Native UAC Elevation:** On Windows, the app automatically triggers the native Administrator prompt on boot to ensure registry tweaks are handled securely.

---

## 🛠️ Tech Stack
* **Backend:** Python 3.8+, FastAPI, Uvicorn, `StaticFiles`
* **OS Interfacing:** `ctypes`, `winreg`, `subprocess` (PowerShell), `os.getuid`
* **Algorithms:** `hashlib` (Cryptography), `difflib` (Fuzzy String Matching)
* **Frontend:** Modular Vanilla HTML5, CSS3, JavaScript (ES6+)
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
**2. Setup a Virtual Environment & Install API requirements**
```bash
python3 -m venv venv
source venv/bin/activate  # On Windows use: venv\Scripts\activate
pip install fastapi uvicorn pydantic
```
**3. Boot the application**
```bash
python server.py
```
*Note: On Windows, DeepDrive will automatically prompt you for Administrator privileges (UAC). On Linux, it will safely boot into User Mode restricted to your `/home` directory.*

---

## 🔮 Future Roadmap
* **Perceptual Image Hashing:** Finding duplicate photos that look identical even if their resolutions or file types differ.
* **Linux Deep Clean Integration:** Expanding the Linux toolset to target `~/.cache`, Systemd Journals, and orphaned package dotfiles via `apt` and `pacman`.
* **Context Menu Integration:** Adding DeepDrive to the native Windows right-click menu.

---

### ⚠️ Disclaimer
*This tool is designed to permanently delete files, directories, and AppxPackages. While strict safety guardrails are implemented, users should always review flagged duplicates and folders carefully before executing a deletion. Use at your own risk.*