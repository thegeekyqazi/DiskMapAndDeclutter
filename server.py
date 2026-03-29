import os
from fastapi import FastAPI, HTTPException
from fastapi.middleware.cors import CORSMiddleware
from pydantic import BaseModel

# --- Import All 4 Custom Engines ---
from ScannerEngine import build_storage_tree, flatten_for_plotly
from deduplicator import find_duplicates, delete_selected_files

from TempEngine import scan_temp_space, flush_temp_files
from GhostHunter import find_ghost_folders, delete_ghost_folders
app = FastAPI(title="DeepDrive API")

# Allow the frontend to communicate with this server
app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"], 
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

# Expected data structure for the deduplicator's delete request
class DeleteRequest(BaseModel):
    file_paths: list[str]
class DeleteFolderRequest(BaseModel):
    folder_paths: list[str]

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