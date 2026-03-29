import os
import sys
import ctypes
import uvicorn
from fastapi import FastAPI, HTTPException
from fastapi.middleware.cors import CORSMiddleware
from pydantic import BaseModel

# --- Import All Custom Engines ---
from ScannerEngine import build_storage_tree, flatten_for_plotly
from deduplicator import find_duplicates, delete_selected_files
from TempEngine import scan_temp_space, flush_temp_files
from GhostHunter import find_ghost_folders, delete_ghost_folders
from DebloatEngine import scan_bloatware, remove_bloatware, disable_windows_telemetry
from AnalysisEngine import get_top_50_files, get_stale_files
app = FastAPI(title="DeepDrive API")

# Allow the frontend to communicate with this server
app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"], 
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

# --- Expected Data Structures ---
class DeleteRequest(BaseModel):
    file_paths: list[str]

class DeleteFolderRequest(BaseModel):
    folder_paths: list[str]

class DebloatRequest(BaseModel):
    packages: list[str]


# ==========================================
# TAB 1: VISUAL DISK MAPPER
# ==========================================
@app.get("/api/scan")
def scan_directory(target_path: str = "."):
    if not os.path.exists(target_path):
        raise HTTPException(status_code=404, detail="Directory not found")
    try:
        raw_tree = build_storage_tree(target_path)
        return flatten_for_plotly(raw_tree)
    except PermissionError as e:
        raise HTTPException(status_code=403, detail=str(e))


# ==========================================
# TAB 2: SMART DEDUPLICATOR
# ==========================================
@app.get("/api/duplicates")
def get_duplicates(target_path: str = "."):
    if not os.path.exists(target_path):
        raise HTTPException(status_code=404, detail="Directory not found")
    try:
        return find_duplicates(target_path)
    except PermissionError as e:
        raise HTTPException(status_code=403, detail=str(e))

@app.post("/api/delete")
def delete_files(request: DeleteRequest):
    if not request.file_paths:
        raise HTTPException(status_code=400, detail="No files provided for deletion.")
    return delete_selected_files(request.file_paths)


# ==========================================
# TAB 3: WINDOWS DEEP CLEAN (Ghosts & Temp)
# ==========================================
@app.get("/api/ghosts")
def get_ghosts():
    """Scans the registry and AppData for orphaned software folders."""
    try:
        return find_ghost_folders()
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"Ghost Hunter Error: {str(e)}")

@app.get("/api/temp/scan")
def scan_temp():
    """Calculates the total size of junk in the Windows Temp folders."""
    try:
        return scan_temp_space()
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"Temp Scan Error: {str(e)}")

@app.post("/api/temp/flush")
def flush_temp():
    """Aggressively deletes contents of Windows Temp folders."""
    try:
        return flush_temp_files()
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"Temp Flush Error: {str(e)}")
    
@app.post("/api/ghosts/delete")
def delete_ghosts(request: DeleteFolderRequest):
    """Accepts a list of folder paths and recursively deletes them."""
    if not request.folder_paths:
        raise HTTPException(status_code=400, detail="No folders provided for deletion.")
    try:
        return delete_ghost_folders(request.folder_paths)
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"Ghost Deletion Error: {str(e)}")


# ==========================================
# TAB 4: OS DEBLOATER & PRIVACY
# ==========================================
@app.get("/api/debloat/scan")
def api_scan_bloatware():
    """Queries PowerShell for installed AppxPackages matching the bloatware list."""
    try:
        return scan_bloatware()
    except Exception as e:
        raise HTTPException(status_code=500, detail=str(e))

@app.post("/api/debloat/remove")
def api_remove_bloatware(request: DebloatRequest):
    """Executes the Remove-AppxPackage command for selected bloatware."""
    if not request.packages:
        raise HTTPException(status_code=400, detail="No packages provided.")
    try:
        return remove_bloatware(request.packages)
    except Exception as e:
        raise HTTPException(status_code=500, detail=str(e))

@app.post("/api/debloat/telemetry")
def api_disable_telemetry():
    """Applies Windows Registry tweaks to disable Microsoft Telemetry."""
    result = disable_windows_telemetry()
    if result["status"] == "error":
        raise HTTPException(status_code=403, detail=result["message"])
    return result

# ==========================================
# TAB 5: FILE RADAR (Top 50 & Stale)
# ==========================================
@app.get("/api/radar/top50")
def api_top50(target_path: str = "."):
    if not os.path.exists(target_path):
        raise HTTPException(status_code=404, detail="Directory not found")
    try:
        return get_top_50_files(target_path)
    except Exception as e:
        raise HTTPException(status_code=500, detail=str(e))

@app.get("/api/radar/stale")
def api_stale(target_path: str = "."):
    if not os.path.exists(target_path):
        raise HTTPException(status_code=404, detail="Directory not found")
    try:
        return get_stale_files(target_path)
    except Exception as e:
        raise HTTPException(status_code=500, detail=str(e))
# ==========================================
# WINDOWS UAC ELEVATION & BOOTLOADER
# ==========================================
def is_admin():
    """Checks if the current Python process has Administrator privileges."""
    try:
        return ctypes.windll.shell32.IsUserAnAdmin()
    except:
        return False

if __name__ == "__main__":
    if not is_admin():
        print("DeepDrive requires Administrator privileges for the OS Debloater.")
        print("Triggering UAC prompt...")
        
        # This tells Windows to relaunch this script, but elevated (UAC Prompt)
        ctypes.windll.shell32.ShellExecuteW(
            None, 
            "runas", 
            sys.executable, 
            os.path.abspath(__file__), 
            None, 
            1
        )
        # Exit this non-admin instance immediately
        sys.exit()

    # If the code reaches here, the user clicked "Yes" on the UAC prompt
    print("Elevated privileges confirmed. Booting DeepDrive Server...")
    
    # Start the Uvicorn server programmatically
    uvicorn.run("server:app", host="127.0.0.1", port=8000, reload=True)