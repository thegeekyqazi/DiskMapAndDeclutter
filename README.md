# 🚀 System Declutter Master (DiskMap & Declutter)

A blazing-fast, cross-platform system utility designed to map storage, mathematically prove file duplication, and safely hunt down orphaned Windows system cache without accidental deletions. 

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
* **Result:** 100% mathematical certainty that files are identical before presenting them for deletion.

### 3. 👻 Windows Deep Clean (Ghost Hunter & Temp Flusher)
Uninstallers notoriously leave massive cache folders behind in `C:\Users\<User>\AppData`. 
* **Registry Interrogation:** The backend scrapes the Windows Registry (`winreg`) to build a master list of currently installed software and publishers.
* **Fuzzy Logic Matching:** Uses `difflib` (Ratcliff/Obershelp algorithm) to bi-directionally compare orphaned `AppData` folder names against the active Registry list.
* **Temp Cache Flusher:** Safely and aggressively wipes `AppData\Local\Temp` and `Windows\Temp`, gracefully skipping files currently locked by the OS.

---

## 🛡️ Safety & Guardrails
Deleting files is dangerous. This app is built with extreme safety constraints:
* **Dynamic OS Detection:** Automatically blocks scanning of critical system paths like `C:\Windows` or Linux `/boot`.
* **AppData Whitelists:** Hardcoded safeguards prevent the Ghost Hunter from flagging critical Windows folders (e.g., `Microsoft`, `Packages`, `Comms`).
* **Frontend Batch Rendering:** Instead of freezing the browser by injecting thousands of DOM elements iteratively, the UI builds a single payload and renders it in O(1) time.

---

## 🛠️ Tech Stack
* **Backend:** Python 3.8+, FastAPI, Uvicorn
* **Algorithms:** `hashlib` (Cryptography), `difflib` (Fuzzy String Matching), `winreg` (Windows API)
* **Frontend:** Vanilla HTML5, CSS3, JavaScript (ES6+)
* **Data Visualization:** Plotly.js
* **Icons:** Google Material Icons

---

## 🚀 Quickstart Installation

Because the core engines rely heavily on Python's standard library, the dependency footprint is incredibly small.

**1. Clone the repository**
```bash
git clone [https://github.com/thegeekyqazi/DiskMapAndDeclutter.git](https://github.com/thegeekyqazi/DiskMapAndDeclutter.git)
cd DiskMapAndDeclutter
```

**2. Install the API requirements**
```bash
pip install fastapi uvicorn pydantic
```

**3. Boot the backend server**
```bash
uvicorn server:app --reload
```

**4. Launch the UI**
Simply open `index.html` in any modern web browser.

---

## 🔮 Future Roadmap
* **Top 50 Space Hogs:** A radar feature to instantly flag the largest single files on the drive.
* **Stale File Detection:** Flagging files that haven't been modified or accessed in over 365 days.
* **Recycle Bin Integration:** Routing deleted files to the OS Recycle Bin (`send2trash`) instead of permanent vaporization for an ultimate "Undo" failsafe.

---

### ⚠️ Disclaimer
*This tool is designed to permanently delete files and directories. While strict safety guardrails are implemented, users should always review flagged duplicates and AppData Ghost folders carefully before executing a deletion. Use at your own risk.*