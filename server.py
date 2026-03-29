import os
from fastapi import FastAPI, HTTPException
from fastapi.middleware.cors import CORSMiddleware
from pydantic import BaseModel

# Import both engines
from ScannerEngine import build_storage_tree, flatten_for_plotly
from deduplicator import find_duplicates, delete_selected_files

app = FastAPI(title="Declutter Engine API")

app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"], 
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

# --- Define the Expected Data Structure for Deletion ---
class DeleteRequest(BaseModel):
    file_paths: list[str]

# --- Visualization Endpoint ---
@app.get("/api/scan")
def scan_directory(target_path: str = "."):
    """Scans the target directory and returns a flattened storage tree for visualization."""
    validate_directory(target_path)
    try:
        raw_tree = build_storage_tree(target_path)
        return flatten_for_plotly(raw_tree)
    except PermissionError as e:
        raise HTTPException(status_code=403, detail=str(e))

# --- Deduplicator Endpoints ---
@app.get("/api/duplicates")
def get_duplicates(target_path: str = "."):
    """Runs the 3-stage funnel and returns the list of duplicates."""
    validate_directory(target_path)
    try:
        return find_duplicates(target_path)
    except PermissionError as e:
        raise HTTPException(status_code=403, detail=str(e))

@app.post("/api/delete")
def delete_files(request: DeleteRequest):
    """Accepts a list of file paths and deletes them."""
    if not request.file_paths:
        raise HTTPException(status_code=400, detail="No files provided for deletion.")
    
    # Call the new deletion function
    return delete_selected_files(request.file_paths)

def validate_directory(target_path: str):
    """Helper function to validate the provided directory path."""
    if not os.path.exists(target_path):
        raise HTTPException(status_code=404, detail="Directory not found")
    if not os.path.isdir(target_path):
        raise HTTPException(status_code=400, detail="Provided path is not a directory")