import os
import sys
import uvicorn
import platform
from fastapi import FastAPI, HTTPException
from fastapi.middleware.cors import CORSMiddleware
from pydantic import BaseModel

# --- Universal Custom Engines ---
from ScannerEngine import build_storage_tree, flatten_for_plotly
from deduplicator import find_duplicates, delete_selected_files
from AnalysisEngine import get_top_50_files, get_stale_files

# --- OS-Specific Imports & Stubs ---
if platform.system() == "Windows":
    import ctypes
    from TempEngine import scan_temp_space, flush_temp_files
    from GhostHunter import find_ghost_folders, delete_ghost_folders
    from DebloatEngine import scan_bloatware, remove_bloatware, disable_windows_telemetry
else:
    # Safe Linux Stubs: Prevents import crashes and feeds empty data to Windows UI tabs
    def find_ghost_folders(): return []
    def delete_ghost_folders(paths): return {"deleted": [], "failed": paths, "freed_bytes": 0}
    def scan_temp_space(): return {"total_bytes": 0, "total_files": 0}
    def flush_temp_files(): return {"deleted_count": 0, "freed_bytes": 0, "locked_count": 0}
    def scan_bloatware(): return []
    def remove_bloatware(packages): return {"success": [], "failed": packages}
    def disable_windows_telemetry(): return {"status": "error", "message": "Privacy Tweaks are Windows-only."}

app = FastAPI(title="DeepDrive API")

@app.get("/api/system_status")
def system_status():
    """Tells the frontend if we are in Read-Only Root Mode."""
    is_root_mode = False
    if platform.system() == "Linux" and is_admin():
        is_root_mode = True
    return {"is_root": is_root_mode, "os": platform.system()}

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
# SYSTEM GUARDRAILS (The Kill-Switch)
# ==========================================
def is_admin():
    """Cross-platform check for elevated privileges."""
    if platform.system() == "Windows":
        try:
            return ctypes.windll.shell32.IsUserAnAdmin()
        except:
            return False
    else:
        # Linux / macOS check for root
        return os.getuid() == 0

def check_root_lockdown():
    """
    If running as root on Linux, DeepDrive is strictly Read-Only.
    This prevents accidental deletion of critical system files.
    """
    if platform.system() == "Linux" and is_admin():
        raise HTTPException(
            status_code=403, 
            detail="SAFETY LOCKDOWN: DeepDrive is running as Root. All deletion operations are disabled to protect the OS."
        )


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
    check_root_lockdown() # ⚡ Safety Intercept
    if not request.file_paths:
        raise HTTPException(status_code=400, detail="No files provided for deletion.")
    return delete_selected_files(request.file_paths)

# ==========================================
# TAB 3: WINDOWS DEEP CLEAN
# ==========================================
@app.get("/api/ghosts")
def get_ghosts():
    try:
        return find_ghost_folders()
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"Ghost Hunter Error: {str(e)}")

@app.get("/api/temp/scan")
def scan_temp():
    try:
        return scan_temp_space()
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"Temp Scan Error: {str(e)}")

@app.post("/api/temp/flush")
def flush_temp():
    check_root_lockdown() # ⚡ Safety Intercept
    try:
        return flush_temp_files()
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"Temp Flush Error: {str(e)}")
    
@app.post("/api/ghosts/delete")
def delete_ghosts(request: DeleteFolderRequest):
    check_root_lockdown() # ⚡ Safety Intercept
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
    try:
        return scan_bloatware()
    except Exception as e:
        raise HTTPException(status_code=500, detail=str(e))

@app.post("/api/debloat/remove")
def api_remove_bloatware(request: DebloatRequest):
    check_root_lockdown() # ⚡ Safety Intercept
    if not request.packages:
        raise HTTPException(status_code=400, detail="No packages provided.")
    try:
        return remove_bloatware(request.packages)
    except Exception as e:
        raise HTTPException(status_code=500, detail=str(e))

@app.post("/api/debloat/telemetry")
def api_disable_telemetry():
    check_root_lockdown() # ⚡ Safety Intercept
    result = disable_windows_telemetry()
    if result["status"] == "error":
        raise HTTPException(status_code=403, detail=result["message"])
    return result

# ==========================================
# TAB 5: FILE RADAR
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
# CROSS-PLATFORM BOOTLOADER
# ==========================================
if __name__ == "__main__":
    current_os = platform.system()
    
    if current_os == "Windows":
        if not is_admin():
            print("DeepDrive requires Administrator privileges for the OS Debloater.")
            print("Triggering UAC prompt...")
            ctypes.windll.shell32.ShellExecuteW(None, "runas", sys.executable, os.path.abspath(__file__), None, 1)
            sys.exit()
        print("Elevated Windows privileges confirmed. Booting DeepDrive Server...")
        
    elif current_os == "Linux":
        if is_admin():
            print("\n[!] WARNING: Running as Root. DeepDrive is locked in Read-Only Mode.")
            print("    You can safely map the '/' filesystem, but deletions are disabled.\n")
        else:
            print("\n[*] Running in User Mode. You can delete files in your /home directory.")
            print("    (To view the entire '/' filesystem, restart app with 'sudo python3 server.py')\n")

    uvicorn.run("server:app", host="127.0.0.1", port=8000, reload=True)